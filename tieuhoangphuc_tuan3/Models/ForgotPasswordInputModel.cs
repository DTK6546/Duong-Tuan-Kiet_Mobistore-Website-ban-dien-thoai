using System.ComponentModel.DataAnnotations;

namespace WebBanDienThoai.Models
{
    public class ForgotPasswordInputModel
    {
        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            public string OTP { get; set; }

            [DataType(DataType.Password)]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public InputModel Input { get; set; } = new InputModel();
        public bool OTPGenerated { get; set; }
        public bool OTPSent { get; set; }
    }
}
