using System.Security.Claims;
using BlazorApp.Model;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;

namespace BlazorApp.Pages
{
    public partial class Login
    {
#nullable disable
        [Inject]
        public ILogger<Login> Logger { get; set; }
#nullable enable

        public LoginModel LoginModel { get; set; } = new LoginModel();

        private Task HandleValidSubmitAsync()
        {
            Logger.LogInformation("Process login...");

            if (LoginModel.Username == "demo" && LoginModel.Password == "demo")
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, "Demo"),
                    new Claim(ClaimTypes.Email, "ImageApp.freddycoder.com")
                };

                var claimIdentity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);
            }

            return Task.CompletedTask;
        }
    }
}
