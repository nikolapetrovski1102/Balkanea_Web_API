using System.Text.Json.Serialization;

namespace Balkanea_hotel_extract.DTOs
{
    public class CountryCodeDTO
    {
        [JsonPropertyName("country_code")]
        public string countryCode { get; set; }
    }
}
