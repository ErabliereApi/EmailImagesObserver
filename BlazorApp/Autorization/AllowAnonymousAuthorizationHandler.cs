using Microsoft.AspNetCore.Authorization;

namespace BlazorApp.Autorization;

/// <summary>
/// Handler use when no authentification is configured
/// </summary>
public class AllowAnonymousAuthorizationHandler : IAuthorizationHandler
{
    /// <inheritdoc />
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        foreach (var requirement in context.PendingRequirements)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
