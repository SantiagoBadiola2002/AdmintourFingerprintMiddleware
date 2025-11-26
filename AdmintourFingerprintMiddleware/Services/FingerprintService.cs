using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AdmintourFingerprintMiddleware.Models;
using libzkfpcsharp;

namespace AdmintourFingerprintMiddleware.Services
{
    /// <summary>
    /// Servicio listo para producción para manejar dispositivos de huellas ZKTeco.
    /// </summary>
    public class FingerprintService : IDisposable
    {
        private readonly ILogger<FingerprintService> _logger;
        private IntPtr _deviceHandle = IntPtr.Zero;
        private bool _initialized = false;
        private readonly object _lock = new();

        public FingerprintService(ILogger<FingerprintService> logger)
        {
            _logger = logger;
        }

        #region Inicialización y manejo del dispositivo

        public bool Inicializar()
        {
            lock (_lock)
            {
                if (_initialized)
                {
                    _logger.LogInformation("FingerprintService: SDK ya inicializado.");
                    return true;
                }

                _logger.LogInformation("FingerprintService: Inicializando SDK...");
                int ret = zkfp2.Init();

                if (ret != zkfp.ZKFP_ERR_OK)
                {
                    _logger.LogError("FingerprintService: Error al inicializar SDK. Código={Ret}", ret);
                    return false;
                }

                _initialized = true;
                _logger.LogInformation("FingerprintService: SDK inicializado correctamente.");
                return true;
            }
        }

        public int ObtenerCantidadDispositivos()
        {
            if (!_initialized) throw new InvalidOperationException("SDK no inicializado");

            int count = zkfp2.GetDeviceCount();
            _logger.LogInformation("FingerprintService: Dispositivos detectados = {Count}", count);
            return count;
        }

        public bool AbrirDispositivo(int index = 0)
        {
            if (!_initialized) throw new InvalidOperationException("SDK no inicializado");

            lock (_lock)
            {
                if (_deviceHandle != IntPtr.Zero)
                {
                    _logger.LogWarning("FingerprintService: Ya hay un dispositivo abierto.");
                    return true;
                }

                _logger.LogInformation("FingerprintService: Abriendo dispositivo índice {Index}", index);

                _deviceHandle = zkfp2.OpenDevice(index);

                if (_deviceHandle == IntPtr.Zero)
                {
                    _logger.LogError("FingerprintService: No se pudo abrir el dispositivo.");
                    return false;
                }

                _logger.LogInformation("FingerprintService: Dispositivo abierto correctamente.");
                return true;
            }
        }

        public void CerrarDispositivo()
        {
            lock (_lock)
            {
                if (_deviceHandle != IntPtr.Zero)
                {
                    _logger.LogInformation("FingerprintService: Cerrando dispositivo...");
                    zkfp2.CloseDevice(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                    _logger.LogInformation("FingerprintService: Dispositivo cerrado.");
                }
            }
        }

        #endregion

        #region Parámetros del dispositivo

        public (int Width, int Height) ObtenerTamanioImagen()
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Dispositivo no inicializado");

            int width = GetIntParameter(1);
            int height = GetIntParameter(2);

            return (width, height);
        }

        private int GetIntParameter(int parameterId)
        {
            int size = 4;
            byte[] buffer = new byte[size];

            int ret = zkfp2.GetParameters(_deviceHandle, parameterId, buffer, ref size);

            if (ret != zkfp.ZKFP_ERR_OK)
                throw new Exception($"Error leyendo parámetro {parameterId}, ret={ret}");

            return BitConverter.ToInt32(buffer, 0);
        }

        #endregion

        #region Info del dispositivo

        public DeviceInfo GetDeviceInfo()
        {
            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Dispositivo no inicializado");

            var (width, height) = ObtenerTamanioImagen();

            return new DeviceInfo
            {
                Width = width,
                Height = height,
                Vendor = GetStringParameter(1101),
                Product = GetStringParameter(1102),
                Serial = GetStringParameter(1103)
            };
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

        #endregion

        #region Captura de Huellas

        public async Task<byte[]> CapturarHuellaAsync(CancellationToken cancellationToken = default)
        {
            if (!_initialized || _deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("El lector no está listo. Inicialice y abra el dispositivo.");

            if (_deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("Dispositivo no inicializado");

            var (width, height) = ObtenerTamanioImagen();
            byte[] imgBuffer = new byte[width * height];
            byte[] templateBuffer = new byte[2048];

            _logger.LogInformation("FingerprintService: Esperando huella...");

            while (!cancellationToken.IsCancellationRequested)
            {
                int size = templateBuffer.Length;
                int ret = zkfp2.AcquireFingerprint(_deviceHandle, imgBuffer, templateBuffer, ref size);

                if (ret == zkfp.ZKFP_ERR_OK && size > 0)
                {
                    byte[] result = new byte[size];
                    Array.Copy(templateBuffer, result, size);
                    _logger.LogInformation("FingerprintService: Huella capturada. Size={Size}", size);
                    return result;
                }

                await Task.Delay(150, cancellationToken);
            }

            throw new OperationCanceledException("Captura cancelada");
        }

        /// <summary>
        /// Captura 3 veces y genera template fusionado.
        /// </summary>
        public async Task<string> CapturarHuella3VecesAsync(CancellationToken token = default)
        {
            if (!_initialized || _deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("El lector no está listo. Inicialice y abra el dispositivo.");

            const int Count = 3;
            byte[][] templates = new byte[Count][];

            for (int i = 0; i < Count; i++)
            {
                _logger.LogInformation("FingerprintService: Captura {Index}/3", i + 1);
                templates[i] = await CapturarHuellaAsync(token);
            }

            IntPtr db = zkfp2.DBInit();
            if (db == IntPtr.Zero)
                throw new Exception("No se pudo inicializar DB.");

            byte[] merged = new byte[2048];
            int mergedSize = merged.Length;

            int retMerge = zkfp2.DBMerge(db, templates[0], templates[1], templates[2], merged, ref mergedSize);

            zkfp2.DBFree(db);

            if (retMerge != zkfp.ZKFP_ERR_OK)
                throw new Exception($"Error al fusionar templates: {retMerge}");

            byte[] finalTemplate = new byte[mergedSize];
            Array.Copy(merged, finalTemplate, mergedSize);

            return Convert.ToBase64String(finalTemplate);
        }

        #endregion

        #region Comparación

        public async Task<bool> CompararDosHuellasAsync(string templateBase64Guardado, CancellationToken token = default)
        {
            if (!_initialized || _deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("El lector no está listo. Inicialice y abra el dispositivo.");

            if (string.IsNullOrWhiteSpace(templateBase64Guardado))
                throw new ArgumentException("El template guardado es inválido.");

            token.ThrowIfCancellationRequested();

            // 1. Template guardado en base64 => bytes
            byte[] templateGuardadoBytes = Convert.FromBase64String(templateBase64Guardado);

            // 2. Capturar huella en vivo
            _logger.LogInformation("Coloque el dedo en el lector para capturar la huella en vivo...");
            byte[] templateCapturadoBytes = await CapturarHuellaAsync(token);

            // 3. Inicializar base de datos ZK
            IntPtr db = zkfp2.DBInit();
            if (db == IntPtr.Zero)
                throw new Exception("No se pudo inicializar la base de datos de huellas.");

            // 4. Comparar huellas usando ZKTeco DBMatch
            int score = zkfp2.DBMatch(db, templateGuardadoBytes, templateCapturadoBytes);

            // 5. Liberar DB
            zkfp2.DBFree(db);

            bool coincide = score >= 0; // >=0 significa comparación exitosa

            _logger.LogInformation("Resultado comparación: coincide={Coincide}, score={Score}", coincide, score);

            return coincide;
        }


        #endregion

        #region Limpieza

        public void Dispose()
        {
            CerrarDispositivo();

            if (_initialized)
            {
                zkfp2.Terminate();
                _initialized = false;
            }

            GC.SuppressFinalize(this);
        }

        public bool Reiniciar(int indiceDispositivo = 0)
        {
            lock (_lock)
            {
                _logger.LogWarning("Reiniciando completamente el servicio de huellas...");

                try
                {
                    // 1. Cerrar dispositivo si está abierto
                    if (_deviceHandle != IntPtr.Zero)
                    {
                        _logger.LogInformation("Cerrando lector antes del reinicio...");
                        zkfp2.CloseDevice(_deviceHandle);
                        _deviceHandle = IntPtr.Zero;
                    }

                    // 2. Terminar SDK si estaba inicializado
                    if (_initialized)
                    {
                        _logger.LogInformation("Terminando SDK antes del reinicio...");
                        zkfp2.Terminate();
                        _initialized = false;
                    }

                    // 3. Inicializar SDK
                    _logger.LogInformation("Inicializando nuevamente el SDK...");
                    int ret = zkfp2.Init();

                    if (ret != zkfp.ZKFP_ERR_OK)
                    {
                        _logger.LogError("Error al reiniciar: Init falló con código {Ret}", ret);
                        return false;
                    }

                    _initialized = true;

                    // 4. Abrir dispositivo automáticamente (opcional)
                    _logger.LogInformation("Abriendo nuevamente el lector índice {Indice}...", indiceDispositivo);
                    _deviceHandle = zkfp2.OpenDevice(indiceDispositivo);

                    if (_deviceHandle == IntPtr.Zero)
                    {
                        _logger.LogError("El SDK se reinició pero no se pudo abrir el lector.");
                        return false;
                    }

                    _logger.LogInformation("Reinicio completo: SDK y lector funcionando correctamente.");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error grave durante el reinicio del servicio de huellas.");
                    return false;
                }
            }
        }


        #endregion
    }
}
