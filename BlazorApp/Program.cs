using System;
using AzureComputerVision;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BlazorApp
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var baseDirectory = Constant.GetBaseDirectory();

                Console.WriteLine($"Base directory : {baseDirectory}");

                var host = CreateHostBuilder(args).Build();

                var client = host.Services.GetRequiredService<IdleClient>();

                var idleTask = client.RunAsync();

                host.Run();

                client.Exit();

                idleTask.GetAwaiter().GetResult();
            }
            catch (Exception? e)
            {
                do
                {
                    Console.Error.WriteLine(e.Message);
                    Console.Error.WriteLine(e.StackTrace);

                    e = e.InnerException;
                } while (e != null);
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
