using BlazorApp.Services;

namespace BlazorApp.Extension;

public static class ReverseProxyExtension
{
    public static IApplicationBuilder AddReverseProxyPathOptions(this IApplicationBuilder app, IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config["UsePathBase"]) == false)
        {
            app.UsePathBase(config["UsePathBase"]);
        }

        if (string.IsNullOrWhiteSpace(config["StartsWithSegments"]) == false)
        {
            var exclusion = config["StartsWithSegments.Exclusions"]?.Split(',');

            app.Use((context, next) =>
            {
                if (exclusion?.Contains(context.Request.Path.ToString()) == true)
                {
                    return next();
                }

                if (context.Request.Path.StartsWithSegments(config["StartsWithSegments"], out var remainder))
                {
                    context.Request.Path = remainder;
                }

                return next();
            });
        }

        if (string.IsNullOrWhiteSpace(config["AddStartSegments"]) == false)
        {
            app.Use((context, next) =>
            {
                context.Request.Path = $"{config["AddStartSegments"]}{context.Request.Path}";

                return next();
            });
        }

        if (AddAuthentificationExtension.IsAzureADAuth(config))
        {
            app.Use(async (context, next) =>
            {
                await next();

                if (context.Response.StatusCode == 302 && 
                    context.Request.Path.Equals(config["AzureAD:CallbackPath"]))
                {
                    try
                    {
                        var urlService = context.RequestServices.GetRequiredService<UrlService>();

                        context.Response.Headers["location"] = urlService.Url(context.Response.Headers["location"]);
                    }
                    catch (Exception e)
                    {
                        var logger = context.RequestServices.GetService<ILogger<UrlServiceForAADSigninRewrite>>();

                        if (logger is not null)
                        {
                            logger.LogCritical(e, e.Message);
                        }
                    }
                }
            });
        }

        return app;
    }
}

public class UrlServiceForAADSigninRewrite { }