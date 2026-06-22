using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace WebBanDienThoai.Services
{
    public class GeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GeminiService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Gemini:ApiKey"]?.Trim();

            if (string.IsNullOrEmpty(_apiKey))
            {
                throw new InvalidOperationException("Gemini API Key chưa được cấu hình trong file appsettings.json!");
            }
        }

        public async Task<string> GetChatResponseAsync(string userMessage, string contextFaq = "")
        {
            try
            {
                string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

                // Cấu hình luật hệ thống (System Instruction) chuẩn hóa theo tài liệu API Google
                string instructionText = "Bạn là trợ lý ảo bán hàng thông minh của cửa hàng điện thoại MobiStore. Trả lời cực kỳ ngắn gọn dưới 3 câu. ";
                if (!string.IsNullOrEmpty(contextFaq))
                {
                    instructionText += $"Sử dụng dữ liệu FAQ chính thức sau đây để trả lời khách: {contextFaq}. ";
                }
                instructionText += "Nếu câu hỏi nằm ngoài ngành điện thoại/công nghệ và không có trong tài liệu, hãy từ chối lịch sự và bảo khách nhấn nút 'Gặp nhân viên'.";

                // Tạo Payload JSON chuẩn hóa cấu trúc phân cấp của Gemini API
                var requestBody = new
                {
                    contents = new[]
                    {
                        new {
                            parts = new[]
                            {
                                new { text = userMessage }
                            }
                        }
                    },
                    systemInstruction = new
                    {
                        parts = new[]
                        {
                            new { text = instructionText }
                        }
                    }
                };

                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                string jsonPayload = JsonSerializer.Serialize(requestBody, options);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(url, content);
                string responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    using JsonDocument doc = JsonDocument.Parse(responseString);
                    JsonElement root = doc.RootElement;

                    if (root.TryGetProperty("candidates", out JsonElement candidates) && candidates.GetArrayLength() > 0)
                    {
                        JsonElement firstCandidate = candidates[0];
                        if (firstCandidate.TryGetProperty("content", out JsonElement resContent))
                        {
                            if (resContent.TryGetProperty("parts", out JsonElement parts) && parts.GetArrayLength() > 0)
                            {
                                string textResult = parts[0].GetProperty("text").GetString();
                                if (!string.IsNullOrEmpty(textResult))
                                {
                                    return textResult.Trim();
                                }
                            }
                        }
                    }
                }

                // Nếu API bị lỗi (Quota, Network...) tự động chuyển sang luồng dự phòng cục bộ
                return GetLocalFallbackResponse(userMessage, contextFaq);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gemini Service Exception]: {ex.Message}");
                return GetLocalFallbackResponse(userMessage, contextFaq);
            }
        }

        private string GetLocalFallbackResponse(string userMessage, string contextFaq)
        {
            // Trích xuất câu trả lời trực tiếp từ FAQ Context nếu có khớp từ khóa trong Database lúc Offline
            if (!string.IsNullOrEmpty(contextFaq) && contextFaq.Contains("Trả lời:"))
            {
                int startIndex = contextFaq.IndexOf("Trả lời:") + 8;
                int endIndex = contextFaq.IndexOf("Câu hỏi:", startIndex);
                string localAnswer = endIndex == -1 ? contextFaq.Substring(startIndex) : contextFaq.Substring(startIndex, endIndex - startIndex);

                return $"🤖 [MobiStore AI (Offline)]: {localAnswer.Replace("(", "").Replace(")", "").Trim()}";
            }

            return "👋 Cảm ơn bạn đã liên hệ MobiStore! Yêu cầu của bạn đã được ghi nhận. Hệ thống đang đồng bộ kho hàng, bạn vui lòng đợi trong giây lát hoặc nhấn nút 'Gặp nhân viên' để được hỗ trợ trực tiếp nhé!";
        }
    }
}