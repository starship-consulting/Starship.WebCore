using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Starship.Azure.Providers.Cosmos;
using Starship.Web.Security;
using Starship.WebCore.Extensions;
using Starship.WebCore.Providers.Authentication;
using Starship.WebCore.Providers.ChargeBee;

namespace Starship.WebCore.Controllers {

    public class UserController : ApiController {

        public UserController(UserRepository users, IsBillingProvider billing, AzureDocumentDbProvider data) {
            Users = users;
            Billing = billing;
            Data = data;
        }

        [Route("login")]
        public async Task Login(string returnUrl = "/") {

            await HttpContext.ChallengeAsync("Auth0", new AuthenticationProperties {
                RedirectUri = returnUrl
            });
        }

        [Authorize]
        [Route("logout")]
        public async Task Logout() {

            await HttpContext.SignOutAsync("Auth0", new AuthenticationProperties {
                RedirectUri = ""
            });

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }

        [Route("api/user")]
        public IActionResult GetUser() {

            if(User == null || User.Identity == null || !User.Identity.IsAuthenticated) {
                return Ok(new UserProfile());
            }
            
            var account = Users.GetAccount();

            // Todo:  Use query hooks instead

            if(Billing != null) {
                Billing.Apply(account);
            }

            return account.ToJsonResult(Data.Settings.SerializerSettings);
        }

        /*[Route("api/user/impersonate/{id}")]
        public IActionResult Impersonate([FromRoute] string id) {

            var identity = User.Identities.First();
            var key = "ImpersonationId";
            
            if(identity.HasClaim(claim => claim.Type == key)) {
                identity.RemoveClaim(identity.FindFirst(key));
            }

            identity.AddClaim(new Claim(key, id));

            return Ok();
        }*/

        private readonly AzureDocumentDbProvider Data;

        private readonly IsBillingProvider Billing;

        private readonly UserRepository Users;
    }
}