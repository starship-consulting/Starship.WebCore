using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Auth0.AuthenticationApi;
using Auth0.AuthenticationApi.Models;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using RestSharp;
using Starship.Web.Services;

namespace Starship.WebCore.Providers.Authentication {
    public class Auth0Provider : IsAuthenticationProvider {

        public Auth0Provider() {
        }

        /*public Auth0Provider(IOptionsMonitor<Auth0Settings> settings) {
            Settings = settings.CurrentValue;
        }*/

        private string GetAccessToken() {
            var client = new RestClient(Settings.TokenUrl);
            var request = new RestRequest(Method.POST);
            request.AddHeader("content-type", "application/json");
            
            request.AddParameter("application/json", JsonConvert.SerializeObject(new {
                client_id = Settings.ClientId,
                client_secret = Settings.ClientSecret,
                audience = Settings.Audience,
                grant_type = "client_credentials"
            }), ParameterType.RequestBody);

            var response = client.Execute(request);
            
            var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(response.Content);
            return json["access_token"].ToString();
        }

        private ManagementApiClient GetManagementClient() {
            var token = GetAccessToken();
            return new ManagementApiClient(token, Settings.Audience);
        }

        public void ValidateEmail(string email) {
            var client = GetManagementClient();
            // What if user already registered the other email?
            //client.Jobs.SendVerificationEmailAsync(new VerifyEmailJobRequest{ UserId = });
        }

        public async Task<UserInfo> GetUserInfoAsync(string accessToken) {
            if (!string.IsNullOrEmpty(accessToken)) {
                var apiClient = new AuthenticationApiClient(Settings.Domain);
                var userInfo = await apiClient.GetUserInfoAsync(accessToken);

                return userInfo;
            }

            return null;
        }

        public void Authenticate(AuthenticationState state) {
            Authenticated?.Invoke(state);
        }
        
        public event Action<AuthenticationState> Authenticated;

        public IOptionsMonitor<Auth0Settings> SettingsMonitor;

        private Auth0Settings Settings => SettingsMonitor.CurrentValue;
    }
}