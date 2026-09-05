using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using System.Globalization;

namespace WeatherApplication.Pages;

public class IndexModel : PageModel
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public IndexModel(IConfiguration configuration)
    {
        _configuration = configuration;
        _httpClient = new HttpClient();
    }

    public string City { get; set; } = "";
    public string Temperature { get; set; } = "";
    public string Condition { get; set; } = "";
    public string Humidity { get; set; } = "";
    public string WindSpeed { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public List<ForecastInfo> Forecast { get; set; } = new List<ForecastInfo>();
    public List<HistoricalInfo> Historical { get; set; } = new List<HistoricalInfo>();
    public bool HasSearched { get; set; } = false;

    public async Task OnGetAsync(string city)
    {
        if (string.IsNullOrEmpty(city))
        {
            return;
        }

        HasSearched = true;
        string apiKey = _configuration["OpenWeather:ApiKey"];

        if (string.IsNullOrEmpty(apiKey) || apiKey.Length < 10)
        {
            ErrorMessage = "API Key is missing or invalid. Please check appsettings.json";
            return;
        }

        try
        {
            // Get Current Weather
            string url = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric";
            HttpResponseMessage response = await _httpClient.GetAsync(url);
            string json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = $"City '{city}' not found. Please check spelling.";
                return;
            }

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            City = root.GetProperty("name").GetString() ?? city;
            double temp = root.GetProperty("main").GetProperty("temp").GetDouble();
            Temperature = temp.ToString("0.0");
            Condition = root.GetProperty("weather")[0].GetProperty("description").GetString() ?? "";
            Humidity = root.GetProperty("main").GetProperty("humidity").GetInt32().ToString();
            double wind = root.GetProperty("wind").GetProperty("speed").GetDouble();
            WindSpeed = wind.ToString("0.0");

            // Grab coordinates for the historical lookup
            double lat = root.GetProperty("coord").GetProperty("lat").GetDouble();
            double lon = root.GetProperty("coord").GetProperty("lon").GetDouble();

            // Get 5-Day Forecast
            string forecastUrl = $"https://api.openweathermap.org/data/2.5/forecast?q={city}&appid={apiKey}&units=metric";
            HttpResponseMessage forecastResponse = await _httpClient.GetAsync(forecastUrl);
            string forecastJson = await forecastResponse.Content.ReadAsStringAsync();

            if (forecastResponse.IsSuccessStatusCode)
            {
                using JsonDocument forecastDoc = JsonDocument.Parse(forecastJson);
                JsonElement forecastRoot = forecastDoc.RootElement;
                JsonElement list = forecastRoot.GetProperty("list");

                var grouped = list.EnumerateArray()
                    .GroupBy(x => x.GetProperty("dt_txt").GetString()?.Split(' ')[0] ?? "")
                    .Take(5);

                foreach (var group in grouped)
                {
                    var firstItem = group.First();
                    string date = group.Key;
                    double avgTemp = group.Average(x => x.GetProperty("main").GetProperty("temp").GetDouble());
                    string condition = firstItem.GetProperty("weather")[0].GetProperty("description").GetString() ?? "";
                    string icon = firstItem.GetProperty("weather")[0].GetProperty("icon").GetString() ?? "";

                    Forecast.Add(new ForecastInfo
                    {
                        Date = date,
                        Temperature = avgTemp.ToString("0.0"),
                        Condition = condition,
                        Icon = icon
                    });
                }
            }

            // Get Past 5 Days (Historical) from Open-Meteo — free, no API key needed
            await LoadHistoricalAsync(lat, lon);
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Network error. Please check your internet connection.";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error: {ex.Message}";
        }
    }

    private async Task LoadHistoricalAsync(double lat, double lon)
    {
        try
        {
            // Open-Meteo's archive has a small delay, so we look at
            // the 5 days ending 3 days ago to make sure data exists.
            DateTime endDate = DateTime.UtcNow.Date.AddDays(-3);
            DateTime startDate = endDate.AddDays(-4);

            string startStr = startDate.ToString("yyyy-MM-dd");
            string endStr = endDate.ToString("yyyy-MM-dd");
            string latStr = lat.ToString(CultureInfo.InvariantCulture);
            string lonStr = lon.ToString(CultureInfo.InvariantCulture);

            string historicalUrl = "https://archive-api.open-meteo.com/v1/archive" +
                $"?latitude={latStr}&longitude={lonStr}" +
                $"&start_date={startStr}&end_date={endStr}" +
                "&daily=temperature_2m_max,temperature_2m_min,weathercode" +
                "&timezone=auto";

            HttpResponseMessage histResponse = await _httpClient.GetAsync(historicalUrl);
            if (!histResponse.IsSuccessStatusCode)
            {
                return; // just leave Historical empty; UI shows "not available"
            }

            string histJson = await histResponse.Content.ReadAsStringAsync();
            using JsonDocument histDoc = JsonDocument.Parse(histJson);
            JsonElement daily = histDoc.RootElement.GetProperty("daily");

            var dates = daily.GetProperty("time").EnumerateArray().Select(x => x.GetString() ?? "").ToList();
            var maxTemps = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(x => x.GetDouble()).ToList();
            var minTemps = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(x => x.GetDouble()).ToList();
            var codes = daily.GetProperty("weathercode").EnumerateArray().Select(x => x.GetInt32()).ToList();

            for (int i = 0; i < dates.Count; i++)
            {
                Historical.Add(new HistoricalInfo
                {
                    Date = dates[i],
                    MaxTemp = maxTemps[i].ToString("0.0"),
                    MinTemp = minTemps[i].ToString("0.0"),
                    Condition = WeatherCodeToText(codes[i])
                });
            }
        }
        catch
        {
            // Silently ignore — historical data is a bonus feature,
            // it shouldn't break the rest of the page if it fails.
        }
    }

    private static string WeatherCodeToText(int code)
    {
        return code switch
        {
            0 => "Clear sky",
            1 or 2 or 3 => "Partly cloudy",
            45 or 48 => "Fog",
            51 or 53 or 55 => "Drizzle",
            56 or 57 => "Freezing drizzle",
            61 or 63 or 65 => "Rain",
            66 or 67 => "Freezing rain",
            71 or 73 or 75 => "Snow",
            77 => "Snow grains",
            80 or 81 or 82 => "Rain showers",
            85 or 86 => "Snow showers",
            95 => "Thunderstorm",
            96 or 99 => "Thunderstorm with hail",
            _ => "Unknown"
        };
    }

    public class ForecastInfo
    {
        public string Date { get; set; } = "";
        public string Temperature { get; set; } = "";
        public string Condition { get; set; } = "";
        public string Icon { get; set; } = "";
    }

    public class HistoricalInfo
    {
        public string Date { get; set; } = "";
        public string MaxTemp { get; set; } = "";
        public string MinTemp { get; set; } = "";
        public string Condition { get; set; } = "";
    }
}