using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

public class GeminiService
{
    private readonly string apiKey = "AIzaSyCVK0Bq53zFrx0BPhev9q1ubzmqPvqpm2c";

    public async Task<string> Ask(string message)
    {
        var client = new HttpClient();

        var body = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new { text = message }
                    }
                }
            }
        };

        var json = JsonConvert.SerializeObject(body);

        var response = await client.PostAsync(
      $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}",
              new StringContent(json, Encoding.UTF8, "application/json")
        );

        var result = await response.Content.ReadAsStringAsync();
        return result;
    }
}