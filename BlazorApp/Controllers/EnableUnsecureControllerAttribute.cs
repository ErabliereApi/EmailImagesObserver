using BlazorApp.Extension;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BlazorApp.Controllers;

public class EnableUsecureControllerAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // If the configuration EnableUnsecureController is set to true, then the controller is going to be accessible without any authentication.
        if (AddAuthentificationExtension.IsUnsecureControllerEnable(context.HttpContext.RequestServices))
        {
            await next();
        }
        else
        {
            // the header for a reason
            context.HttpContext.Response.Headers.Append("X-EmailImageObserver-AuthFailure", "Unsecure controller are disabled");

            context.Result = new UnauthorizedResult();
        }
    }
}