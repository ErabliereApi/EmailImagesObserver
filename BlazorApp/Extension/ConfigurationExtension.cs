namespace BlazorApp.Extension;

public static class ConfigurationExtension
{
    public static string? GetAAD(this IConfiguration configuration, string value)
    {
        // Support for kubernetes
        // Colon is not allow in environement variable name
        var key1 = $"AzureAD:{value}";
        var key2 = $"AzureAD__{value}";

        if (string.IsNullOrWhiteSpace(configuration.GetValue<string>(key1)) &&
            !string.IsNullOrWhiteSpace(configuration.GetValue<string>(key2)))
        {
            return configuration.GetValue<string>(key2);
        }

        return configuration.GetValue<string>(key1);
    }


}
