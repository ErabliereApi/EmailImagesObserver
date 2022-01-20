using AzureComputerVision;
using BlazorApp.HostDecorator;

namespace BlazorApp;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            var baseDirectory = Constant.GetBaseDirectory();

            Console.WriteLine($"Base directory : {baseDirectory}");

            var host = CreateHostBuilder(args).Build().WithIdleClient();

            host.Run();
        }
        catch (Exception? e)
        {
            LogException(e);
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });

    public static void LogException(Exception? exception)
    {
        while (exception != null)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(exception.StackTrace);

            exception = exception.InnerException;
        } 
    }
}
