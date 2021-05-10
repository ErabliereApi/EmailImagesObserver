using System;
using System.IO;
using System.Security;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace AzureComputerVision 
{
    public class LocalDataProtection 
    {
        private readonly IDataProtector dataProtector;
        private readonly string settingsFile;

        public LocalDataProtection(IDataProtectionProvider  dataProtector)
        {
            this.dataProtector = dataProtector.CreateProtector(Program.AppName);
            this.settingsFile = Path.Combine(Program.GetBaseDirectory(), "settings.data");
        }

        public void SaveLoginInfo(LoginInfo loginInfo)
        {
            var json = JsonSerializer.Serialize(loginInfo);

            var protectedData = dataProtector.Protect(json);

            File.WriteAllText(settingsFile, protectedData);
        }

        public LoginInfo GetLoginInfo() 
        {
            if (InfoPreviouslySaved()) {
                var text = File.ReadAllText(settingsFile);

                var deserializedLoginInfo = JsonSerializer.Deserialize<LoginInfo>(dataProtector.Unprotect(text));

                if (deserializedLoginInfo is not null) return deserializedLoginInfo;
            }

            Console.WriteLine("No configuration found, please enter your configuration bellow");

            var loginInfo = new LoginInfo();

            Console.Write("AzureVisionEndpoint: ");
            loginInfo.AzureVisionEndpoint = Console.ReadLine();
            Console.Write("AzureVisionSubscriptionKey: ");
            loginInfo.AzureVisionSubscriptionKey = ReadSecureLine();
            Console.Write("Email: ");
            loginInfo.EmailLogin = Console.ReadLine();
            Console.Write("Password: ");
            loginInfo.EmailPassword = ReadSecureLine();
            Console.Write("ImapServer: ");
            loginInfo.ImapServer = Console.ReadLine();
            Console.Write("ImapPort: ");
            loginInfo.ImapPort = int.Parse(Console.ReadLine());

            SaveLoginInfo(loginInfo);

            return loginInfo;
        }

        private string ReadSecureLine()
        {
            var pass = string.Empty;
            ConsoleKey key;

            do
            {
                var keyInfo = Console.ReadKey(intercept: true);
                key = keyInfo.Key;

                if (key == ConsoleKey.Backspace && pass.Length > 0)
                {
                    //Console.Write("\b \b");
                    pass = pass[0..^1];
                }
                else if (!char.IsControl(keyInfo.KeyChar))
                {
                    //Console.Write("*");
                    pass += keyInfo.KeyChar;
                }
            } while (key != ConsoleKey.Enter);
            Console.WriteLine();

            return pass;
        }

        private bool InfoPreviouslySaved()
        {
            return File.Exists(settingsFile);
        }
    }
}