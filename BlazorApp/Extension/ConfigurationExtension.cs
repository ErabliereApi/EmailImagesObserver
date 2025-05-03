namespace BlazorApp.Extension;

public static class ConfigurationExtension
{
    public static bool UseFlorence2AI(this IConfiguration configuration)
    {
        return configuration["USE_FLORENCE2_VISION"]?.ToLower()?.Trim() == "true";
    }

    public static bool UseAzureVision(this IConfiguration configuration)
    {
        return configuration["USE_AZURE_VISION"]?.ToLower()?.Trim() == "true";
    }

    public static string? DatabaseProvider(this IConfiguration configuration)
    {
        return configuration["Database:Provider"];
    }

    public static bool CorsEnabled(this IConfiguration configuration)
    {
        return string.Equals(configuration["CORS_ENABLE"]?.Trim(), bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HttpsRedirectionEnabled(this IConfiguration configuration)
    {
        return !string.Equals(configuration["DISABLE_HTTPS_REDIRECTION"]?.Trim(), bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }
}
