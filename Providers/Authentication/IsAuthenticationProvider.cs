using System;
using System.Threading.Tasks;
using Auth0.AuthenticationApi.Models;

namespace Starship.WebCore.Providers.Authentication {
    public interface IsAuthenticationProvider {

        Task<UserInfo> GetUserInfoAsync(string accessToken);

        event Action<AuthenticationState> Authenticated;
    }
}