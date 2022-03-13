using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace BlazorApp.Controller;

/// <summary>
/// Controller used to manage redirection with Microsoft to login and logout users
/// </summary>
[Route("[controller]/[action]")]
public class AzureADController : ControllerBase
{
    /// <summary>
    /// Login method to handle Microsoft authentication
    /// </summary>
    /// <param name="returnUrl">The returnUrl use after the microsoft authentication is completed</param>
    [HttpGet]
    public async Task<ActionResult> Login(string returnUrl)
    {
        var props = new AuthenticationProperties
        {
            RedirectUri = returnUrl
        };

        return await Task.Run(() => Challenge(props));
    }
}