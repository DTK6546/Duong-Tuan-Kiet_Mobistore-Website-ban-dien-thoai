using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;
using System.Security.Cryptography;
using System.Text;

using WebBanDienThoai.Models;

namespace WebBanDienThoai.Services.Momo
{

    public class MomoService : IMomoService

    {
        private readonly IOptions<MomoOptionModel> _options;

        public MomoService(IOptions<MomoOptionModel> options)
        {
            _options = options;
        }

        public async Task<MomoCreatePaymentResponseModel> CreatePaymentMomo(OrderInfoModel model, string userName)
        {
            model.OrderId = DateTime.UtcNow.Ticks.ToString();
            var requestId = model.OrderId;

            model.OrderInformation = "Khách hàng: " + userName + ". Nội dung đơn hàng: " + model.OrderInformation;

            string rawHash =
                "accessKey=" + _options.Value.AccessKey +
                "&amount=" + model.Amount +
                "&extraData=" +
                "&ipnUrl=" + _options.Value.NotifyUrl +
                "&orderId=" + model.OrderId +
                "&orderInfo=" + model.OrderInformation +
                "&partnerCode=" + _options.Value.PartnerCode +
                "&redirectUrl=" + _options.Value.ReturnUrl +
                "&requestId=" + requestId +
                "&requestType=" + _options.Value.RequestType;

            string signature = ComputeHmacSha256(rawHash, _options.Value.SecretKey);

            var requestData = new
            {
                partnerCode = _options.Value.PartnerCode,
                accessKey = _options.Value.AccessKey,
                requestId = requestId,
                amount = model.Amount.ToString(),
                orderId = model.OrderId,
                orderInfo = model.OrderInformation,
                redirectUrl = _options.Value.ReturnUrl,
                ipnUrl = _options.Value.NotifyUrl,
                extraData = "",
                requestType = _options.Value.RequestType,
                signature = signature,
                lang = "vi"
            };

            var client = new RestClient(_options.Value.MomoApiUrl);
            var request = new RestRequest("", Method.Post);
            request.AddHeader("Content-Type", "application/json");

            var json = JsonConvert.SerializeObject(requestData);
            request.AddStringBody(json, DataFormat.Json);

            var response = await client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                return new MomoCreatePaymentResponseModel
                {
                    ErrorCode = -1,
                    Message = "HTTP error: " + response.StatusCode
                };
            }

            return JsonConvert.DeserializeObject<MomoCreatePaymentResponseModel>(response.Content);
        }


        public Task<string> CreatePaymentAsync(decimal amount)
        {
            throw new NotImplementedException();
        }


        public MomoExecuteResponseModel PaymentExecuteAsync(IQueryCollection collection)
        {
            var amount = collection.First(s => s.Key == "amount").Value;
            var orderInfo = collection.First(s => s.Key == "orderInfo").Value;
            var orderId = collection.First(s => s.Key == "orderId").Value;

            return new MomoExecuteResponseModel()
            {
                Amount = amount,
                OrderId = orderId,
                OrderInfo = orderInfo
            };
        }
        private string ComputeHmacSha256(string message, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(messageBytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }
}

