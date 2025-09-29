namespace AdmintourFingerprintMiddleware.Models
{
    /// <summary>
    /// Base de datos simulada en memoria (se borra al reiniciar la app).
    /// </summary>
    public static class BaseDeDatosSimulada
    {
        // Huellas definitivas ya registradas (IdReserva → TemplateBase64)
        public static Dictionary<string, string> HuespedesRegistrados { get; set; } = new Dictionary<string, string>();

        // Huellas temporales durante el registro paso a paso (IdReserva → lista de templates)
        public static Dictionary<string, List<string>> HuellasTemporales { get; set; } = new Dictionary<string, List<string>>();
    }
}