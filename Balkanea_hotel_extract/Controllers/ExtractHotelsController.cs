using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Balkanea_hotel_extract.DTOs;

namespace Balkanea_hotel_extract.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize(AuthenticationSchemes = "BasicAuthentication")]
    public class ExtractHotelsController : Controller
    {
        private const string InputFileName = "feed_en_v3.json.zst";
        private const string OutputDirectory = "output";
        private string absolutePath = Directory.GetCurrentDirectory().ToString();

        [HttpPost("process")]
        public IActionResult ProcessDump([FromBody] ExtractDTO extractDTO)
        {
            try
            {
                string hotel_dump_path = Path.Combine(absolutePath, $"ZSTDHotelDump\\{InputFileName}");

                if (!System.IO.File.Exists(hotel_dump_path))
                {
                    return NotFound("Input file not found");
                }

                var result = ProcessFile(hotel_dump_path, extractDTO);
                var outputPath = WriteOutputFile(result.GroupedHotels, result.Statistics, extractDTO.countryCode);

                return Ok(new
                {
                    Message = "Processing completed successfully",
                    Statistics = result.Statistics
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error processing file: {ex.Message}");
            }
        }

        private (Dictionary<int, List<string>> GroupedHotels, HotelStatistics Statistics) ProcessFile(string filePath, ExtractDTO extractDTO)
        {
            var groupedHotels = new Dictionary<int, List<string>>();
            var statistics = new HotelStatistics();

            using (var fileStream = System.IO.File.OpenRead(filePath))
            using (var decompressor = new ZstdNet.DecompressionStream(fileStream))
            using (var reader = new StreamReader(decompressor, Encoding.UTF8))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        using JsonDocument document = JsonDocument.Parse(line);
                        JsonElement root = document.RootElement;

                        if (root.TryGetProperty("region", out JsonElement region) &&
                            region.TryGetProperty("id", out JsonElement regionId) &&
                            region.TryGetProperty("country_code", out JsonElement countryCode))
                        {
                            int rid = regionId.GetInt32();
                            string cc = countryCode.GetString() ?? "GR";

                            if (cc == extractDTO.countryCode)
                            {
                                // Get the entire JSON as a formatted string
                                var jsonString = JsonSerializer.Serialize(root, new JsonSerializerOptions
                                {
                                    WriteIndented = false
                                });

                                if (!groupedHotels.TryGetValue(rid, out var hotelList))
                                {
                                    hotelList = new List<string>();
                                    groupedHotels[rid] = hotelList;
                                    statistics.RegionCounts[rid] = 0;
                                }
                                hotelList.Add(jsonString);
                                statistics.RegionCounts[rid]++;
                                statistics.TotalHotels++;
                            }
                        }
                    }
                    catch (JsonException)
                    {
                        Console.WriteLine($"Error decoding JSON line: {line}");
                    }
                    catch (InvalidOperationException ex)
                    {
                        Console.WriteLine($"Invalid data format: {ex.Message}");
                    }
                }
            }

            statistics.TotalRegions = groupedHotels.Count;
            return (groupedHotels, statistics);
        }

        private string WriteOutputFile(Dictionary<int, List<string>> groupedHotels, HotelStatistics statistics, string countryCode)
        {
            Directory.CreateDirectory(OutputDirectory);
            var outputPath = Path.Combine(OutputDirectory, $"{DateTime.Today:dd-MM-yyyy}.json");

            var output = new
            {
                Statistics = new
                {
                    Coutry = countryCode,
                    TotalRegions = statistics.TotalRegions,
                    TotalHotels = statistics.TotalHotels,
                    RegionCounts = statistics.RegionCounts
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            System.IO.File.WriteAllText(outputPath, JsonSerializer.Serialize(output, options));

            return outputPath;
        }
    }

    public class HotelStatistics
    {
        public int TotalRegions { get; set; } = 0;
        public int TotalHotels { get; set; } = 0;
        public Dictionary<int, int> RegionCounts { get; set; } = new Dictionary<int, int>();
    }
}
