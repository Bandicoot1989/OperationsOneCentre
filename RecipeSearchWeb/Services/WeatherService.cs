using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace RecipeSearchWeb.Services;

public class WeatherService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(HttpClient httpClient, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<WeatherData?> GetWeatherForecastAsync(double latitude, double longitude)
    {
        try
        {
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&longitude={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&daily=temperature_2m_max,temperature_2m_min,weathercode&timezone=auto&forecast_days=7";
            
            _logger.LogInformation("Fetching weather from: {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Weather API error: {StatusCode} - {Content}", response.StatusCode, errorContent);
                return null;
            }
            
            var weatherResponse = await response.Content.ReadFromJsonAsync<OpenMeteoResponse>();
            
            if (weatherResponse?.Daily == null)
                return null;

            var forecasts = new List<DailyForecast>();
            for (int i = 0; i < Math.Min(7, weatherResponse.Daily.Time.Count); i++)
            {
                forecasts.Add(new DailyForecast
                {
                    Date = DateOnly.Parse(weatherResponse.Daily.Time[i]),
                    TemperatureMax = (int)Math.Round(weatherResponse.Daily.Temperature2mMax[i]),
                    TemperatureMin = (int)Math.Round(weatherResponse.Daily.Temperature2mMin[i]),
                    WeatherCode = weatherResponse.Daily.Weathercode[i]
                });
            }

            return new WeatherData
            {
                Latitude = weatherResponse.Latitude,
                Longitude = weatherResponse.Longitude,
                Forecasts = forecasts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching weather data for lat={Latitude}, lon={Longitude}", latitude, longitude);
            return null;
        }
    }

    public async Task<GeoLocation?> GetLocationFromIpAsync()
    {
        try
        {
            // Use ip-api.com for free IP geolocation
            var url = "http://ip-api.com/json/?fields=status,country,city,lat,lon";
            _logger.LogInformation("Fetching location from: {Url}", url);
            
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Location API response: {Content}", content);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Location API returned status: {StatusCode}", response.StatusCode);
                return GetDefaultLocation();
            }
            
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            };
            
            var locationResponse = System.Text.Json.JsonSerializer.Deserialize<IpApiResponse>(content, options);
            
            if (locationResponse?.Status == "success")
            {
                _logger.LogInformation("Location detected: {City}, {Country} (Lat: {Lat}, Lon: {Lon})", 
                    locationResponse.City, locationResponse.Country, locationResponse.Lat, locationResponse.Lon);
                
                // Validate coordinates
                if (locationResponse.Lat < -90 || locationResponse.Lat > 90 || 
                    locationResponse.Lon < -180 || locationResponse.Lon > 180)
                {
                    _logger.LogWarning("Invalid coordinates: Lat={Lat}, Lon={Lon}", locationResponse.Lat, locationResponse.Lon);
                    return GetDefaultLocation();
                }
                
                return new GeoLocation
                {
                    Latitude = locationResponse.Lat,
                    Longitude = locationResponse.Lon,
                    City = locationResponse.City ?? "Unknown",
                    Country = locationResponse.Country ?? "Unknown"
                };
            }
            
            _logger.LogWarning("Location API returned unsuccessful status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting location from IP");
        }

        return GetDefaultLocation();
    }

    private GeoLocation GetDefaultLocation()
    {
        _logger.LogInformation("Using default location: New York");
        return new GeoLocation
        {
            Latitude = 40.7128,
            Longitude = -74.0060,
            City = "New York",
            Country = "USA"
        };
    }

    public static string GetWeatherDescription(int weatherCode)
    {
        return weatherCode switch
        {
            0 => "Clear Sky",
            1 or 2 or 3 => "Partly Cloudy",
            45 or 48 => "Foggy",
            51 or 53 or 55 => "Light Drizzle",
            56 or 57 => "Freezing Drizzle",
            61 or 63 or 65 => "Rainy",
            66 or 67 => "Freezing Rain",
            71 or 73 or 75 => "Snowy",
            77 => "Snow Grains",
            80 or 81 or 82 => "Rain Showers",
            85 or 86 => "Snow Showers",
            95 => "Thunderstorm",
            96 or 99 => "Thunderstorm with Hail",
            _ => "Unknown"
        };
    }

    public static string GetWeatherEmoji(int weatherCode)
    {
        return weatherCode switch
        {
            0 => "☀️",
            1 or 2 or 3 => "⛅",
            45 or 48 => "🌫️",
            51 or 53 or 55 => "🌦️",
            56 or 57 => "🌧️",
            61 or 63 or 65 => "🌧️",
            66 or 67 => "🌨️",
            71 or 73 or 75 => "❄️",
            77 => "🌨️",
            80 or 81 or 82 => "🌦️",
            85 or 86 => "🌨️",
            95 => "⛈️",
            96 or 99 => "⛈️",
            _ => "🌈"
        };
    }
}

// Response models for Open-Meteo API
public class OpenMeteoResponse
{
    [JsonPropertyName("latitude")]
    public double Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public double Longitude { get; set; }

    [JsonPropertyName("daily")]
    public DailyData? Daily { get; set; }
}

public class DailyData
{
    [JsonPropertyName("time")]
    public List<string> Time { get; set; } = new();

    [JsonPropertyName("temperature_2m_max")]
    public List<double> Temperature2mMax { get; set; } = new();

    [JsonPropertyName("temperature_2m_min")]
    public List<double> Temperature2mMin { get; set; } = new();

    [JsonPropertyName("weathercode")]
    public List<int> Weathercode { get; set; } = new();
}

// Response model for IP API
public class IpApiResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("city")]
    public string City { get; set; } = string.Empty;

    [JsonPropertyName("lat")]
    public double Lat { get; set; }

    [JsonPropertyName("lon")]
    public double Lon { get; set; }
}

// Domain models
public class WeatherData
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public List<DailyForecast> Forecasts { get; set; } = new();
}

public class DailyForecast
{
    public DateOnly Date { get; set; }
    public int TemperatureMax { get; set; }
    public int TemperatureMin { get; set; }
    public int WeatherCode { get; set; }
}

public class GeoLocation
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
