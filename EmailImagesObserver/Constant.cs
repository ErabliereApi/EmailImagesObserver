using System;
using System.IO;
using System.Runtime.InteropServices;

namespace AzureComputerVision
{
    public static class Constant
    {
        public const string AppName = "EmailImagesObserver";

        public static string GetBaseDirectory()
        {
            // Get the baseDirectory base directory
            string? baseDirectory;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Constant.AppName);
            }
            else
            {
                baseDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Constant.AppName.Replace("Em", ".em"));
            }

            return baseDirectory;
        }
    }
}
