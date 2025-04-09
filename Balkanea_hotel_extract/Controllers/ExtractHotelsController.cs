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
    [Authorize(AuthenticationSchemes = "BasicAuthentication")]
    public class ExtractHotelsController : Controller
    {
        private const string InputFileName = "feed_en_v3 (3).json.zst";
        private const string OutputDirectory = "output";
        private string absolutePath = Directory.GetCurrentDirectory().ToString();

        [HttpPost("process")]
        public IActionResult ProcessDump([FromBody] ExtractDTO extractDTO)
        {
            try
            {

                string hotel_dump_path = Path.Combine(absolutePath, "ZSTDHotelDump\\feed_en_v3 (3).json.zst");

                if (!System.IO.File.Exists(hotel_dump_path))
                {
                    return NotFound("Input file not found");
                }

                var groupedHotels = ProcessFile(hotel_dump_path, extractDTO);
                var outputPath = WriteOutputFile(groupedHotels);

                return Ok(new
                {
                    Message = "Processing completed successfully",
                    OutputFile = groupedHotels
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error processing file: {ex.Message}");
            }
        }
        private Dictionary<int, List<string>> ProcessFile(string filePath, ExtractDTO extractDTO)
        {
            var groupedHotels = new Dictionary<int, List<string>>();

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

                                if (!groupedHotels.TryGetValue(rid, out var hotelList))
                                {
                                    hotelList = new List<string>();
                                    groupedHotels[rid] = hotelList;
                                }
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

            return groupedHotels;
        }

        private string WriteOutputFile(Dictionary<int, List<string>> groupedHotels)
        {
            Directory.CreateDirectory(OutputDirectory);
            var outputPath = Path.Combine(OutputDirectory, $"{DateTime.Today:dd-MM-yyyy}.json");
            
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
}
