using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using BlazorApp.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BlazorApp.Pages
{
    public partial class Login
    {
        [Inject]
        public ILogger<Login> Logger { get; set; }

       //[Inject]
       // public IHttpContextAccessor ContextAccessor { get; set; }

        public LoginModel LoginModel { get; set; } = new LoginModel();

        private async Task HandleValidSubmitAsync()
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

                //await ContextAccessor.HttpContext.SignInAsync(
               //     CookieAuthenticationDefaults.AuthenticationScheme,
                //    new ClaimsPrincipal(claimIdentity));
            }
        }
    }
}
