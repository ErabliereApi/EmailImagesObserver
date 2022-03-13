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
            app.Use((context, next) =>
            {
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

        return app;
    }
}
