using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;

namespace Starship.WebCore.Providers.Authentication {
    public class ExpandedAuthorizationPolicyProvider : IAuthorizationPolicyProvider {

        public Task<AuthorizationPolicy> GetPolicyAsync(string policyName) {
            return GetDefaultPolicyAsync();
        }

        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() {
            return Task.FromResult(new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(CookieAuthenticationDefaults.AuthenticationScheme, JwtBearerDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build());
        }
    }
}