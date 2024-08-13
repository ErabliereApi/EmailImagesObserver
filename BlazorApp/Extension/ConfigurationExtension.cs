namespace BlazorApp.Extension;

public static class ConfigurationExtension
{
    public static bool UseFlorence2AI(this IConfiguration configuration)
    {
        return configuration["USE_FLORENCE2_VISION"]?.ToLower()?.Trim() == "true";
    }
}
