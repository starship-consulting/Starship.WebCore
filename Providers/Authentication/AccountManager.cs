using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
            Settings = provider.GetService<IOptionsMonitor<AccountManagementSettings>>().CurrentValue;
            Data = provider.GetService<AzureDocumentDbProvider>();
            ContextAccessor = provider.GetService<IHttpContextAccessor>();
            Authentication = provider.GetService<IsAuthenticationProvider>();
            Authentication.Authenticated += OnAuthenticated;
            ClientSettingsProviders = provider.GetServices<IsClientSettingsProvider>().ToList();
        }
        
        public void Dispose() {
            if(Authentication != null) {
                Authentication.Authenticated -= OnAuthenticated;
            }
        }
        
        public Account GetAccountByEmail(string email) {
            email = email.ToLower();
            return Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account" && each.Email.ToLower() == email).ToList().FirstOrDefault();
        }

        public Account GetAccount() {
            var profile = GetUserProfile();

            if(profile.Email.IsEmpty()) {
                return new Account { Id = profile.Id };
            }

            return GetAccountByEmail(profile.Email);
        }

        public UserProfile GetUserProfile() {

            var profile = Context.User.GetUserProfile();
            
            if(Context.Session.Keys.Contains(UserImpersonationKey)) {
                var account = GetAccountByEmail(profile.Email);

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

        private void OnAuthenticated(ClaimsPrincipal principal) {

            var profile = principal.GetUserProfile();
            var account = GetAccountByEmail(profile.Email);
                
            if(account == null) {
                account = new Account {
                    Id = profile.Id,
                    Owner = profile.Id,
                    FirstName = profile.Name.Split(" ").FirstOrDefault(),
                    LastName = profile.Name.Split(" ").LastOrDefault(),
                    Email = profile.Email,
                    Photo = profile.Photo
                };
            }
            
            if(!string.IsNullOrEmpty(profile.Photo)) {
                account.Photo = profile.Photo;
            }

            account.LastLogin = DateTime.UtcNow;

            AccountLoggedIn?.Invoke(account);

            Data.DefaultCollection.Save(account);
        }
        
        public event Action<Account> AccountLoggedIn;

        public const string UserImpersonationKey = "impersonate";

        private HttpContext Context => ContextAccessor.HttpContext;

        private List<IsClientSettingsProvider> ClientSettingsProviders;

        private readonly AzureDocumentDbProvider Data;

        private readonly IsAuthenticationProvider Authentication;

        private readonly IHttpContextAccessor ContextAccessor;

        private readonly AccountManagementSettings Settings;
    }
}