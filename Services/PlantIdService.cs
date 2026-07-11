using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlantScan.Models;

namespace PlantScan.Services;

public class PlantIdService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;

    public PlantIdService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _apiKey = configuration["PlantApi:ApiKey"] ?? string.Empty;
    }

    public async Task<PlantResult?> IdentifyPlantAsync(IFormFile image)
    {
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        return await IdentifyFromBase64Internal(base64);
    }

    public async Task<PlantResult?> IdentifyPlantFromBase64Async(string base64DataUrl)
    {
        var base64 = base64DataUrl.Contains(",")
            ? base64DataUrl.Split(',')[1]
            : base64DataUrl;
        return await IdentifyFromBase64Internal(base64);
    }

    private async Task<PlantResult?> IdentifyFromBase64Internal(string base64)
    {
        try
        {
            // STEP 1 — Identify the plant
            var client1 = _httpClientFactory.CreateClient();
            client1.DefaultRequestHeaders.Add("Api-Key", _apiKey);

            var payload = new
            {
                images = new[] { base64 },
                similar_images = true
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client1.PostAsync(
                "https://api.plant.id/v3/identification", content);

            var responseBody = await response.Content.ReadAsStringAsync();
            

            if (!response.IsSuccessStatusCode) return null;

            var parsed = JObject.Parse(responseBody);
            var suggestion = parsed["result"]?["classification"]?["suggestions"]?[0];
            if (suggestion == null) return null;

            var scientificName = suggestion["name"]?.ToString() ?? "Unknown Plant";
            var probability = suggestion["probability"]?.Value<double>() ?? 0.0;
            var imageUrl = suggestion["similar_images"]?[0]?["url"]?.ToString() ?? string.Empty;

            // STEP 2 — Search knowledge base for entity token
            var commonName = string.Empty;
            var description = string.Empty;
            var uses = string.Empty;
            var warnings = string.Empty;

            var client2 = _httpClientFactory.CreateClient();
            client2.DefaultRequestHeaders.Add("Api-Key", _apiKey);

            var encodedName = Uri.EscapeDataString(scientificName);
            var searchUrl = $"https://api.plant.id/v3/kb/plants/name_search?q={encodedName}&language=en";
            var searchResponse = await client2.GetAsync(searchUrl);
            var searchBody = await searchResponse.Content.ReadAsStringAsync();
            
            if (searchResponse.IsSuccessStatusCode)
            {
                var searchParsed = JObject.Parse(searchBody);
                var entityToken = searchParsed["entities"]?[0]?["access_token"]?.ToString();
                

                if (!string.IsNullOrEmpty(entityToken))
                {
                    // STEP 3 — Fetch full plant details
                    var client3 = _httpClientFactory.CreateClient();
                    client3.DefaultRequestHeaders.Add("Api-Key", _apiKey);

                    var detailsUrl = $"https://api.plant.id/v3/kb/plants/{entityToken}?details=common_names,description,edible_parts,toxicity,image&language=en";
                    var detailsResponse = await client3.GetAsync(detailsUrl);
                    var detailsBody = await detailsResponse.Content.ReadAsStringAsync();
                    

                    if (detailsResponse.IsSuccessStatusCode)
                    {
                        var details = JObject.Parse(detailsBody);

                        try
                        {
                            var commonNamesToken = details["common_names"];
                            if (commonNamesToken != null && commonNamesToken.HasValues)
                                commonName = commonNamesToken[0]?.ToString() ?? string.Empty;
                        }
                        catch { }

                        try
                        {
                            description = details["description"]?["value"]?.ToString() ?? string.Empty;
                            if (description.Length > 600)
                                description = description.Substring(0, 600).TrimEnd() + "...";
                        }
                        catch { }

                        try
                        {
                            var usesToken = details["edible_parts"];
                            if (usesToken != null && usesToken.HasValues)
                                uses = string.Join(", ", usesToken.Select(u => u.ToString()));
                        }
                        catch { }

                        try
                        {
                            var toxicity = details["toxicity"];
                            if (toxicity != null && toxicity.Type != JTokenType.Null)
                            {
                                if (toxicity.Type == JTokenType.String)
                                    warnings = toxicity.ToString();
                                else if (toxicity.Type == JTokenType.Object)
                                    warnings = toxicity["value"]?.ToString()
                                        ?? toxicity["description"]?.ToString()
                                        ?? string.Empty;
                                else if (toxicity.Type == JTokenType.Array && toxicity.HasValues)
                                    warnings = string.Join(", ", toxicity.Select(t => t.ToString()));
                            }
                        }
                        catch { }

                        try
                        {
                            var kbImage = details["image"]?["value"]?.ToString();
                            if (!string.IsNullOrEmpty(kbImage))
                                imageUrl = kbImage;
                        }
                        catch { }
                    }
                }
            }

            var displayName = !string.IsNullOrEmpty(commonName)
                ? $"{commonName} ({scientificName})"
                : scientificName;

            return new PlantResult
            {
                PlantName = displayName,
                Confidence = probability,
                Description = description,
                CommonUses = uses,
                Warnings = warnings,
                ImageUrl = imageUrl
            };
        }
        catch (Exception)
        {
            
            return null;
        }
    }
}