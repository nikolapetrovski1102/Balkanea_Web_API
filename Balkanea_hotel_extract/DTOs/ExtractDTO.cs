using System.Text.Json.Serialization;

namespace Balkanea_hotel_extract.DTOs
{
    public class ExtractDTO
    {
        [JsonPropertyName("region_id")]
        public List<int> regionId { get; set; } = new List<int>();
        [JsonPropertyName("country_code")]
        public string countryCode { get; set; }
    }
}
