using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Web.Security;
using Starship.WebCore.Configuration;
using Starship.WebCore.Extensions;

namespace Starship.WebCore.Providers.Authentication {

    public class UserRepository {
        
        public UserRepository(IConfiguration configuration, IHttpContextAccessor context, AzureDocumentDbProvider data) {
            Settings = ConfigurationMapper.Map<ClientSettings>(configuration);
            Principal = context.HttpContext.User;
            Data = data;
        }
        
        public UserProfile GetUserProfile() {

            var profile = Principal.GetUserProfile();

            if(Settings.HasImpersonationEmail()) {
                profile = new UserProfile(Settings.GetImpersonationEmail());
            }

            return profile;
        }

        public Account GetAccount() {
            return Data.GetAccount(GetUserProfile().Email);
        }

        public ClientSettings GetSettings() {
            return Settings;
        }
        
        private ClaimsPrincipal Principal { get; set; }

        private ClientSettings Settings { get; set; }

        private AzureDocumentDbProvider Data { get; set; }
    }
}