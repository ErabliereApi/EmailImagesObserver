using BlazorApp.Services;
using BlazorApp.AzureComputerVision;
using BlazorApp.Data;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Imap;
using MailKit;
using BlazorApp.Extension;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Identity.Web.UI;
using Microsoft.Extensions.Logging.Console;
using MailKit.Net.Smtp;
using Florence2;
using BlazorApp.Model;
using BlazorApp.Notification;
using BlazorApp.ComputerVision;

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
        services.AddLogging(config =>
        {
            config.AddConsoleFormatter<AvoidLongTextFormatter, ConsoleFormatterOptions>();
            config.AddConsole(options =>
            {
                options.FormatterName = "AvoidLongTextFormatter";
            });
        })
        .Configure<ConsoleLoggerOptions>(options =>
        {
            options.FormatterName = "AvoidLongTextFormatter";
        })
        .Configure<ConsoleFormatterOptions>(options => 
        {
            
        });

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

        services.AddEmailImageObserverAutorisation(Configuration);

        services.AddDistributedCaching(Configuration);

        services.AddSession();

        /// IdleClient
        services.AddSingleton<IdleClient>();
        services.AddSingleton<IImapClient>(sp => new ImapClient(sp.GetRequiredService<IProtocolLogger>()));
        services.AddSingleton<IProtocolLogger>(sp =>
        {
            if (HostEnvironment.IsDevelopment() && string.Equals(Configuration["Logging:LogLevel:Default"], "Debug", StringComparison.OrdinalIgnoreCase))
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
        services.AddScoped<AzureImageMLApi>(); // V1
        services.AddScoped<AzureVisionApi>(); // V2
        services.AddSingleton<AlerteClient>();
        services.AddSingleton<ISmtpClient>(sp => new SmtpClient(sp.GetRequiredService<IProtocolLogger>()));
        services.AddSingleton<IEmailService, ErabliereApiEmailService>();
        services.AddSingleton<ISMSService, TwilioSMSService>();

        // Florence2
        services.AddSingleton(sp => new FlorenceModelDownloader("./models"));
        services.AddSingleton(sp => new Florence2Model(sp.GetRequiredService<FlorenceModelDownloader>()));
        services.AddTransient<Florence2LocalModel>();

        // Ai background worker
        services.AddSingleton<AIAnalysisQueue>();

        services.AddSingleton<CustomLocalModel>(new CustomLocalModel(Configuration["CUSTOM_LOCAL_MODEL"]));

        // Database
        services.AddDbContext<BlazorDbContext>(options =>
        {
            if (Configuration.DatabaseProvider() == "Sql")
            {
                options.UseSqlServer(Configuration.GetConnectionString("Sql"), options =>
                {
                    options.EnableRetryOnFailure();
                });
            }
            else if (Configuration.DatabaseProvider() == "Sqlite")
            {
                options.UseSqlite(Configuration.GetConnectionString("Sqlite"));
            }
            else
            {
                options.UseInMemoryDatabase("InMemory");
            }
        });

        // Timezone
        services.AddScoped<TimezoneService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider, ILogger<UseForwardedHeadersMethod> logger)
    {
        if (Configuration.DatabaseProvider() == "Sql" || Configuration.DatabaseProvider() == "Sqlite")
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
            context.Response.Headers.Append("X-Frame-Options", ("X-FRAME-OPTIONS") ?? "DENY");
            context.Response.Headers.Append("X-Content-Type-Options", ("X-Content-Type-Options") ?? "nosniff");
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
            endpoints.MapControllers();
            endpoints.MapBlazorHub();
            endpoints.MapFallbackToPage("/_Host");
        });
    }
}
