using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AzureComputerVision;
using Microsoft.AspNetCore.DataProtection;
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

                // Get DataProtection
                var services = new ServiceCollection();

                services.AddDataProtection()
                        .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Constant.AppName)))
                        .SetApplicationName(Constant.AppName);

                services.AddSingleton<LocalDataProtection>();

                var builder = services.BuildServiceProvider();

                var dataProtector = builder.GetRequiredService<LocalDataProtection>();

                var config = dataProtector.GetLoginInfo();

                using var client = new IdleClient(config, baseDirectory);
                var idleTask = client.RunAsync();

                Task.Run(() =>
                {
                    var hostBuilder = CreateHostBuilder(args);

                    hostBuilder.Properties.Add(typeof(IdleClient), client);

                    hostBuilder.Build().Run();
                }).Wait();

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
