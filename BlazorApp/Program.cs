using BlazorApp;
using BlazorApp.Extension;
using BlazorApp.HostDecorator;

Console.Out.WriteLine($"[INF] {DateTime.Now} Début de EmailImagesObserver");

Console.WriteLine("ASPNETCORE_ENVIRONMENT: " + Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"));

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

    host.Run();
}
catch (Exception? e)
{
    Console.Error.WriteLine(e);
}
finally
{
    Console.Out.WriteLine($"[INF] {DateTime.Now} Fin de EmailImagesObserver");
}