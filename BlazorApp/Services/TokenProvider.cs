namespace BlazorApp.Services;

/// <summary>
/// This class is use to keep the Xsrf token
public class TokenProvider
{
    /// <summary>
    /// The Xsrf token
    /// </summary>
    public string? XsrfToken { get; set; }

    /// <summary>
    /// The cookie name
    /// </summary>
    public string? Cookie { get; set; }
}

/// <summary>
/// This class is use to keep the Xsrf token
/// </summary>
public class InitialApplicationState
{
    /// <summary>
    /// The Xsrf token
    /// </summary>
    public string? XsrfToken { get; set; }

    /// <summary>
    /// The cookie name
    /// </summary>
    public string? Cookie { get; set; }
}