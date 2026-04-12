using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace ljp_itsolutions.Models
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Username or email is required")]
        [Display(Name = "Username or Email")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool? RememberMe { get; set; }

        [FromForm(Name = "g-recaptcha-response")]
        public string? RecaptchaResponse { get; set; }
    }
}
