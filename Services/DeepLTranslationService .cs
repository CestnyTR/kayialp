using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace kayialp.Services
{
    // appsettings.json: "DeepL": { "ApiKey": "xxxxxxxx", "Endpoint": "https://api-free.deepl.com/v2/translate" }
    public class DeepLTranslationService : ITranslationService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly string _endpoint;

        public DeepLTranslationService(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _apiKey = cfg["DeepL:ApiKey"] ?? "";
            _endpoint = cfg["DeepL:Endpoint"] ?? "https://api-free.deepl.com/v2/translate";
        }

        public async Task<string> TranslateAsync(string text, string sourceLangCode, string targetLangCode)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            req.Headers.Add("Authorization", $"DeepL-Auth-Key {_apiKey}");
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["text"] = text,
                ["source_lang"] = sourceLangCode.ToUpperInvariant(), // "TR"
                ["target_lang"] = targetLangCode.ToUpperInvariant(), // "EN", "EN-GB" gibi de olabilir
                // ["formality"] = "prefer_more" // istersen
            });

            var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();

            var json = await res.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var translations = root.GetProperty("translations");
            if (translations.GetArrayLength() > 0)
            {
                return translations[0].GetProperty("text").GetString() ?? text;
            }
            return text;
        }
    }
}
