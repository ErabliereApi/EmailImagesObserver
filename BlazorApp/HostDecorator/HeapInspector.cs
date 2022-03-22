using Microsoft.Diagnostics.Runtime;
using System.Diagnostics;

namespace BlazorApp;

public static class AddHostHeapInspectorDecorator
{
    public static IHost WithHeapInspector(this IHost host)
    {
        return new HeapInspector(host);
    }
}

public class HeapInspector : IHost
{
    private IHost _host;

    public HeapInspector(IHost host)
    {
        _host = host;
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
                        Console.WriteLine(obj.ToString());
                    }

                    await Task.Delay(1000, cancellationToken);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
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
