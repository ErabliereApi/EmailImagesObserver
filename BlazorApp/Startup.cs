using BlazorApp.Services;
using BlazorApp.AzureComputerVision;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication.Cookies;
using BlazorApp.Data;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Imap;
using MailKit;

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
        /// Blazor app
        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddScoped<ImageInfoService>();

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie();

        /// IdleClient
        services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Constant.AppName)))
                .SetApplicationName(Constant.AppName);
        services.AddSingleton<LocalDataProtection>();
        services.AddSingleton<IdleClient>();
        services.AddSingleton(sp => new ImapClient(sp.GetRequiredService<IProtocolLogger>()));
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

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
    {
        if (Configuration["Database:Provider"] == "Sql")
{
            var database = serviceProvider.GetRequiredService<BlazorDbContext>();

            database.Database.Migrate();
        }

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();

        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapBlazorHub();
            endpoints.MapFallbackToPage("/_Host");
        });
    }
}
