using BlazorApp.Services;
using BlazorApp.AzureComputerVision;
using Microsoft.AspNetCore.DataProtection;
using BlazorApp.Data;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Imap;
using MailKit;
using BlazorApp.Extension;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web.UI;

namespace BlazorApp;

public class Startup
{
    public Startup(IConfiguration configuration, IWebHostEnvironment hostEnvironment)
    {
        Configuration = configuration;
        HostEnvironment = hostEnvironment;
    }

    public IConfiguration Configuration { get; }
    public IWebHostEnvironment HostEnvironment { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddForwardedHeadersIfEnable(Configuration);

        // Blazor app
        var mvcBuilder = services.AddRazorPages();
        if (AddAuthentificationExtension.IsAzureADAuth(Configuration))
        {
            mvcBuilder.AddMvcOptions(options =>
            {
                var policy = new AuthorizationPolicyBuilder()
                              .RequireAuthenticatedUser()
                              .Build();
                options.Filters.Add(new AuthorizeFilter(policy));
            }).AddMicrosoftIdentityUI();
        }
        services.AddServerSideBlazor();
        services.AddScoped<ImageInfoService>();
        services.AddSingleton(new UrlService(Configuration["StartsWithSegments"]));

        services.AddTeamMemberVelocityAutorisation(Configuration);

        services.AddDistributedCaching(Configuration);

        services.AddSession();

        /// IdleClient
        services.AddSingleton<IdleClient>();
        services.AddSingleton<IImapClient>(sp => new ImapClient(sp.GetRequiredService<IProtocolLogger>()));
        services.AddSingleton<IProtocolLogger>(sp =>
        {
            if (HostEnvironment.IsDevelopment())
            {
                return new ProtocolLogger(Console.OpenStandardOutput());
            }
            else
            {
                return new ProtocolLogger(Stream.Null);
            }
        });
        services.AddOptions<LoginInfo>().Configure<IConfiguration>((options, config) =>
        {
            config.GetSection("LoginInfo").Bind(options);
        });
        services.AddOptions();

        // AzureImageML
        services.AddScoped<AzureImageMLApi>();

        // Database
        services.AddDbContext<BlazorDbContext>(options =>
        {
            if (Configuration["Database:Provider"] == "Sql")
            {
                options.UseSqlServer(Configuration.GetConnectionString("Sql"), options =>
                {
                    options.EnableRetryOnFailure();
                });
            }
            else if (Configuration["Database:Provider"] == "Sqlite")
            {
                options.UseSqlite(Configuration.GetConnectionString("Sqlite"));
            }
            else
            {
                options.UseInMemoryDatabase("InMemory");
            }
        });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider, ILogger<UseForwardedHeadersMethod> logger)
    {
        if (Configuration["Database:Provider"] == "Sql" || Configuration["Database:Provider"] == "Sqlite")
{
            var database = serviceProvider.GetRequiredService<BlazorDbContext>();

            database.Database.Migrate();
        }

        app.AddReverseProxyPathOptions(Configuration);

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
}

        app.UseForwardedHeadersRulesIfEnabled(logger, Configuration);

        app.Use(async (context, next) =>
        {
            context.Response.Headers.Add("X-Frame-Options", ("X-FRAME-OPTIONS") ?? "DENY");
            context.Response.Headers.Add("X-Content-Type-Options", ("X-Content-Type-Options") ?? "nosniff");
            await next();
        });

        app.UseHttpsRedirection();

        app.UseStaticFiles();

        app.UseRouting();

        app.UseSession();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapBlazorHub();
            endpoints.MapFallbackToPage("/_Host");
        });
    }
}
