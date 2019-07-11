using System;
using System.Security.Claims;
using Starship.Azure.Data;

namespace Starship.WebCore.Providers.Authentication {
    public class AuthenticationState {
        
        public AuthenticationState() {
        }

        public AuthenticationState(ClaimsPrincipal principal, string redirectUri) {
            Principal = principal;
            RedirectUri = redirectUri;
        }
        
        public Account Account { get; set; }

        public ClaimsPrincipal Principal { get; set; }

        public string RedirectUri { get; set; }
    }
}