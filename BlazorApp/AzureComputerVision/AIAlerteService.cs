using BlazorApp.Data;
using Microsoft.EntityFrameworkCore;

namespace BlazorApp.AzureComputerVision;

public abstract class AIAlerteService
{
    private readonly BlazorDbContext _context;
    private readonly ILogger<AIAlerteService> _logger;
    private readonly AlerteClient _alerteClient;

    public AIAlerteService(Data.BlazorDbContext context, ILogger<AIAlerteService> logger, AlerteClient alerteClient)
    {
        _context = context;
        _logger = logger;
        _alerteClient = alerteClient;
    }

    protected async Task SendAlerteAsync(ImageInfo imageInfo, string jsonResult, CancellationToken token)
    {
        var alertes = await _context.Alertes
            .Where(a => a.ExternalOwnerId == null || a.ExternalOwnerId == imageInfo.ExternalOwner.ToString())
            .ToArrayAsync(token);

        var anyAlerte = false;

        foreach (var alerte in alertes)
        {
            if (alerte.Keywords == null)
            {
                continue;
            }

            var searchJson = jsonResult;

            // first remove the RemoveKeywords for the json result
            if (alerte.RemoveKeywords != null)
            {
                var removeKeywords = alerte.RemoveKeywords.Split(';');

                foreach (var removeKeyword in removeKeywords)
                {
                    var originLength = searchJson.Length;
                    searchJson = searchJson.Replace(removeKeyword, string.Empty, StringComparison.OrdinalIgnoreCase);
                    if (originLength != searchJson.Length)
                    {
                        _logger.LogDebug("Keyword removed: {removeKeyword}", removeKeyword);
                    }
                    else
                    {
                        _logger.LogDebug("Keyword not found: {removeKeyword}", removeKeyword);
                    }
                }
            }

            var keywords = alerte.Keywords.Split(';');

            foreach (var keyword in keywords)
            {
                if (searchJson.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    await _alerteClient.SendAlertAsync(alerte, imageInfo, keyword, token);
                    anyAlerte = true;
                }
            }
        }

        if (anyAlerte)
        {
            _logger.LogInformation("Alerte was sent");
        }
        else
        {
            _logger.LogInformation("No alerte was sent");
        }
    }
}
