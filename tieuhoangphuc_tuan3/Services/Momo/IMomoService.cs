using WebBanDienThoai.Models;

namespace WebBanDienThoai.Services.Momo
{
    public interface IMomoService
    {
        Task<MomoCreatePaymentResponseModel> CreatePaymentMomo(OrderInfoModel model, string userName);
        Task<string> CreatePaymentAsync(decimal amount);
        MomoExecuteResponseModel PaymentExecuteAsync(IQueryCollection collection);
    }
}