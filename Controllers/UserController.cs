using System;
using Microsoft.AspNetCore.Mvc;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Web.Security;
using Starship.WebCore.Extensions;

namespace Starship.WebCore.Controllers {

    public class UserController : ApiController {

        public UserController(AzureDocumentDbProvider provider) {
            Provider = provider;
        }

        [Route("api/user")]
        public IActionResult GetUser() {

            if(User == null || User.Identity == null || !User.Identity.IsAuthenticated) {
                return Ok(new UserProfile());
            }

            var user = new UserProfile(User);
            var account = Provider.DefaultCollection.Find<CosmosDocument>(user.Id);
            return account.ToJsonResult(Provider.Settings.SerializerSettings);
        }

        private readonly AzureDocumentDbProvider Provider;
    }
}