using System.Collections.Concurrent;
using BlazorApp.Extension;
using BlazorApp.Data;
using Florence2;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using BlazorApp.Model;

namespace BlazorApp.AzureComputerVision;

public class AIAnalysisQueue : IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<LoginInfo> _loginInfo;
    private readonly ILogger<AIAnalysisQueue> _logger;
    private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private bool _disposed;

    public AIAnalysisQueue(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        IOptions<LoginInfo> loginInfo,
        ILogger<AIAnalysisQueue> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _loginInfo = loginInfo;
        _logger = logger;
    }

    private readonly ConcurrentQueue<long> _queue = new ConcurrentQueue<long>();

    public async Task BackgroundProcessAsync(CancellationToken token)
    {
        while (!_cancellationTokenSource.IsCancellationRequested || !token.IsCancellationRequested)
        {
            try
            {
                if (_queue.TryDequeue(out var id))
                {
                    using var scope = _serviceProvider.CreateScope();

                    var db = scope.ServiceProvider.GetRequiredService<BlazorDbContext>();

                    var image = await db.ImagesInfo
                        .FirstOrDefaultAsync(i => i.Id == id, token);

                    if (image != null)
                    {
                        if (_configuration.UseFlorence2AI())
                        {
                            var modelSession = scope.ServiceProvider.GetRequiredService<Florence2Model>();
                            var florence2 = scope.ServiceProvider.GetRequiredService<Florence2LocalModel>();

                            await florence2.AnalyzeImageAsync(modelSession, image, null, _cancellationTokenSource.Token);
                        }
                        else if (_configuration.UseAzureVision())
                        {
                            var azureVision = scope.ServiceProvider.GetRequiredService<AzureVisionApi>();

                            var client = AzureVisionApi.Authenticate(_loginInfo.Value);

                            await azureVision.AnalyzeImageAsync(client, image, null, _cancellationTokenSource.Token);
                        }
                        else
                        {
                            var azureComputerVision = scope.ServiceProvider.GetRequiredService<AzureImageMLApi>();

                            var client = AzureImageMLApi.Authenticate(_loginInfo.Value);

                            await azureComputerVision.AnalyzeImageAsync(client, image, null, _cancellationTokenSource.Token);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing AI analysis queue. {Message} {StackTrace}", ex.Message, ex.StackTrace);
            }


            await Task.Delay(1000, _cancellationTokenSource.Token);
        }
    }

    public void Enqueue(long id)
    {
        _queue.Enqueue(id);
    }

    public void Exit()
    {
        _cancellationTokenSource.Cancel();
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cancellationTokenSource.Dispose();
            }

            _disposed = true;
        }
    }

    ~AIAnalysisQueue()
    {
        Dispose(false);
    }
}