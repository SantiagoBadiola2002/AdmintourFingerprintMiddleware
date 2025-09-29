namespace AdmintourFingerprintMiddleware.Models
{
    public class FingerprintVerifyRequest
    {
        public string ReservationId { get; set; }
        public string FingerprintData { get; set; } // base64
    }
}
