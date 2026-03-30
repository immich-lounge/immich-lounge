using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ImmichLoungeCompanion.Api;

[ApiController]
[Route("api/geocode")]
public class GeocodeController(IHttpClientFactory httpClientFactory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return Ok(Array.Empty<object>());
        }

        var client = httpClientFactory.CreateClient("nominatim");
        var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(q)}&format=json&limit=5&addressdetails=0";
        var results = await client.GetFromJsonAsync<List<NominatimResult>>(url);
        return Ok(results?.Select(r => new GeocodeResult(r.DisplayName, r.Lat, r.Lon)) ?? []);
    }

    private record NominatimResult(
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("lat")] string Lat,
        [property: JsonPropertyName("lon")] string Lon);

    public record GeocodeResult(string DisplayName, string Lat, string Lon);
}
