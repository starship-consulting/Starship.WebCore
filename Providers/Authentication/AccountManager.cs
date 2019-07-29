using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Extensions;
using Starship.Web.Security;
using Starship.WebCore.Configuration;
using Starship.WebCore.Extensions;
using Starship.WebCore.Interfaces;

namespace Starship.WebCore.Providers.Authentication {
    public class AccountManager : IDisposable {
        
        public AccountManager(IServiceProvider provider) {
            Data = provider.GetService<AzureCosmosDbProvider>();
            ContextAccessor = provider.GetService<IHttpContextAccessor>();
            Authentication = provider.GetService<IsAuthenticationProvider>();
            Authentication.Authenticated += OnAuthenticated;

            ClientSettingsProviders = new List<IsClientSettingsProvider>();
        }
        
        public void Dispose() {
            if(Authentication != null) {
                Authentication.Authenticated -= OnAuthenticated;
            }
        }
        
        public Account GetAccount(UserProfile profile) {
            return profile.Email.IsEmpty() ? GetAccountById(profile.Id) : GetAccountByEmail(profile.Email);
        }

        public Account GetAccountByEmail(string email) {
            email = email.ToLower();
            return Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account" && each.Email.ToLower() == email).ToList().FirstOrDefault();
        }

        public Account GetAccountById(string id) {
            return Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account" && each.Id == id).ToList().FirstOrDefault();
        }

        public Account GetAccount(ClaimsPrincipal principal) {
            return GetAccount(principal.GetUserProfile());
        }

        public Account GetAccount() {
            return GetAccount(GetUserProfile());
        }

        public UserProfile GetUserProfile() {

            var profile = Context.User.GetUserProfile();
            
            if(Context.Session != null && Context.Session.Keys.Contains(UserImpersonationKey)) {
                var account = GetAccount(profile);

                if(account != null && account.IsAdmin()) {
                    var impersonate = Context.Session.GetString(UserImpersonationKey);
                    profile = new UserProfile(impersonate);
                }
            }
            
            return profile;
        }

        public ClientSettings GetSettings() {

            var profile = GetUserProfile();
            var settings = new ClientSettings();
            
            if(profile.IsImpersonating) {
                settings["impersonate"] = profile.Email;
            }

            foreach(var provider in ClientSettingsProviders) {
                provider.ApplySettings(settings);
            }

            return settings;
        }

        private void OnAuthenticated(AuthenticationState state) {

            var profile = state.Principal.GetUserProfile();

            /*if(profile.Email.IsEmpty()) {
                return;
            }*/

            state.Account = GetAccount(state.Principal);
                
            if(state.Account == null) {
                state.Account = new Account {
                    Id = profile.Id,
                    Owner = profile.Id,
                    Email = profile.Email,
                    Photo = profile.Photo
                };
                
                if(!profile.Name.IsEmpty()) {
                    if(profile.Name.Contains(" ")) {
                        state.Account.FirstName = profile.Name.Split(" ").FirstOrDefault();
                        state.Account.LastName = profile.Name.Split(" ").LastOrDefault();
                    }
                    else {
                        state.Account.FirstName = profile.Name;
                    }
                }
                else {
                    state.Account.FirstName = profile.Email;
                }
            }
            
            if(!string.IsNullOrEmpty(profile.Photo)) {
                state.Account.Photo = profile.Photo;
            }

            state.Account.LastLogin = DateTime.UtcNow;

            AccountLoggedIn?.Invoke(state);

            var account = Data.DefaultCollection.Save(state.Account);
        }
        
        public event Action<AuthenticationState> AccountLoggedIn;

        public const string UserImpersonationKey = "impersonate";

        public List<IsClientSettingsProvider> ClientSettingsProviders;

        private HttpContext Context => ContextAccessor.HttpContext;

        private readonly AzureCosmosDbProvider Data;

        private readonly IsAuthenticationProvider Authentication;

        private readonly IHttpContextAccessor ContextAccessor;
    }
}