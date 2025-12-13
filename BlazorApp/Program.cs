using System.Reflection;
using BlazorApp;
using BlazorApp.Extension;
using BlazorApp.HostDecorator;

Console.WriteLine($"[INF] {DateTime.Now} Début de EmailImagesObserver");
Console.WriteLine("ASPNETCORE_ENVIRONMENT: " + Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));
Console.WriteLine("TimeZone: " + TimeZoneInfo.Local);
var versions = Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
var versionArray = versions?.Split('+');
Console.WriteLine("Version: " + versionArray?[0]);
if (versionArray?.Length > 1)
{
    Console.WriteLine("Commit: " + versionArray[1]);
}

try
{
    var host = Host.CreateDefaultBuilder(args)
                   .ConfigureAppConfiguration(app =>
                   {
                       app.AddUserSecrets(typeof(Startup).Assembly)
                          .AddCommandLine(args);
                   })
                   .ConfigureWebHostDefaults(webBuilder =>
                   {
                       webBuilder.UseStaticWebAssets();
                       webBuilder.UseStartup<Startup>();
                   })
                   .Build()
                   .WithIdleClient();

    var config = host.Services.GetRequiredService<IConfiguration>();

    if (config.UseFlorence2AI())
    {
        host = host.WithFlorenceAI();
    }

    await host.RunAsync();
}
catch (Exception? e)
{
    Console.Error.WriteLine(e);
}
finally
{
    Console.WriteLine($"[INF] {DateTime.Now} Fin de EmailImagesObserver");
}