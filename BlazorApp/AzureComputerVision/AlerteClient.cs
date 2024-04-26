using System.Net.Mail;
using BlazorApp.AzureComputerVision;
using BlazorApp.Services;
using Microsoft.Extensions.Options;
using MimeKit;

/// <summary>
/// Client for sending alerts
/// </summary>
public class AlerteClient
{
    private readonly ILogger<AlerteClient> logger;
    private readonly IEmailService emailService;
    private readonly ISMSService smsService;
    private readonly LoginInfo config;


    public AlerteClient(ILogger<AlerteClient> logger, 
        IEmailService emailService, 
        ISMSService smsService, 
        IOptions<LoginInfo> config)
    {
        this.logger = logger;
        this.emailService = emailService;
        this.smsService = smsService;
        this.config = config.Value;
    }

    /// <summary>
    /// Send an alert using both email and SMS
    /// </summary>
    public async Task SendAlertAsync(Alerte alerte, object _donnee, CancellationToken token)
    {
        await SendEmailAlert(alerte, _donnee, token);
        await SendSMSAlert(alerte, _donnee, token);
    }

    private async Task SendEmailAlert(Alerte alerte, object _donnee, CancellationToken token)
    {
        if (!config.SendEmailAlertIsConfigured)
        {
            logger.LogWarning("Les configurations ne courriel ne sont pas initialisé, la fonctionnalité d'alerte ne peut pas fonctionner.");

            return;
        }

        try
        {
            if (alerte.SendTo != null)
            {
                var mailMessage = new MimeMessage();
                mailMessage.From.Add(new MailboxAddress("ErabliereAPI - Alerte Service", config.Sender));
                foreach (var destinataire in alerte.SendTo.Split(';'))
                {
                    mailMessage.To.Add(MailboxAddress.Parse(destinataire));
                }
                mailMessage.Subject = $"Alerte ID : {alerte.Id}";
                mailMessage.Body = new TextPart("plain")
                {
                    Text = FormatTextMessage(alerte, _donnee)
                };

                await emailService.SendEmailAsync(mailMessage, CancellationToken.None);
            }
        }
        catch (Exception e)
        {
            logger.LogCritical(new EventId(92837486, "TriggerAlertV2Attribute.TriggerAlerte"), e, "Une erreur imprévue est survenu lors de l'envoie de l'alerte.");
        }
    }

    private async Task SendSMSAlert(Alerte alerte, object _donnee, CancellationToken token)
    {
        if (!config.SendSMSAlertIsConfigured)
        {
            logger.LogWarning("Les configurations de SMS ne sont pas initialisé, la fonctionnalité d'alerte ne peut pas fonctionner.");

            return;
        }

        try
        {
            if (alerte.TextTo != null)
            {
                var message = FormatTextMessage(alerte, _donnee);

                foreach (var destinataire in alerte.TextTo.Split(';'))
                {
                    await smsService.SendSMSAsync(message, destinataire, CancellationToken.None);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogCritical(new EventId(92837486, "TriggerAlertAttribute.TriggerAlerte"), e, "Une erreur imprévue est survenu lors de l'envoie de l'alerte.");
        }
    }

    private string FormatTextMessage(Alerte alerte, object donnee)
    {
        return $"Alerte ID : {alerte.Id}\n{alerte.Title}\n{donnee}";
    }
}