using BlazorApp.Autorization;
using BlazorApp.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Web;
using Microsoft.Rest;

namespace BlazorApp.Extension;

/// <summary>
/// Class that handle the authorization configuration of the app.
/// 
/// There or 4 possible mode of authentication.
/// 1. No auth, if there is nothing configure about the authorization, then the app will work without any auth.
/// 2. CookieAuth, if there is a COOKIEAUTH_USER defined. Then this user will have to login to use the app. You can set a passwork with the COOKIE_PASSWORD environment variable.
/// 3. Identity, if the environment variable USE_IDENTITY is set to true, Microsoft.Identity will be use with SqLite database.
/// 4. Microsoft account, if the environemtn variable MicrosoftId is initialised, then Microsoft account is going to be used. MicrosoftId represent the ClientId and MicrosoftSecret is the secret create in the AzureAD client credentials section.
/// </summary>
/// <remarks>
/// You can use only one authentication method at the time, otherwise the application will throw and InvalidProgramException
/// </remarks>
public static class AddAuthentificationExtension
{
    /// <exception cref="InvalidProgramException" />
    public static IServiceCollection AddTeamMemberVelocityAutorisation(this IServiceCollection services, IConfiguration configuration)
    {
        if (!OnlyOneAuthMethodIsUsed(configuration))
        {
            throw new InvalidProgramException("You cannot use cookie user and identity at the same time. See the github wiki for more information.");
        }

        if (IsCookieAuth(configuration))
        {
            services.AddSingleton(new AuthUrlPagesProvider(CookieAuthenticationDefaults.AuthenticationScheme));

            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                    .AddCookie();
        }
        else if (IsIdentityAuth(configuration))
        {
            services.AddSingleton(new AuthUrlPagesProvider("Identity.Application"));

            services.AddAuthentication("Identity.Application")
                    .AddCookie();
        }
        else if (IsAzureADAuth(configuration))
        {
            services.AddSingleton(new AuthUrlPagesProvider("AzureAD"));

            // Support for kubernetes
            // Colon is not allow in environement variable name
            if (string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD:ClientId")) &&
                !string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD__ClientId")))
            {
                configuration["AzureAD:ClientId"] = configuration.GetValue<string>("AzureAD__ClientId");
            }

            if (string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD:TenantId")) &&
                !string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD__TenantId")))
            {
                configuration["AzureAD:TenantId"] = configuration.GetValue<string>("AzureAD__TenantId");
            }

            if (string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD:CallbackPath")) &&
                !string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD__CallbackPath")))
            {
                configuration["AzureAD:CallbackPath"] = configuration.GetValue<string>("AzureAD__CallbackPath");
            }

            if (string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD:SignedOutCallbackPath")) && !string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD__SignedOutCallbackPath")))
            {
                configuration["AzureAD:SignedOutCallbackPath"] = configuration.GetValue<string>("AzureAD__SignedOutCallbackPath");
            }

            if (string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD:Instance")) &&
                !string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD__Instance")))
            {
                configuration["AzureAD:Instance"] = configuration.GetValue<string>("AzureAD__Instance");
            }

            services.AddMicrosoftIdentityWebAppAuthentication(configuration);
        }
        else
        {
            services.AddSingleton(new AuthUrlPagesProvider(""));

            services.AddSingleton<IHostEnvironmentAuthenticationStateProvider, AllowAnonymousStateProvider>();
            services.AddSingleton<IAuthorizationHandler, AllowAnonymousAuthorizationHandler>();
        }

        services.AddScoped<TokenProvider>();

        return services;
    }

    /// <summary>
    /// Method use to validate the configuration of the app. Only one method should be identify.
    /// </summary>
    /// <returns></returns>
    public static bool OnlyOneAuthMethodIsUsed(IConfiguration configuration)
    {
        int count = 0;

        if (IsCookieAuth(configuration)) count++;

        if (IsIdentityAuth(configuration)) count++;

        if (IsAzureADAuth(configuration)) count++;

        return count <= 1;
    }

    /// <summary>
    /// Indicate if the AzureAD authentication mecanism is configure
    /// </summary>
    /// <returns></returns>
    public static bool IsAzureADAuth(IConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD:TenantId")) &&
            string.IsNullOrWhiteSpace(configuration.GetValue<string>("AzureAD__TenantId")))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Indicate if the self-hosted Identity authentication mecanism is configure
    /// </summary>
    /// <returns></returns>
    public static bool IsIdentityAuth(IConfiguration configuration)
    {
        return string.Equals(configuration.GetValue<string>("USE_IDENTITY"), bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Indicate if the environment user cookie authentication mecanism is configure
    /// </summary>
    /// <returns></returns>
    public static bool IsCookieAuth(IConfiguration configuration)
    {
        return !string.IsNullOrWhiteSpace(configuration.GetValue<string>("COOKIEAUTH_USER"));
    }
}