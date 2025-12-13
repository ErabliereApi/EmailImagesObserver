using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace BlazorApp.Extension;

/// <summary>
/// Class containing extention method to add Forwarded Headers middleware base on environment variable of the app.
/// </summary>
public static class UseForwardedHeadersExtension
{
    /// <summary>
    /// Add forwarded headers services necessary when using the app behind a reverse proxy
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IServiceCollection AddForwardedHeadersIfEnable(this IServiceCollection services, IConfiguration configuration)
    {
        if (string.Equals(configuration.GetValue<string>("Forwarded_headers"), bool.TrueString, StringComparison.OrdinalIgnoreCase))
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                foreach (var network in configuration.GetValue<string>("KNOW_NETWORKS")?.Split(';') ?? Array.Empty<string>())
                {
                    var ipInfo = network.Split("/");

                    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse(ipInfo[0]), int.Parse(ipInfo[1])));
                }
            });
        }

        return services;
    }

    /// <summary>
    /// Add forwarded headers middleware necessary when using the app behind a reverse proxy
    /// </summary>
    /// <param name="app"></param>
    /// <param name="logger"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseForwardedHeadersRulesIfEnabled(this IApplicationBuilder app, ILogger logger, IConfiguration configuration)
    {
        if (string.Equals(configuration.GetValue<string>("Forwarded_headers"), bool.TrueString, StringComparison.OrdinalIgnoreCase))
        {
            app.UseForwardedHeaders();

            DebugHeaders(app, logger, configuration);

            if (string.Equals(configuration.GetValue<string>("USE_SCHEMA_FROM_PROXY"), bool.TrueString, StringComparison.CurrentCultureIgnoreCase))
            {
                app.Use(async (context, next) =>
                {
                    if (context.Request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto))
                    {
                        var forwaredProtoString = forwardedProto.ToString();

                        if (forwaredProtoString != null) 
                        {
                            context.Request.Scheme = forwaredProtoString;
                        }
                    }

                    await next();
                });
            }
        }

        return app;
    }

    private static void DebugHeaders(IApplicationBuilder app, ILogger logger, IConfiguration configuration)
    {
        if (string.Equals(configuration.GetValue<string>("Debug_headers"), bool.TrueString, StringComparison.OrdinalIgnoreCase))
        {
            app.Use(async (context, next) =>
            {
                // Request method, scheme, and path
                logger.LogDebug("Request Method: {Method}", context.Request.Method);
                logger.LogDebug("Request Scheme: {Scheme}", context.Request.Scheme);
                logger.LogDebug("Request Path: {Path}", context.Request.Path);

                // Headers
                foreach (var header in context.Request.Headers)
                {
                    logger.LogDebug("Header: {Key}: {Value}", header.Key, header.Value);
                }

                // Connection: RemoteIp
                logger.LogDebug("Request RemoteIp: {RemoteIpAddress}",
                    context.Connection.RemoteIpAddress);

                await next();
            });
        }
    }
}