using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Web.Security;
using Starship.WebCore.Extensions;
using Starship.WebCore.Providers.Interfaces;

namespace Starship.WebCore.Controllers {

    public class UserController : ApiController {

        public UserController(IServiceProvider serviceProvider) {
            SettingsProvider = serviceProvider.GetService<IsUserSettingsProvider>();
            SubscriptionProvider = serviceProvider.GetService<IsSubscriptionProvider>();
            Provider = serviceProvider.GetService<AzureDocumentDbProvider>();
        }

        [Route("api/user")]
        public IActionResult GetUser() {

            if(User == null || User.Identity == null || !User.Identity.IsAuthenticated) {
                return Ok(new UserProfile());
            }

            var profile = new UserProfile(User);

            var account = Provider.DefaultCollection.Find<Account>(profile.Id);

            // Todo:  Use query hooks instead

            if(SubscriptionProvider != null) {
                SubscriptionProvider.Apply(account);
            }

            if(SettingsProvider != null) {
                SettingsProvider.Apply(account);
            }

            return account.ToJsonResult(Provider.Settings.SerializerSettings);
        }

        private readonly AzureDocumentDbProvider Provider;

        private readonly IsSubscriptionProvider SubscriptionProvider;

        private readonly IsUserSettingsProvider SettingsProvider;
    }
}