using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AdmintourFingerprintMiddleware.Models;
using libzkfpcsharp;

namespace AdmintourFingerprintMiddleware.Services
{
    public class FingerprintService : IDisposable
    {
        private readonly ILogger<FingerprintService> _logger;

        private IntPtr _deviceHandle = IntPtr.Zero;
        private IntPtr _dbHandle = IntPtr.Zero;

        private bool _initialized = false;
        private readonly object _initLock = new();

        private readonly SemaphoreSlim _captureLock = new(1, 1);

        private const int TEMPLATE_SIZE = 4096;

        private int _imgWidth;
        private int _imgHeight;

        public FingerprintService(ILogger<FingerprintService> logger)
        {
            _logger = logger;
        }

        private void EnsureReady(int deviceIndex = 0)
        {
            lock (_initLock)
            {
                if (!_initialized)
                {
                    _logger.LogInformation("Inicializando SDK automáticamente...");
                    if (!Inicializar())
                        throw new Exception("No se pudo inicializar el SDK");
                }

                if (_deviceHandle == IntPtr.Zero)
                {
                    _logger.LogInformation("Abriendo lector automáticamente...");
                    if (!AbrirDispositivo(deviceIndex))
                        throw new Exception("No se pudo abrir el lector");
                }
            }
        }


        // =========================================================
        // INIT
        // =========================================================

        public bool Inicializar()
        {
            lock (_initLock)
            {
                if (_initialized)
                    return true;

                int ret = zkfp2.Init();

                if (ret != zkfp.ZKFP_ERR_OK)
                {
                    _logger.LogError("Error inicializando SDK: {Ret}", ret);
                    return false;
                }

                _dbHandle = zkfp2.DBInit();

                if (_dbHandle == IntPtr.Zero)
                {
                    _logger.LogError("No se pudo inicializar DB biométrica.");
                    zkfp2.Terminate();
                    return false;
                }

                _initialized = true;
                _logger.LogInformation("SDK inicializado correctamente.");
                return true;
            }
        }

        // =========================================================
        // DEVICE
        // =========================================================

        public int ObtenerCantidadDispositivos()
        {
            if (!_initialized)
                throw new InvalidOperationException("SDK no inicializado");

            return zkfp2.GetDeviceCount();
        }

        public bool AbrirDispositivo(int index = 0)
        {
            lock (_initLock)
            {
                if (!_initialized)
                    throw new InvalidOperationException("SDK no inicializado");

                if (_deviceHandle != IntPtr.Zero)
                    return true;

                _deviceHandle = zkfp2.OpenDevice(index);

                if (_deviceHandle == IntPtr.Zero)
                {
                    _logger.LogError("No se pudo abrir dispositivo.");
                    return false;
                }

                CachearResolucion();

                _logger.LogInformation("Dispositivo abierto. Resolución {W}x{H}", _imgWidth, _imgHeight);
                return true;
            }
        }

        private void CachearResolucion()
        {
            _imgWidth = GetIntParameter(1);
            _imgHeight = GetIntParameter(2);
        }

        public void CerrarDispositivo()
        {
            lock (_initLock)
            {
                if (_deviceHandle != IntPtr.Zero)
                {
                    zkfp2.CloseDevice(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                }
            }
        }

        // =========================================================
        // PARAMETERS
        // =========================================================

        private int GetIntParameter(int id)
        {
            byte[] buffer = new byte[4];
            int size = buffer.Length;

            int ret = zkfp2.GetParameters(_deviceHandle, id, buffer, ref size);

            if (ret != zkfp.ZKFP_ERR_OK)
                throw new Exception($"Error leyendo parámetro {id}");

            return BitConverter.ToInt32(buffer, 0);
        }

        private string GetStringParameter(int id)
        {
            byte[] buffer = new byte[256];
            int size = buffer.Length;

            int ret = zkfp2.GetParameters(_deviceHandle, id, buffer, ref size);

            if (ret != zkfp.ZKFP_ERR_OK)
                throw new Exception($"Error leyendo parámetro {id}");

            return System.Text.Encoding.ASCII.GetString(buffer, 0, size).TrimEnd('\0');
        }

        public DeviceInfo GetDeviceInfo()
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Dispositivo no abierto");

            return new DeviceInfo
            {
                Width = _imgWidth,
                Height = _imgHeight,
                Vendor = GetStringParameter(1101),
                Product = GetStringParameter(1102),
                Serial = GetStringParameter(1103)
            };
        }

        // =========================================================
        // CAPTURE CORE (PROTEGIDO)
        // =========================================================

        public async Task<byte[]> CapturarHuellaAsync(CancellationToken token = default)
        {
            EnsureReady(); // Inicia y abre el sdk y el lector

            await _captureLock.WaitAsync(token);

            try
            {
                byte[] img = new byte[_imgWidth * _imgHeight];
                byte[] template = new byte[TEMPLATE_SIZE];

                while (!token.IsCancellationRequested)
                {
                    int len = template.Length;

                    int ret = zkfp2.AcquireFingerprint(_deviceHandle, img, template, ref len);

                    if (ret == zkfp.ZKFP_ERR_OK && len > 0)
                    {
                        byte[] result = new byte[len];
                        Array.Copy(template, result, len);
                        return result;
                    }

                    await Task.Delay(150, token);
                }

                throw new OperationCanceledException();
            }
            finally
            {
                _captureLock.Release();
            }
        }


        // =========================================================
        // CAPTURE PUBLIC
        // =========================================================

        public async Task<string> CapturarUnicaAsync(CancellationToken token)
        {
            var tpl = await CapturarHuellaAsync(token);
            return Convert.ToBase64String(tpl);
        }

        public async Task<string> CapturarHuella3VecesAsync(CancellationToken token = default)
        {
            EnsureReady();

            var t1 = await CapturarHuellaAsync(token);
            var t2 = await CapturarHuellaAsync(token);
            var t3 = await CapturarHuellaAsync(token);

            byte[] merged = new byte[TEMPLATE_SIZE];
            int len = merged.Length;

            int ret = zkfp2.DBMerge(_dbHandle, t1, t2, t3, merged, ref len);

            if (ret != zkfp.ZKFP_ERR_OK)
                throw new Exception($"DBMerge error {ret}");

            byte[] finalTpl = new byte[len];
            Array.Copy(merged, finalTpl, len);

            return Convert.ToBase64String(finalTpl);
        }

        // =========================================================
        // MATCH
        // =========================================================

        public async Task<bool> CompararDosHuellasAsync(string base64, CancellationToken token = default)
        {
            EnsureReady();

            byte[] stored = Convert.FromBase64String(base64);
            byte[] live = await CapturarHuellaAsync(token);

            int score = zkfp2.DBMatch(_dbHandle, stored, live);

            return score >= 0;
        }

        public class MatchListaResult
        {
            public bool Coincide { get; set; }
            public int Indice { get; set; }
            public int Score { get; set; }
            public int Total { get; set; }
        }

        public async Task<MatchListaResult> BuscarCoincidenciaEnListaAsync(
            string[] templatesBase64,
            CancellationToken token = default)
        {
            EnsureReady();

            if (templatesBase64 == null || templatesBase64.Length == 0)
                throw new ArgumentException("Lista vacía");

            byte[] captured = await CapturarHuellaAsync(token);

            int bestScore = -1;
            int bestIndex = -1;

            for (int i = 0; i < templatesBase64.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(templatesBase64[i]))
                    continue;

                byte[] stored;

                try
                {
                    stored = Convert.FromBase64String(templatesBase64[i]);
                }
                catch
                {
                    continue;
                }

                int score = zkfp2.DBMatch(_dbHandle, stored, captured);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            bool match = bestScore >= 0;

            return new MatchListaResult
            {
                Coincide = match,
                Indice = match ? bestIndex : -1,
                Score = bestScore,
                Total = templatesBase64.Length
            };
        }

        // =========================================================
        // RESET
        // =========================================================

        public bool Reiniciar(int indice = 0)
        {
            lock (_initLock)
            {
                try
                {
                    CerrarDispositivo();

                    if (_dbHandle != IntPtr.Zero)
                    {
                        zkfp2.DBFree(_dbHandle);
                        _dbHandle = IntPtr.Zero;
                    }

                    if (_initialized)
                    {
                        zkfp2.Terminate();
                        _initialized = false;
                    }

                    return Inicializar() && AbrirDispositivo(indice);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reiniciando lector");
                    return false;
                }
            }
        }

        // =========================================================
        // DISPOSE
        // =========================================================

        public void Dispose()
        {
            CerrarDispositivo();

            if (_dbHandle != IntPtr.Zero)
            {
                zkfp2.DBFree(_dbHandle);
                _dbHandle = IntPtr.Zero;
            }

            if (_initialized)
            {
                zkfp2.Terminate();
                _initialized = false;
            }
        }
    }
}
