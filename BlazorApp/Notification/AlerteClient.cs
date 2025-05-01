using BlazorApp.Model;
using BlazorApp.Services;
using Microsoft.Extensions.Options;
using MimeKit;

namespace BlazorApp.Notification;

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
    public async Task SendAlertAsync(Alerte alerte, object _donnee, string? keyword, CancellationToken token)
    {
        await SendEmailAlert(alerte, _donnee, keyword, token);
        await SendSMSAlert(alerte, _donnee, keyword, token);
    }

    private async Task SendEmailAlert(Alerte alerte, object _donnee, string? keyword, CancellationToken token)
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
                    Text = FormatTextMessage(alerte, _donnee, keyword)
                };

                await emailService.SendEmailAsync(mailMessage, CancellationToken.None);
            }
        }
        catch (Exception e)
        {
            logger.LogCritical(new EventId(92837486, "AlerteClient.SendEmailAlert"), e, "Une erreur imprévue est survenu lors de l'envoie de l'alerte. {message} {stack}", e.Message, e.StackTrace);
        }
    }

    private async Task SendSMSAlert(Alerte alerte, object _donnee, string? keyword, CancellationToken token)
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
                var message = FormatTextMessage(alerte, _donnee, keyword);

                foreach (var destinataire in alerte.TextTo.Split(';'))
                {
                    await smsService.SendSMSAsync(message, destinataire, CancellationToken.None);
                }
            }
        }
        catch (Exception e)
        {
            logger.LogCritical(new EventId(92837486, "AlerteClient.SendSMSAlert"), e, "Une erreur imprévue est survenu lors de l'envoie de l'alerte. {message} {stack}", e.Message, e.StackTrace);
        }
    }

    private string FormatTextMessage(Alerte alerte, object donnee, string? keyword)
    {
        return $"Alerte ID : {alerte.Id}\n{alerte.Title}\n{donnee}\nKeyword: {keyword}";
    }
}