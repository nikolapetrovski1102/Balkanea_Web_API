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

        [HttpPost("process-by-country")]
        public IActionResult ProcessDumpByCountry([FromBody] CountryCodeDTO extractDTO)
        {
            try
            {
                string hotel_dump_path = Path.Combine(absolutePath, $"ZSTDHotelDump\\{InputFileName}");

                if (!System.IO.File.Exists(hotel_dump_path))
                {
                    return NotFound("Input file not found");
                }

                var result = ProcessFileByCountry(hotel_dump_path, extractDTO);
                //var outputPath = WriteOutputFileWithStatistics(result.GroupedHotels, result.Statistics, extractDTO.countryCode);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error processing file: {ex.Message}");
            }
        }

        [HttpPost("process-by-region")]
        public IActionResult ProcessDumpByRegion([FromBody] ExtractDTO extractDTO)
        {
            try
            {
                string hotel_dump_path = Path.Combine(absolutePath, $"ZSTDHotelDump\\{InputFileName}");

                if (!System.IO.File.Exists(hotel_dump_path))
                {
                    return NotFound("Input file not found");
                }

                var result = ProcessFileByRegion(hotel_dump_path, extractDTO);
                //var outputPath = WriteOutputFileWithHotels(groupedHotels);

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error processing file: {ex.Message}");
            }
        }

        private List<string> ProcessFileByCountry(string filePath, CountryCodeDTO extractDTO)
        {
            var hotelList = new List<string>();

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
                            string cc = countryCode.GetString();

                            if (cc == extractDTO.countryCode)
                            {
                                // Get the entire JSON as a formatted string
                                var jsonString = JsonSerializer.Serialize(root, new JsonSerializerOptions
                                {
                                    WriteIndented = false
                                });

                                hotelList.Add(jsonString);
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

            return hotelList;
        }

        private List<string> ProcessFileByRegion(string filePath, ExtractDTO extractDTO)
        {
            var hotelList = new List<string>();

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
                            string cc = countryCode.GetString() ?? "";

                            if (rid == extractDTO.regionId && cc == extractDTO.countryCode)
                            {
                                // Get the entire JSON as a formatted string
                                var jsonString = JsonSerializer.Serialize(root, new JsonSerializerOptions
                                {
                                    WriteIndented = false
                                });

                                hotelList.Add(jsonString);
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

            return hotelList;
        }

        private string WriteOutputFileWithStatistics(Dictionary<int, List<string>> groupedHotels, HotelStatistics statistics, string countryCode)
        {
            Directory.CreateDirectory(OutputDirectory);
            var outputPath = Path.Combine(OutputDirectory, $"{DateTime.Today:dd-MM-yyyy}_country_{countryCode}.json");

            var output = new
            {
                Hotels = groupedHotels.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.Select(json => JsonDocument.Parse(json).RootElement).ToList()
                )
                //Statistics = new
                //{
                //    Country = countryCode,
                //    TotalRegions = statistics.TotalRegions,
                //    TotalHotels = statistics.TotalHotels,
                //    RegionCounts = statistics.RegionCounts
                //}
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            System.IO.File.WriteAllText(outputPath, JsonSerializer.Serialize(output, options));

            return outputPath;
        }

        private string WriteOutputFileWithHotels(Dictionary<int, List<string>> groupedHotels)
        {
            Directory.CreateDirectory(OutputDirectory);
            var outputPath = Path.Combine(OutputDirectory, $"{DateTime.Today:dd-MM-yyyy}_region.json");

            var finalOutput = new Dictionary<int, List<JsonElement>>();

            foreach (var (regionId, jsonStrings) in groupedHotels)
            {
                var hotels = jsonStrings
                    .Select(json => JsonDocument.Parse(json).RootElement)
                    .ToList();

                finalOutput[regionId] = hotels;
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            System.IO.File.WriteAllText(outputPath, JsonSerializer.Serialize(finalOutput, options));

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
