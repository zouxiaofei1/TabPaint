using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace TabPaint
{
    public sealed class AiImageGenerationService
    {
        private static readonly Lazy<AiImageGenerationService> _lazy =
            new Lazy<AiImageGenerationService>(() => new AiImageGenerationService());

        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(2)
        };

        public static AiImageGenerationService Instance => _lazy.Value;

        private AiImageGenerationService() { }

        public async Task<string> GenerateImageAsync(string prompt, string apiBaseUrl, string apiKey, string model)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("Prompt is empty.");
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
                throw new ArgumentException("API Base URL is empty.");
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("API Key is empty.");

            if (!IsValidApiBaseUrl(apiBaseUrl))
                throw new ArgumentException("Invalid API Base URL format.");

            string endpoint = BuildEndpoint(apiBaseUrl);
            string finalModel = string.IsNullOrWhiteSpace(model) ? "gpt-image-1" : model.Trim();

            var payload = new
            {
                model = finalModel,
                prompt = prompt,
                n = 1,
                size = "1024x1024",
                response_format = "b64_json"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            string responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {responseText}");
            }

            byte[] imageBytes = await ParseImageBytesAsync(responseText);
            return await SaveGeneratedImageAsync(imageBytes);
        }

        private static string BuildEndpoint(string apiBaseUrl)
        {
            if (!IsValidApiBaseUrl(apiBaseUrl))
                throw new ArgumentException("Invalid API Base URL format.");

            string trimmed = apiBaseUrl.Trim().TrimEnd('/');
            if (trimmed.EndsWith("/images/generations", StringComparison.OrdinalIgnoreCase))
                return trimmed;
            return $"{trimmed}/images/generations";
        }

        public static bool IsValidApiBaseUrl(string? apiBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
                return false;

            string trimmed = apiBaseUrl.Trim();
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return false;

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private static async Task<byte[]> ParseImageBytesAsync(string responseText)
        {
            using var doc = JsonDocument.Parse(responseText);
            if (!doc.RootElement.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array ||
                data.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("AI response missing data array.");
            }

            var first = data[0];

            if (first.TryGetProperty("b64_json", out var b64Node))
            {
                string? b64 = b64Node.GetString();
                if (string.IsNullOrWhiteSpace(b64))
                    throw new InvalidOperationException("b64_json is empty.");
                return Convert.FromBase64String(b64);
            }

            if (first.TryGetProperty("url", out var urlNode))
            {
                string? url = urlNode.GetString();
                if (string.IsNullOrWhiteSpace(url))
                    throw new InvalidOperationException("url is empty.");
                return await _httpClient.GetByteArrayAsync(url);
            }

            throw new InvalidOperationException("AI response contains neither b64_json nor url.");
        }

        private static async Task<string> SaveGeneratedImageAsync(byte[] imageBytes)
        {
            string dir = Path.Combine(AppConsts.CacheDir, "AiGenerated");
            Directory.CreateDirectory(dir);

            string filePath = Path.Combine(dir, $"ai_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");
            await File.WriteAllBytesAsync(filePath, imageBytes);
            return filePath;
        }
    }
}
