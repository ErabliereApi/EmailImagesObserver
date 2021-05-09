using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace AzureComputerVision
{
    class Program
    {
        public const string AppName = "EmailImagesObserver";

        static void Main(string[] args)
        {
            var baseDirectory = GetBaseDirectory();

            Console.WriteLine($"Base directory : {baseDirectory}");

            // Get DataProtection
            var services = new ServiceCollection();

            services.AddDataProtection()
                    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName)))
                    .SetApplicationName(AppName);

            services.AddSingleton<LocalDataProtection>();

            var builder = services.BuildServiceProvider();

            var dataProtector = builder.GetRequiredService<LocalDataProtection>();

            var config = dataProtector.GetLoginInfo();

            using (var client = new IdleClient(config, baseDirectory)) 
            {
                Console.WriteLine("Hit any key to end the observer.");

                var idleTask = client.RunAsync();

                Task.Run(() => 
                {
                    Console.ReadKey(true);
                }).Wait();

                client.Exit();

                idleTask.GetAwaiter().GetResult();
            }
        }

        public static string GetBaseDirectory()
        {
            // Get the baseDirectory base directory
            var baseDirectory = string.Empty;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) 
            {
                baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppName);
            }
            else 
            {
                baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), AppName.Replace("Em", ".em"));
            }

            return baseDirectory;
        }
    }
}
