using System.Text.Json.Serialization;

namespace PaymentService.DTO
{
    public class BkashTokenResponse
    {
        [JsonPropertyName("id_token")]
        public string IdToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("statusCode")]
        public string StatusCode { get; set; }

        [JsonPropertyName("statusMessage")]
        public string StatusMessage { get; set; }
    }
}
