using BlazorApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace BlazorApp.Controller;

/// <summary>
/// Controller used to manage redirection with Microsoft to login and logout users
/// </summary>
[Route("[controller]/[action]")]
public class AzureADController : ControllerBase
{
    private readonly UrlService _urlService;

    public AzureADController(UrlService urlService)
    {
        _urlService = urlService;
    }

    /// <summary>
    /// Login method to handle Microsoft authentication
    /// </summary>
    /// <param name="returnUrl">The returnUrl use after the microsoft authentication is completed</param>
    [HttpGet]
    public async Task<ActionResult> Login(string? returnUrl)
    {
        if (returnUrl?.StartsWith("/") == false)
        {
            returnUrl = $"/{returnUrl}";
        }

        var props = new AuthenticationProperties
        {
            RedirectUri = _urlService.Url(returnUrl)
        };

        return await Task.Run(() => Challenge(props));
    }
}