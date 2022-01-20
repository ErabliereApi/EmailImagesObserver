using System.Runtime.InteropServices;

namespace AzureComputerVision;

/// <summary>
/// Constante class
/// </summary>
public static class Constant
{
    /// <summary>
    /// Application name
    /// </summary>
    public const string AppName = "EmailImagesObserver";

    /// <summary>
    /// Base directory
    /// </summary>
    /// <returns></returns>
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
