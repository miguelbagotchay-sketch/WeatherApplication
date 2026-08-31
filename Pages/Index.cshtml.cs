using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

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

            // Parse JSON
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            // Extract data
            City = root.GetProperty("name").GetString() ?? city;
            double temp = root.GetProperty("main").GetProperty("temp").GetDouble();
            Temperature = temp.ToString("0.0");
            Condition = root.GetProperty("weather")[0].GetProperty("description").GetString() ?? "";
            Humidity = root.GetProperty("main").GetProperty("humidity").GetInt32().ToString();
            double wind = root.GetProperty("wind").GetProperty("speed").GetDouble();
            WindSpeed = wind.ToString("0.0");

            // Get 5-Day Forecast
            string forecastUrl = $"https://api.openweathermap.org/data/2.5/forecast?q={city}&appid={apiKey}&units=metric";
            HttpResponseMessage forecastResponse = await _httpClient.GetAsync(forecastUrl);
            string forecastJson = await forecastResponse.Content.ReadAsStringAsync();

            if (forecastResponse.IsSuccessStatusCode)
            {
                using JsonDocument forecastDoc = JsonDocument.Parse(forecastJson);
                JsonElement forecastRoot = forecastDoc.RootElement;
                JsonElement list = forecastRoot.GetProperty("list");

                // Group by date
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

    public class ForecastInfo
    {
        public string Date { get; set; } = "";
        public string Temperature { get; set; } = "";
        public string Condition { get; set; } = "";
        public string Icon { get; set; } = "";
    }
}