using Florence2;

namespace BlazorApp.Extension;

public static class ModelDownloaderExtension
{
    public static bool IsModelDownloaded(this FlorenceModelDownloader modelSource, string basePath, string modelPath)
    {
        return File.Exists(Path.Combine(basePath, modelPath));
    } 
}
