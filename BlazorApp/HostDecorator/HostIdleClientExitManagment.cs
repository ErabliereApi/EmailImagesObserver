using BlazorApp.AzureComputerVision;

namespace BlazorApp.HostDecorator
{
    public static class AddHostIdleClientDecorator
    {
        public static IHost WithIdleClient(this IHost host)
        {
            return new HostIdleClientExitManagment(host);
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

            _idleTask = client.RunAsync();

            return _host.StartAsync(cancellationToken);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            var client = Services.GetRequiredService<IdleClient>();

            client.Exit();

            if (_idleTask != null)
            {
                await _idleTask;
            }

            await _host.StopAsync();
        }

        private Task? _idleTask;
    }
}
