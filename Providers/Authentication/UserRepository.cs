using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Extensions;
using Starship.Web.Security;
using Starship.WebCore.Configuration;
using Starship.WebCore.Extensions;

namespace Starship.WebCore.Providers.Authentication {

    public class UserRepository {
        
        public UserRepository(IConfiguration configuration, IHttpContextAccessor context, AzureDocumentDbProvider data) {
            Settings = ConfigurationMapper.Map<ClientSettings>(configuration);
            Context = context.HttpContext;
            Data = data;
        }
        
        public UserProfile GetUserProfile() {

            var profile = Context.User.GetUserProfile();

            if(Context.Session.Keys.Contains("impersonate")) {
                var account = Data.GetAccount(profile.Email);

                if(account != null && account.IsAdmin()) {
                    var impersonate = Context.Session.GetString("impersonate");
                    profile = new UserProfile(impersonate);
                }
            }
            
            return profile;
        }

        public Account GetAccount() {
            var profile = GetUserProfile();

            if(profile.Email.IsEmpty()) {
                return new Account { Id = profile.Id };
            }

            return Data.GetAccount(profile.Email);
        }

        public ClientSettings GetSettings() {
            return Settings.Clone();
        }

        private ClientSettings Settings { get; set; }

        private AzureDocumentDbProvider Data { get; set; }

        private HttpContext Context { get; set; }
    }
}