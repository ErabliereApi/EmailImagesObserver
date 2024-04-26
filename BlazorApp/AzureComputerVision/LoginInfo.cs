namespace BlazorApp.AzureComputerVision;

/// <summary>
/// Class with properties to authenticate and communicate to
/// external services
/// </summary>
/// <remarks>
/// Azure informations and email informations don't need to be related in any way.
/// </remarks>
public class LoginInfo
{
    /// <summary>
    /// The azure vision enpoint url
    /// </summary>
    public string? AzureVisionEndpoint { get; set; }

    /// <summary>
    /// The azure vision subscription key
    /// </summary>
    public string? AzureVisionSubscriptionKey { get; set; }

    /// <summary>
    /// The email address used
    /// </summary>
    public string? EmailLogin { get; set; }

    /// <summary>
    /// The email password
    /// </summary>
    public string? EmailPassword { get; set; }

    /// <summary>
    /// The imap server address
    /// </summary>
    public string? ImapServer { get; set; }

    /// <summary>
    /// The impa port
    /// </summary>
    public int ImapPort { get; set; }

    /// <summary>
    /// Set the value to true if the email alert is configured
    /// </summary>
    public bool SendEmailAlertIsConfigured { get; set; }

    /// <summary>
    /// Set the value to true if the SMS alert is configured
    /// </summary>
    public bool SendSMSAlertIsConfigured { get; set; }

    /// <summary>
    /// The email sender
    /// </summary>
    public string Sender { get; set; } = "";

    /// <summary>
    /// The smtp server address
    /// </summary>
    public string SmtpServer { get; set; } = "";

    /// <summary>
    /// The smtp port
    /// </summary>
    public int SmtpPort { get; set; } = 0;

    /// <summary>
    /// The email username
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// The email username
    /// </summary>
    public string Email { get; set; } = "";

    /// <summary>
    /// TenantId used to send email via OAuth2
    /// </summary>
    public string TenantId { get; set; } = "";

    /// <summary>
    /// Twilo number
    /// </summary>
    public string Numero { get; set; } = "";

    /// <summary>
    /// Twilo account sid
    /// </summary>
    public string AccountSid { get; set; } = "";

    /// <summary>
    /// Twilo auth token
    /// </summary>
    public string AuthToken { get; set; } = "";
}
