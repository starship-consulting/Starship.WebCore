using System;
using Microsoft.AspNetCore.Http;

namespace Starship.WebCore.Providers.Authentication {
    public class UserImpersonationInterceptor {
        
        public UserImpersonationInterceptor(IServiceProvider provider) {
        }
        
        private HttpContext Context => ContextAccessor.HttpContext;

        private readonly IHttpContextAccessor ContextAccessor;
    }
}