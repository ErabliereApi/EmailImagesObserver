using BlazorApp.Data;
using Florence2;
using System.Collections.Concurrent;
using System.Text.Json;

namespace BlazorApp.AzureComputerVision;

public class Florence2LocalModel : AIAlerteService
{
    private readonly ILogger<Florence2LocalModel> _logger;
    private readonly BlazorDbContext _context;
    private readonly IConfiguration _config;

    public Florence2LocalModel(ILogger<Florence2LocalModel> logger, BlazorDbContext context, AlerteClient alerteClient, IConfiguration config)
        : base(context, logger, alerteClient)
    {
        _logger = logger;
        _context = context;
        _config = config;
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

        List<TaskTypes> tasks = GetFlorence2TaskTypesConfigure();

        try
        {
            var results = new List<FlorenceResults>(15);

            int i = 1;
            foreach (var task in tasks)
            {
                _logger.LogInformation("Task {I}: {Task}", i++, task);

                using var stream = new MemoryStream(imageInfo.Images);

                var singleResults = modelSession.Run(task, stream, textInput: "", CancellationToken.None);

                if (singleResults == null || singleResults.Length == 0)
                {
                    _logger.LogInformation("No results produce for TaskTypes: {TaskTypes}", task);
                }
                else
                {
                    results.AddRange(singleResults);
                }
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

    private List<TaskTypes> GetFlorence2TaskTypesConfigure()
    {
        var tasks = new List<TaskTypes>();

        try
        {
            var configTypes = _config.GetValue<string>("USE_FLORENCE2_TASKTYPES")?.Split(',');

            if (configTypes != null)
            {
                foreach (var t in configTypes)
                {
                    if (Enum.TryParse<TaskTypes>(t, out var result))
                    {
                        tasks.Add(result);
                    }
                }
            }
            else
            {
                tasks.AddRange(Enum.GetValues<TaskTypes>());
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in GetFlorence2TaskTypesConfigure: {message}", e.Message);
            tasks.AddRange(Enum.GetValues<TaskTypes>());
        }

        return tasks;
    }
}
