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

        private bool _initialized = false;
        private IntPtr _deviceHandle = IntPtr.Zero;
        private IntPtr _dbHandle = IntPtr.Zero;

        private const int TEMPLATE_SIZE = 2048;

        public FingerprintService(ILogger<FingerprintService> logger)
        {
            _logger = logger;
        }

        // =====================================================
        // Inicialización y apertura del lector
        // =====================================================

        public bool Inicializar()
        {
            try
            {
                if (_initialized) return true;

                int ret = zkfp2.Init();
                if (ret != 0)
                {
                    _logger.LogError("Error al inicializar el SDK. Código: {Ret}", ret);
                    return false;
                }

                _initialized = true;

                // Inicializar DB para operaciones de huellas
                _dbHandle = zkfp2.DBInit();
                if (_dbHandle == IntPtr.Zero)
                {
                    _logger.LogError("No se pudo inicializar la base de datos de huellas.");
                    return false;
                }

                _logger.LogInformation("SDK y DB inicializados correctamente.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al inicializar SDK.");
                return false;
            }
        }

        public bool AbrirDispositivo(int indice)
        {
            try
            {
                if (!_initialized)
                    throw new InvalidOperationException("Debe inicializar el SDK antes de abrir el dispositivo.");

                if (_deviceHandle != IntPtr.Zero) return true;

                _deviceHandle = zkfp2.OpenDevice(indice);
                if (_deviceHandle == IntPtr.Zero)
                {
                    _logger.LogError("No se pudo abrir el dispositivo. Índice: {Indice}", indice);
                    return false;
                }

                _logger.LogInformation("Dispositivo abierto correctamente. Índice: {Indice}", indice);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción al abrir dispositivo.");
                return false;
            }
        }

        public void CerrarDispositivo()
        {
            try
            {
                if (_deviceHandle != IntPtr.Zero)
                {
                    zkfp2.CloseDevice(_deviceHandle);
                    _deviceHandle = IntPtr.Zero;
                    _logger.LogInformation("Dispositivo cerrado.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar dispositivo.");
            }
        }

        // =====================================================
        // Captura de huellas
        // =====================================================

        public async Task<string> CapturarUnicaAsync(CancellationToken token)
        {
            byte[] template = await CapturarHuellaAsync(token);
            return Convert.ToBase64String(template);
        }

        public async Task<string> Capturar3VecesAsync(CancellationToken token)
        {
            _logger.LogInformation("Iniciando captura 3 veces...");

            byte[] t1 = await CapturarHuellaAsync(token);
            byte[] t2 = await CapturarHuellaAsync(token);
            byte[] t3 = await CapturarHuellaAsync(token);

            // En tu SDK, DBMerge devuelve el template merged dentro de regTemp y el largo en regTempLen
            byte[] regTemp = new byte[TEMPLATE_SIZE];
            int regTempLen = regTemp.Length;

            int ret = zkfp2.DBMerge(_dbHandle, t1, t2, t3, regTemp, ref regTempLen);
            if (ret != 0)
                throw new Exception($"Error al fusionar huellas. Código: {ret}");

            // recortar al tamaño real
            byte[] finalTemplate = new byte[regTempLen];
            Array.Copy(regTemp, finalTemplate, regTempLen);

            return Convert.ToBase64String(finalTemplate);
        }



        private async Task<byte[]> CapturarHuellaAsync(CancellationToken token)
        {
            if (!_initialized || _deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("El lector no está listo. Inicialice y abra el dispositivo.");

            token.ThrowIfCancellationRequested();

            var template = new byte[TEMPLATE_SIZE];
            var img = new byte[256 * 360]; // tamaño típico, depende del dispositivo
            int templateLen = TEMPLATE_SIZE;

            _logger.LogInformation("Esperando dedo...");

            // Intentar por 60 segundos
            var start = DateTime.UtcNow;
            while ((DateTime.UtcNow - start).TotalSeconds < 60)
            {
                token.ThrowIfCancellationRequested();

                int ret = zkfp2.AcquireFingerprint(_deviceHandle, img, template, ref templateLen);
                if (ret == 0 && templateLen > 0)
                {
                    _logger.LogInformation("Huella capturada. Len: {Len}", templateLen);

                    // recortar al tamaño real
                    byte[] finalTemplate = new byte[templateLen];
                    Array.Copy(template, finalTemplate, templateLen);
                    return finalTemplate;
                }

                await Task.Delay(200, token);
            }

            throw new TimeoutException("Tiempo de espera agotado para capturar huella.");
        }

        // =====================================================
        // Comparación
        // =====================================================

        /// <summary>
        /// Compara una huella guardada (base64) con una huella capturada en el momento.
        /// </summary>
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

        // ----------------------------------------------------------------------
        // MATCH: Captura UNA sola vez y compara contra una lista de templates (Base64)
        // ----------------------------------------------------------------------
        public class MatchListaResult
        {
            public bool Coincide { get; set; }
            public int Indice { get; set; } = -1;
            public int Score { get; set; } = -1;
            public int Total { get; set; }
        }

        public async Task<MatchListaResult> BuscarCoincidenciaEnListaAsync(
            string[] templatesBase64Guardados,
            CancellationToken token = default)
        {
            if (!_initialized || _deviceHandle == IntPtr.Zero)
                throw new InvalidOperationException("El lector no está listo. Inicialice y abra el dispositivo.");

            if (templatesBase64Guardados == null || templatesBase64Guardados.Length == 0)
                throw new ArgumentException("La lista de templates está vacía.");

            token.ThrowIfCancellationRequested();

            // 1) Capturar UNA sola huella en vivo
            _logger.LogInformation("Coloque el dedo en el lector para capturar la huella en vivo (1 sola vez)...");
            byte[] templateCapturadoBytes = await CapturarHuellaAsync(token);

            // 2) Inicializar DB una vez (local)
            IntPtr db = zkfp2.DBInit();
            if (db == IntPtr.Zero)
                throw new Exception("No se pudo inicializar la base de datos de huellas.");

            try
            {
                int bestScore = -1;
                int bestIndex = -1;

                for (int i = 0; i < templatesBase64Guardados.Length; i++)
                {
                    token.ThrowIfCancellationRequested();

                    var t64 = templatesBase64Guardados[i];
                    if (string.IsNullOrWhiteSpace(t64))
                        continue;

                    byte[] guardadoBytes;
                    try
                    {
                        guardadoBytes = Convert.FromBase64String(t64);
                    }
                    catch
                    {
                        _logger.LogWarning("Template inválido en índice {Index}. Se omite.", i);
                        continue;
                    }

                    int score = zkfp2.DBMatch(db, guardadoBytes, templateCapturadoBytes);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestIndex = i;
                    }
                }

                // Mantenemos el mismo criterio que ya usás: match si score >= 0
                bool coincide = bestScore >= 0;

                _logger.LogInformation("Búsqueda en lista: coincide={Coincide}, bestIndex={Index}, bestScore={Score}",
                    coincide, bestIndex, bestScore);

                return new MatchListaResult
                {
                    Coincide = coincide,
                    Indice = coincide ? bestIndex : -1,
                    Score = bestScore,
                    Total = templatesBase64Guardados.Length
                };
            }
            finally
            {
                zkfp2.DBFree(db);
            }
        }

        // =====================================================
        // Limpieza / Reinicio
        // =====================================================

        public void Dispose()
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

                _logger.LogInformation("FingerprintService liberado correctamente.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al liberar FingerprintService.");
            }
        }

        public bool ReiniciarServicio()
        {
            lock (this)
            {
                try
                {
                    _logger.LogWarning("Reiniciando servicio de huellas...");

                    // 1) Cerrar el lector si está abierto
                    CerrarDispositivo();

                    // 2) Terminar SDK
                    if (_initialized)
                    {
                        zkfp2.Terminate();
                        _initialized = false;
                    }

                    // 3) Volver a inicializar SDK y DB
                    bool okInit = Inicializar();
                    if (!okInit)
                    {
                        _logger.LogError("No se pudo reinicializar el SDK.");
                        return false;
                    }

                    // 4) Reabrir lector (índice 0 por defecto)
                    bool okOpen = AbrirDispositivo(0);
                    if (!okOpen)
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
    }
}
