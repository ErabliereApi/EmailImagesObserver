namespace AzureComputerVision
{
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
        public string? AzureVisionEndpoint {get;set;}

        /// <summary>
        /// The azure vision subscription key
        /// </summary>
        public string? AzureVisionSubscriptionKey {get;set;}

        /// <summary>
        /// The email address used
        /// </summary>
        public string? EmailLogin {get;set;}

        /// <summary>
        /// The email password
        /// </summary>
        public string? EmailPassword {get;set;}

        /// <summary>
        /// The imap server address
        /// </summary>
        public string? ImapServer {get;set;}

        /// <summary>
        /// The impa port
        /// </summary>
        public int ImapPort {get;set;}
    }
}