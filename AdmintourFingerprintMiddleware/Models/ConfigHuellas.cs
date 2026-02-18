using System.Text.Json;

namespace AdmintourFingerprintMiddleware.Models
{
    public class ConfigHuellas
    {
        private static string Ruta => @"C:\admintour\configAdmintourHuellas.txt";

        public int HotelCodigo { get; set; }
        public string ApiUrl { get; set; } = "";

        public static async Task<ConfigHuellas> CargarAsync()
        {
            if (!File.Exists(Ruta))
                throw new Exception("Archivo de configuración no encontrado.");

            var json = await File.ReadAllTextAsync(Ruta);
            var config = System.Text.Json.JsonSerializer.Deserialize<ConfigHuellas>(json);

            if (config == null)
                throw new Exception("Archivo de configuración inválido.");

            return config;
        }
    }

}
