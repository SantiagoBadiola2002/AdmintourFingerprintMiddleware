using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AdmintourFingerprintMiddleware.Services
{
    public class AdmintourApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AdmintourApiClient> _logger;

        public AdmintourApiClient(HttpClient httpClient, ILogger<AdmintourApiClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <summary>
        /// Envía la huella digital capturada al servidor Admintour.
        /// </summary>
        /// <param name="hotelCodigo">Código del hotel (hotcod)</param>
        /// <param name="huellaBase64">Template de huella en Base64</param>
        public async Task GrabarHuellaAsync(string hotelCodigo, string huellaBase64)
        {
            var url = "https://gx18.admintour.com/admintour/API_ChannelAdmintour/Grabo_HuellaDigital";

            var body = new
            {
                hotcod = int.Parse(hotelCodigo),
                huella = huellaBase64,
                clientehuella = 0,
                usuariohuella = 0
            };

            var json = JsonSerializer.Serialize(body, new JsonSerializerOptions { WriteIndented = true });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 🔍 Mostrar el body en consola o en el log
            _logger.LogInformation("Body que se enviará al servidor:\n{Json}", json);
            // También podés usar Console.WriteLine(json); si querés verlo sin logs

            _logger.LogInformation("Enviando huella digital al servidor Admintour ({Hotel})...", hotelCodigo);

            try
            {
                var response = await _httpClient.PostAsync(url, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("Respuesta del servidor Admintour: {Response}", responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error BadRequest: {responseContent}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar huella al servidor Admintour");
                throw;
            }
        }

    }
}
