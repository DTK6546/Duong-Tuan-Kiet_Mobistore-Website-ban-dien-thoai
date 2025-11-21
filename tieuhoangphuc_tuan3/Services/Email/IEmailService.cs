using Microsoft.AspNetCore.Identity.UI.Services;
using System.Net.Mail;
using System.Threading.Tasks;

namespace WebBanDienThoai.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;

        public EmailSender(IConfiguration config)
        {
            _config = config;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var smtpClient = new SmtpClient
            {
                Host = _config["Email:Smtp:Host"],
                Port = int.Parse(_config["Email:Smtp:Port"]),
                EnableSsl = true,
                Credentials = new System.Net.NetworkCredential(
                    _config["Email:Smtp:Username"],
                    _config["Email:Smtp:Password"])
            };

            using var emailMessage = new MailMessage
            {
                From = new MailAddress(_config["Email:Smtp:From"]),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };

            emailMessage.To.Add(new MailAddress(email));
            await smtpClient.SendMailAsync(emailMessage);
        }
    }
}