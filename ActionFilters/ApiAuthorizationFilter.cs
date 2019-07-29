using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.ActionFilters {

    public class ApiAuthorizationFilter : ActionFilterAttribute {

        public override void OnActionExecuting(ActionExecutingContext context) {

            var accountManager = context.HttpContext.RequestServices.GetService<AccountManager>();
            var profile = accountManager.GetUserProfile();
            
            if(!profile.IsAuthenticated) {
                context.Result = new ContentResult {
                    Content = "Not authenticated.",
                    ContentType = "text/plain",
                    StatusCode = StatusCodes.Status401Unauthorized
                };
            }
        }
    }
}