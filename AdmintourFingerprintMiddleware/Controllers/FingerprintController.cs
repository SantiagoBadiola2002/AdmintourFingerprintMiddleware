using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using AdmintourFingerprintMiddleware.Services;
using AdmintourFingerprintMiddleware.Models;
using System.Text.Json;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Collections.Generic;

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

                return Ok(new { status = "ok", mensaje = "SDK inicializado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inicializar SDK.");
                return StatusCode(500, new { error = ex.Message });
            }
        }


        // ----------------------------------------------------------------------
        // POST: api/huellas/abrir?indice=0
        // ----------------------------------------------------------------------
        [HttpPost("abrir")]
        public IActionResult AbrirLector([FromQuery] int indice = 0)
        {
            try
            {
                bool ok = _servicio.AbrirDispositivo(indice);

                if (!ok)
                    return StatusCode(500, new { error = "No se pudo abrir el lector." });

                return Ok(new { status = "ok", mensaje = "Lector abierto correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al abrir lector.");
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
                return Ok(new { status = "ok", mensaje = "Lector cerrado correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al cerrar lector.");
                return StatusCode(500, new { error = ex.Message });
            }
        }


        // ----------------------------------------------------------------------
        // POST: api/huellas/capturarUnica
        // ----------------------------------------------------------------------
        [HttpPost("capturarUnica")]
        public async Task<IActionResult> CapturarUnica()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var templateBase64 = await _servicio.CapturarUnicaAsync(cts.Token);

                return Ok(new
                {
                    status = "ok",
                    templateBase64
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(408, new { error = "Tiempo agotado durante la captura." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al capturar huella única.");
                return StatusCode(500, new { error = ex.Message });
            }
        }


        // ----------------------------------------------------------------------
        // POST: api/huellas/capturar3Veces
        // ----------------------------------------------------------------------
        [HttpPost("capturar3Veces")]
        public async Task<IActionResult> Capturar3Veces()
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var templateBase64 = await _servicio.CapturarHuella3VecesAsync(cts.Token);

                return Ok(new
                {
                    status = "ok",
                    templateBase64
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(408, new { error = "Tiempo agotado durante la captura." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al capturar huella 3 veces.");
                return StatusCode(500, new { error = ex.Message });
            }
        }


        // ----------------------------------------------------------------------
        // POST: api/huellas/compararDosHuellas
        // body: { templateGuardadoBase64: "..." }
        // Compara template guardado vs huella capturada en el momento
        // ----------------------------------------------------------------------
        [HttpPost("compararDosHuellas")]
        public async Task<IActionResult> Comparar([FromBody] string templateBase64)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

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

        // GET: api/huellas/hotel
        [HttpGet("hotel")]
        public async Task<IActionResult> ObtenerHotel()
        {
            try
            {
                var config = await ConfigHuellas.CargarAsync();

                return Ok(new
                {
                    hotelCodigo = config.HotelCodigo
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo hotel");
                return StatusCode(500, new { error = ex.Message });
            }
        }



        // ----------------------------------------------------------------------
        // POST: api/huellas/capturar-y-enviar
        // Captura 3 veces, fusiona, lee hotel desde C:\admintour\configAdmintourHuellas.txt y envía a Admintour
        // ----------------------------------------------------------------------
        [HttpPost("capturar-y-enviar")]
        public async Task<IActionResult> CapturarYEnviar([FromServices] AdmintourApiClient apiClient)
        {
            try
            {
                var config = await ConfigHuellas.CargarAsync();
                int hotelCodigo = config.HotelCodigo;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var templateBase64 = await _servicio.CapturarHuella3VecesAsync(cts.Token);

                await apiClient.GrabarHuellaAsync(hotelCodigo.ToString(), templateBase64);

                return Ok(new
                {
                    estado = "ok",
                    mensaje = "Huella capturada y enviada correctamente.",
                    hotel = hotelCodigo
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(408, new { estado = "error", mensaje = "Tiempo de espera agotado durante la captura." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al capturar o enviar la huella.");
                return StatusCode(500, new { estado = "error", mensaje = ex.Message });
            }
            finally
            {
                _servicio.CerrarDispositivo();
            }
        }




        // ----------------------------------------------------------------------
        // POST: api/huellas/validar-en-lista-admintour
        // Lee hotcod de C:\admintour\configAdmintourHuellas.txt, trae la lista de Huellas desde Admintour,
        // captura UNA sola vez y compara contra toda la lista.
        // Devuelve ok + nombre + apellido + hotelCodigo
        // ----------------------------------------------------------------------
        [HttpPost("validar-en-lista-admintour")]
        public async Task<IActionResult> ValidarEnListaAdmintour([FromServices] AdmintourApiClient apiClient)
        {
            try
            {
                var config = await ConfigHuellas.CargarAsync();
                int hotcod = config.HotelCodigo;

                _logger.LogInformation("Validación de huella. Hotcod={Hotcod}", hotcod);

                var huellasResp = await apiClient.ObtenerHuellasAsync(hotcod);
                var lista = huellasResp.ColeccionHuellas ?? new List<AdmintourApiClient.HuellaItem>();

                if (lista.Count == 0)
                    return Ok(new { ok = false, hotelCodigo = hotcod, mensaje = "No se encontraron huellas." });

                var templates = lista
                    .Where(x => !string.IsNullOrWhiteSpace(x.HotelHuellaDigital))
                    .Select(x => x.HotelHuellaDigital!)
                    .ToArray();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var match = await _servicio.BuscarCoincidenciaEnListaAsync(templates, cts.Token);

                if (!match.Coincide || match.Indice < 0 || match.Indice >= lista.Count)
                    return Ok(new { ok = false, hotelCodigo = hotcod, mensaje = "No coincide con ninguna huella." });

                var usuario = lista[match.Indice];

                return Ok(new
                {
                    ok = true,
                    hotelCodigo = usuario.HotelHuellaCodigo ?? hotcod.ToString(),
                    nombre = usuario.HotelHuellaUsuNombre ?? "",
                    apellido = usuario.HotelHuellaUsuApellido ?? "",
                    hotelHuellaUsuarioId = usuario.HotelHuellaUsuarioId
                });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(408, new { ok = false, mensaje = "Tiempo agotado durante la captura/comparación." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en ValidarEnListaAdmintour");
                return StatusCode(500, new { ok = false, mensaje = ex.Message });
            }
            finally
            {
                _servicio.CerrarDispositivo();
            }
        }

        // ----------------------------------------------------------------------
        // POST: api/huellas/login-admintour
        // Realiza el login usando los datos obtenidos de ValidarEnListaAdmintour
        // ----------------------------------------------------------------------
        [HttpPost("login-admintour")]
        public async Task<IActionResult> LoginAdmintour([FromBody] LoginRequest request, [FromServices] AdmintourApiClient apiClient)
        {
            if (string.IsNullOrEmpty(request.HotelCodigo) || string.IsNullOrEmpty(request.UsuarioId))
            {
                return BadRequest(new { ok = false, mensaje = "Datos incompletos." });
            }

            var result = await apiClient.LoginAsync(request.HotelCodigo, request.UsuarioId);

            if (result.success)
            {
                // Enviamos la URL calculada al frontend
                return Ok(new { ok = true, url = result.url, mensaje = "Login exitoso." });
            }

            return StatusCode(500, new { ok = false, mensaje = "Error al generar link de acceso." });
        }



    }
}
