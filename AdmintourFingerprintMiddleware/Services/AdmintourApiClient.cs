using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using AdmintourFingerprintMiddleware.Models;

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

            _logger.LogInformation("Body que se enviará al servidor:\n{Json}", json);
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

        // ===========================
        // NUEVO: OBTENER HUELLAS
        // ===========================

        public class HuellasResponse
        {
            public string? ErrorHuella { get; set; }
            public List<HuellaItem>? ColeccionHuellas { get; set; }
        }

        public class HuellaItem
        {
            public string? HotelHuellaCodigo { get; set; }
            public int HotelHuellaUsuarioId { get; set; }
            public string? HotelHuellaUsuNombre { get; set; }
            public string? HotelHuellaUsuApellido { get; set; }
            public string? HotelHuellaDigital { get; set; }
        }

        public async Task<HuellasResponse> ObtenerHuellasAsync(int hotcod)
        {
            var url = $"https://gx18.admintour.com/admintour/API_ChannelAdmintour/Huellas?hotcod={hotcod}";

            _logger.LogInformation("Consultando huellas en Admintour: {Url}", url);

            var resp = await _httpClient.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();

            _logger.LogInformation("Respuesta Huellas: {Body}", body);

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Error llamando Huellas: HTTP {(int)resp.StatusCode} - {body}");

            var data = JsonSerializer.Deserialize<HuellasResponse>(body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (data == null)
                throw new Exception("No se pudo deserializar la respuesta de Huellas.");

            data.ColeccionHuellas ??= new List<HuellaItem>();
            return data;
        }


        // En AdmintourApiClient.cs
        public async Task<(bool success, string url)> LoginAsync(string hotelCodigoStr, string usuarioIdStr)
        {
            try
            {
                var config = await ConfigHuellas.CargarAsync();

                if (!int.TryParse(hotelCodigoStr, out int hotcodInt) ||
                    !int.TryParse(usuarioIdStr, out int usuidInt))
                {
                    throw new Exception("Los parámetros recibidos no son numéricos válidos.");
                }

                // 1. Aplicamos la lógica de sumar 2026
                int hotcodFinal = hotcodInt + 2026;
                int usuidFinal = usuidInt + 2026;

                // 2. Limpiar la URL base de posibles signos de interrogación al final
                string urlBase = config.Url.Trim().Split('?')[0];

                // 3. Construir la URL con el formato exacto: aspx?valor1,valor2
                string urlFinal = $"{urlBase}?{hotcodFinal},{usuidFinal}";

                _logger.LogInformation("URL de redirección generada: {Url}", urlFinal);

                return (true, urlFinal);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error crítico en LoginAsync");
                return (false, string.Empty);
            }
        }

    }
}
