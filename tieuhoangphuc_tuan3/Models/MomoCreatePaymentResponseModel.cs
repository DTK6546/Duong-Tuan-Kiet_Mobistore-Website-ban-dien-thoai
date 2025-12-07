using Newtonsoft.Json;

namespace WebBanDienThoai.Models
{
    public class MomoCreatePaymentResponseModel
    {
        [JsonProperty("partnerCode")]
        public string PartnerCode { get; set; }

        [JsonProperty("orderId")]
        public string OrderId { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        [JsonProperty("amount")]
        public long Amount { get; set; }

        // MoMo trả "resultCode" → map vào ErrorCode
        [JsonProperty("resultCode")]
        public int ErrorCode { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("localMessage")]
        public string LocalMessage { get; set; }

        [JsonProperty("requestType")]
        public string RequestType { get; set; }

        [JsonProperty("payUrl")]
        public string PayUrl { get; set; }

        [JsonProperty("signature")]
        public string Signature { get; set; }

        [JsonProperty("qrCodeUrl")]
        public string QrCodeUrl { get; set; }

        [JsonProperty("deeplink")]
        public string Deeplink { get; set; }

        [JsonProperty("deeplinkWebInApp")]
        public string DeeplinkWebInApp { get; set; }
    }

}
