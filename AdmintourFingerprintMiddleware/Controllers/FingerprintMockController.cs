using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using AdmintourFingerprintMiddleware.Models;
using AdmintourFingerprintMiddleware.Services;

namespace AdmintourFingerprintMiddleware.Controllers
{
    [ApiController]
    [Route("mock/huella")]
    public class FingerprintMockController : ControllerBase
    {
        private readonly FingerprintMockService _servicioHuella;
        private readonly ILogger<FingerprintMockController> _logger;

        public FingerprintMockController(FingerprintMockService servicioHuella, ILogger<FingerprintMockController> logger)
        {
            _servicioHuella = servicioHuella;
            _logger = logger;
        }

        [HttpPost("InicializarYAbrir/{index?}")]
        public IActionResult InicializarYAbrir(int index = 0)
        {
            try
            {
                bool result = _servicioHuella.InicializarYAbrir(index);
                return Ok(new { Success = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inicializando y abriendo dispositivo");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("ObtenerCantidadDispositivos")]
        public IActionResult ObtenerCantidadDispositivos()
        {
            try
            {
                int count = _servicioHuella.ObtenerCantidadDispositivos();
                return Ok(new { DeviceCount = count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo cantidad de dispositivos");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("AbrirDispositivo/{index?}")]
        public IActionResult AbrirDispositivo(int index = 0)
        {
            try
            {
                bool result = _servicioHuella.AbrirDispositivo(index);
                return Ok(new { DeviceOpened = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error abriendo dispositivo");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("capturar-huella")]
        public IActionResult CapturarHuella()
        {
            try
            {
                _logger.LogInformation("Controller: Esperando que el usuario coloque el dedo en el sensor...");

                // Captura el template en binario
                byte[] templateBinario = _servicioHuella.CapturarHuellaEsperando();

                // Convertir a Base64 para enviar por JSON
                string base64Template = Convert.ToBase64String(templateBinario);

                return Ok(new
                {
                    Status = "ok",
                    Template = base64Template
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Controller: Error capturando huella");
                return StatusCode(500, new
                {
                    Status = "error",
                    Message = ex.Message
                });
            }
        }

        [HttpGet("capturar-huella-3")]
        public IActionResult CapturarHuella3Veces()
        {
            try
            {
                _logger.LogInformation("Controller: Iniciando captura de huella 3 veces para generar un template confiable...");

                // Llamamos al servicio que captura 3 veces y fusiona
                string base64TemplateFinal = _servicioHuella.CapturarHuella3Veces();

                return Ok(new
                {
                    Status = "ok",
                    Template = base64TemplateFinal
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Controller: Error capturando huella 3 veces");
                return StatusCode(500, new
                {
                    Status = "error",
                    Message = ex.Message
                });
            }
        }




        [HttpPost("CerrarDispositivo")]
        public IActionResult CerrarDispositivo()
        {
            try
            {
                _servicioHuella.CerrarDispositivo();
                return Ok(new { Closed = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cerrando dispositivo");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("device-info")]
        public IActionResult GetDeviceInfo()
        {
            try
            {
                var info = _servicioHuella.GetDeviceInfo();
                return Ok(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo información del dispositivo");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpPost("ConvertirBase64ATemplate")]
        public IActionResult ConvertirBase64ATemplate([FromBody] string base64Template)
        {
            try
            {
                byte[] template = _servicioHuella.ConvertirBase64ATemplate(base64Template);
                return Ok(new { Length = template.Length });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error convirtiendo Base64 a template");
                return StatusCode(500, ex.Message);
            }
        }

        [HttpGet("comparar")]
        public IActionResult CompararHuellas()
        {
            try
            {
                _logger.LogInformation("Iniciando comparación de dos huellas...");
                var resultado = _servicioHuella.CompararDosHuellas();

                return Ok(new
                {
                    Status = "ok",
                    Match = resultado.Match,
                    Score = resultado.Score
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparando huellas");
                return StatusCode(500, new
                {
                    Status = "error",
                    Message = ex.Message
                });
            }
        }



    }
}
