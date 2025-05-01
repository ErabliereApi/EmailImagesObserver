using BlazorApp.AzureComputerVision;
using BlazorApp.Model;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace BlazorApp.Services;

/// <summary>
/// Interface permettant d'abstraire l'envoie des SMS
/// </summary>
public interface ISMSService
{
    /// <summary>
    /// Envoie un SMS
    /// </summary>
    /// <param name="message"></param>
    /// <param name="destinataire"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    Task SendSMSAsync(string message, string destinataire, CancellationToken token);
}

/// <summary>
/// Implementation of <see cref="ISMSService" /> using Twilio
/// </summary>
public class TwilioSMSService : ISMSService
{
    private readonly LoginInfo _smsConfig;
    private readonly ILogger<TwilioSMSService> _logger;

    /// <summary>
    /// Constructeur par défaut
    /// </summary>
    /// <param name="smsConfig"></param>
    /// <param name="logger"></param>
    public TwilioSMSService(
        IOptions<LoginInfo> smsConfig,
        ILogger<TwilioSMSService> logger)
    {
        _smsConfig = smsConfig.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendSMSAsync(string message, string destinataire, CancellationToken token)
    {
        try
        {
            _logger.LogInformation("Begin sending SMS...");

            string? numero = _smsConfig.Numero;
            string? accountSid = _smsConfig.AccountSid;
            string? authToken = _smsConfig.AuthToken;

            TwilioClient.Init(accountSid, authToken);

            await MessageResource.CreateAsync(
                body: message,
                from: new PhoneNumber(numero),
                to: new PhoneNumber(destinataire)
            );

            _logger.LogInformation("SMS sent successfully");
        }
        catch (Exception e)
        {
            _logger.LogCritical(e, e.Message);
        }
    }
}