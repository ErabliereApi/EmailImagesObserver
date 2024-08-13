using BlazorApp.Data;
using Florence2;
using System.Collections.Concurrent;
using System.Text.Json;

namespace BlazorApp.AzureComputerVision;

public class Florence2LocalModel : AIAlerteService
{
    private readonly ILogger<Florence2LocalModel> _logger;
    private readonly BlazorDbContext _context;

    public Florence2LocalModel(ILogger<Florence2LocalModel> logger, BlazorDbContext context, AlerteClient alerteClient)
        : base(context, logger, alerteClient)
    {
        _logger = logger;
        _context = context;
    }

    public async Task AnalyzeImageAsync(Florence2Model modelSession, Data.ImageInfo imageInfo, ConcurrentDictionary<Guid, IObserver<Data.ImageInfo>>? observer = null, CancellationToken token = default)
    {
        _logger.LogInformation("----------------------------------------------------------");
        _logger.LogInformation("ANALYZE IMAGE - ATTACHMENT");
        _logger.LogInformation(imageInfo.ToString());
        _logger.LogInformation("");

        if (imageInfo.Images == null)
        {
            _logger.LogInformation("Image is null, end function AnalyseImageAsync");
            return;
        }

        try
        {
            var results = new List<FlorenceResults>(15);

            foreach (var task in Enum.GetValues<TaskTypes>())
            {
                _logger.LogInformation("Task: {Task}", task);

                using var stream = new MemoryStream(imageInfo.Images);

                var singleResults = modelSession.Run(task, stream, textInput: "", CancellationToken.None);

                results.AddRange(singleResults);
            }
                
            var jsonResult = JsonSerializer.Serialize(results);

            imageInfo.AzureImageAPIInfo = jsonResult;
            imageInfo.AITypes += "Florence2;";

            _logger.LogInformation(jsonResult);

            _context.Update(imageInfo);

            await _context.SaveChangesAsync(token);

            if (observer is not null)
            {
                foreach (var value in observer)
                {
                    value.Value.OnNext(imageInfo);
                }
            }

            await SendAlerteAsync(imageInfo, jsonResult, token);
        }
        catch (Exception? e)
        {
            _logger.LogError(e, "Error in AzureImageMLApi: {message}", e.Message);
        }
    }
}
