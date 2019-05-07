using System;
using System.Security.Claims;

namespace Starship.WebCore.Providers.Authentication {
    public interface IsAuthenticationProvider {

        event Action<ClaimsPrincipal> Authenticated;
    }
}