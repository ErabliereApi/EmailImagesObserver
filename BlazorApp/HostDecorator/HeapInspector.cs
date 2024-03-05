using Microsoft.Diagnostics.Runtime;
using System.Diagnostics;

namespace BlazorApp;

public static class AddHostHeapInspectorDecorator
{
    public static IHost WithHeapInspector(this IHost host)
    {
        return new HeapInspector(host, host.Services.GetRequiredService<ILogger<HeapInspector>>());
    }
}

public class HeapInspector : IHost
{
    private IHost _host;
    private readonly ILogger<HeapInspector> _logger;

    public HeapInspector(IHost host, ILogger<HeapInspector> logger)
    {
        _host = host;
        _logger = logger;
    }

    public IServiceProvider Services => _host.Services;

    public void Dispose()
    {
        _host.Dispose();
    }

    Task? _backgroup;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _backgroup = Task.Run(async () =>
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                try
                {
                    var target = DataTarget.CreateSnapshotAndAttach(Process.GetCurrentProcess().Id);

                    var heap = default(ClrHeap);

                    foreach (var obj in heap?.EnumerateObjects().Where(obj => GC.GetGeneration(obj) == 2) ?? Array.Empty<ClrObject>())
                    {
                        _logger.LogInformation(obj.ToString());
                    }

                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, e.Message);
                }
            }
            
        }, cancellationToken);

        return _host.StartAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return _host.StopAsync(cancellationToken);
    }
}
