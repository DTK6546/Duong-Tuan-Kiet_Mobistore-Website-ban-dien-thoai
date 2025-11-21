using System.Security.Cryptography;
using System.Text;
using System.Net;

namespace WebBanDienThoai.Services.VNPay
{
    public class VNPayLibrary
    {
        private SortedList<string, string> requestData = new();

        public void AddRequestData(string key, string value)
        {
            requestData.Add(key, value);
        }

        public string CreateRequestUrl(string baseUrl, string vnp_HashSecret)
        {
            var data = new StringBuilder();
            foreach (var kv in requestData)
            {
                data.Append(WebUtility.UrlEncode(kv.Key) + "=" + WebUtility.UrlEncode(kv.Value) + "&");
            }

            var rawData = data.ToString().TrimEnd('&');
            var hash = HmacSHA512(vnp_HashSecret, rawData);
            return $"{baseUrl}?{rawData}&vnp_SecureHash={hash}";
        }

        public static string HmacSHA512(string key, string inputData)
        {
            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(inputData));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}
