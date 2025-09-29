using Microsoft.AspNetCore.Mvc;
using AdmintourFingerprintMiddleware.Models;
using libzkfpcsharp;

namespace AdmintourFingerprintMiddleware.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class FingerprintController : ControllerBase
    {
        // Simulación de registro de huella
        [HttpPost("registrar")]
        public IActionResult RegistrarHuella([FromBody] RegistroHuellaRequest request)
        {
            // Acá más adelante llamás al SDK de ZKTeco para capturar la huella
            var fakeFingerprintTemplate = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

            return Ok(new
            {
                Success = true,
                Message = "Huella registrada (mock).",
                ReservationId = request.IdReserva,
                FingerprintTemplate = fakeFingerprintTemplate
            });
        }

        // Simulación de verificación de huella
        [HttpPost("verificar")]
        public IActionResult VerificarHuella([FromBody] FingerprintVerifyRequest request)
        {
            // Simulación: cualquier huella que termine en 'A' es válida
            bool isValid = request.FingerprintData.EndsWith("A");

            return Ok(new
            {
                Success = isValid,
                Message = isValid ? "Huella válida." : "Huella no reconocida.",
                ReservationId = request.ReservationId
            });
        }
    }
}
