namespace AdmintourFingerprintMiddleware.Models
{
    public class FingerCompareRequest
    {
        public string Template1 { get; set; }
        public string Template2 { get; set; }
        public int? Umbral { get; set; }
    }
}
