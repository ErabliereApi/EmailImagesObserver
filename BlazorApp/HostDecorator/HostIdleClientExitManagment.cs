using BlazorApp.AzureComputerVision;
using BlazorApp.Extension;
using Florence2;

namespace BlazorApp.HostDecorator;

public static class AddHostIdleClientDecorator
{
    public static IHost WithIdleClient(this IHost host)
    {
        return new HostIdleClientExitManagment(host);
    }

    public static IHost WithFlorenceAI(this IHost host)
    {
        return new HostFlorenceAI(host);
    }
}

public class HostIdleClientExitManagment : IHost
{
    private readonly IHost _host;

    public HostIdleClientExitManagment(IHost host)
    {
        _host = host;
    }

    public IServiceProvider Services => _host.Services;

    public void Dispose()
    {
        _host.Dispose();
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var client = Services.GetRequiredService<IdleClient>();

        var host = _host.StartAsync(cancellationToken);

        _idleTask = client.RunAsync(cancellationToken);

        return host;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var client = Services.GetRequiredService<IdleClient>();

        client.Exit();

        if (_idleTask != null)
        {
            await _idleTask;
        }

        await _host.StopAsync(cancellationToken);
    }

    private Task? _idleTask;
}

public class HostFlorenceAI : IHost
{
    private readonly IHost _host;
    private Task? _modelsDownload;

    public HostFlorenceAI(IHost host) 
    {
        _host = host;
    }

    public IServiceProvider Services => _host.Services;

    public void Dispose()
    {
        _host.Dispose();
    }

    private readonly string basePath = "./models";
    private readonly string decoder = "decoder_model_merged.onnx";
    private readonly string embeded = "embed_tokens.onnx";
    private readonly string encoder = "encoder_model.onnx";
    private readonly string vision = "vision_encoder.onnx";

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        var modelSource = new FlorenceModelDownloader(basePath);

        var logger = Services.GetRequiredService<ILogger<HostFlorenceAI>>();

        if (!modelSource.IsModelDownloaded(basePath, decoder) || !modelSource.IsModelDownloaded(basePath, embeded) || !modelSource.IsModelDownloaded(basePath, encoder) || !modelSource.IsModelDownloaded(basePath, vision))
        {
            _modelsDownload = modelSource.DownloadModelsAsync(status =>
            {
                logger.LogInformation("{Progress} {Error} {Message}", status.Progress, status.Error, status.Message);
            });
        }

        return _host.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _host.StopAsync(cancellationToken);
    }
}