using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Microsoft.Extensions.Options;
using Starship.Web.Services;

namespace Starship.WebCore.Providers.Authentication {
    public class Auth0Provider : IsAuthenticationProvider {

        public Auth0Provider() {
        }

        /*public Auth0Provider(IOptionsMonitor<Auth0Settings> settings) {
            Settings = settings.CurrentValue;
        }*/

        public async Task<UserInfo> GetUserInfoAsync(string accessToken) {
            if (!string.IsNullOrEmpty(accessToken)) {
                var apiClient = new AuthenticationApiClient(Settings.CurrentValue.Domain);
                var userInfo = await apiClient.GetUserInfoAsync(accessToken);

                return userInfo;
            }

            return null;
        }

        public void Authenticate(AuthenticationState state) {
            Authenticated?.Invoke(state);
        }
        
        public event Action<AuthenticationState> Authenticated;

        public IOptionsMonitor<Auth0Settings> Settings;
    }
}