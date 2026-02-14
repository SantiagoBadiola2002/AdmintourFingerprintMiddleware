using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AdmintourFingerprintMiddleware.Services;
using AdmintourFingerprintMiddleware.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AdmintourFingerprintMiddleware.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HuellasController : ControllerBase
    {
        private readonly ILogger<HuellasController> _logger;
        private readonly FingerprintService _servicio;

        public HuellasController(
            ILogger<HuellasController> logger,
            FingerprintService servicio)
        {
            _logger = logger;
            _servicio = servicio;
        }


        // ----------------------------------------------------------------------
        // POST: api/huellas/inicializar
        // ----------------------------------------------------------------------
        [HttpPost("inicializar")]
        public IActionResult InicializarSDK()
        {
            try
            {
                bool ok = _servicio.Inicializar();

                if (!ok)
                    return StatusCode(500, new { error = "No se pudo inicializar el SDK de huellas." });

                return Ok(new { inicializado = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inicializar el SDK.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ----------------------------------------------------------------------
        // POST: api/huellas/abrir
        // ----------------------------------------------------------------------
        [HttpPost("abrir")]
        public IActionResult AbrirLector([FromQuery] int indice = 0)
        {
            try
            {
                bool ok = _servicio.AbrirDispositivo(indice);

                if (!ok)
                    return StatusCode(500, new { error = "No se pudo abrir el lector de huellas." });

                return Ok(new { lectorAbierto = true, indice });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al abrir el lector.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ----------------------------------------------------------------------
        // POST: api/huellas/cerrar
        // ----------------------------------------------------------------------
        [HttpPost("cerrar")]
        public IActionResult CerrarLector()
        {
            try
            {
                _servicio.CerrarDispositivo();
                return Ok(new { lectorCerrado = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar el lector.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ----------------------------------------------------------------------
        // GET: api/huellas/estado
        // ----------------------------------------------------------------------
        [HttpGet("estado")]
        public IActionResult ObtenerEstado()
        {
            try
            {
                int cantidad = _servicio.ObtenerCantidadDispositivos();

                return Ok(new
                {
                    sdkInicializado = true,
                    dispositivosDisponibles = cantidad
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consultando el estado general de huellas.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpPost("reiniciar")]
        public IActionResult ReiniciarServicio([FromQuery] int indice = 0)
        {
            try
            {
                bool ok = _servicio.Reiniciar(indice);

                if (!ok)
                    return StatusCode(500, new
                    {
                        reiniciado = false,
                        mensaje = "No se pudo reiniciar correctamente el servicio de huellas."
                    });

                return Ok(new
                {
                    reiniciado = true,
                    mensaje = "Servicio de huellas reiniciado correctamente.",
                    indice
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al reiniciar el servicio de huellas.");
                return StatusCode(500, new { reiniciado = false, mensaje = ex.Message });
            }
        }


        // ----------------------------------------------------------------------
        // GET: api/huellas/info-dispositivo
        // ----------------------------------------------------------------------
        [HttpGet("info-dispositivo")]
        public IActionResult ObtenerInformacionDispositivo()
        {
            try
            {
                DeviceInfo info = _servicio.GetDeviceInfo();
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener información del lector.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ----------------------------------------------------------------------
        // POST: api/huellas/capturar
        // Captura una sola huella (un template)
        // ----------------------------------------------------------------------
        [HttpPost("capturarUnica")]
        public async Task<IActionResult> CapturarHuella()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

                byte[] template = await _servicio.CapturarHuellaAsync(cts.Token);

                return Ok(new
                {
                    templateBase64 = Convert.ToBase64String(template),
                    tamaño = template.Length
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(408, new { error = "Tiempo de espera agotado. No se detectó ninguna huella." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al capturar la huella.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ----------------------------------------------------------------------
        // POST: api/huellas/enrolar
        // Captura 3 veces y fusiona template
        // ----------------------------------------------------------------------
        [HttpPost("capturar3Veces")]
        public async Task<IActionResult> EnrolarHuella()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

                string base64Template = await _servicio.CapturarHuella3VecesAsync(cts.Token);

                return Ok(new
                {
                    templateBase64 = base64Template,
                    tamaño = Convert.FromBase64String(base64Template).Length
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(408, new { error = "Tiempo de espera agotado durante el proceso de enrolamiento." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en el proceso de enrolamiento.");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ----------------------------------------------------------------------
        // POST: api/huellas/comparar
        // Captura huella 3 veces x2 y compara
        // ----------------------------------------------------------------------
        [HttpPost("compararDosHuellas")]
        public async Task<IActionResult> Comparar([FromBody] string templateBase64)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));

                bool coincide = await _servicio.CompararDosHuellasAsync(templateBase64, cts.Token);

                return Ok(new
                {
                    coincide,
                    mensaje = coincide ? "Las huellas coinciden." : "Las huellas no coinciden."
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(408, new { mensaje = "Tiempo agotado durante la comparación." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al comparar huellas.");
                return StatusCode(500, new { mensaje = ex.Message });
            }
        }


        [HttpPost("capturar-y-enviar")]
        public async Task<IActionResult> CapturarYEnviar(
    [FromServices] AdmintourApiClient apiClient)
        {
            try
            {
                string rutaArchivo = @"C:\config\hotel.txt";

                if (!System.IO.File.Exists(rutaArchivo))
                    return StatusCode(500, new { error = "Archivo de configuración de hotel no encontrado." });

                string hotelCodigo = (await System.IO.File.ReadAllTextAsync(rutaArchivo)).Trim();

                if (string.IsNullOrWhiteSpace(hotelCodigo))
                    return StatusCode(500, new { error = "El archivo de hotel está vacío o es inválido." });

                _logger.LogInformation("Iniciando proceso de captura y envío de huella para el hotel {Hotel}", hotelCodigo);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

                // Captura fusionada de 3 huellas
                string templateBase64 = await _servicio.CapturarHuella3VecesAsync(cts.Token);

                // Envío al API externo
                await apiClient.GrabarHuellaAsync(hotelCodigo, templateBase64);

                return Ok(new
                {
                    estado = "ok",
                    mensaje = "Huella capturada y enviada correctamente.",
                    hotel = hotelCodigo
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(408, new
                {
                    estado = "error",
                    mensaje = "Tiempo de espera agotado durante la captura."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al capturar o enviar la huella.");

                return StatusCode(500, new
                {
                    estado = "error",
                    mensaje = ex.Message
                });
            }
        }



    }
}
