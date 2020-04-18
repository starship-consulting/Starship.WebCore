using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Integration.Billing;
using Starship.Web.Security;
using Starship.WebCore.Models;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Controllers {

    public class UserController : ApiController {

        public UserController(IServiceProvider serviceProvider) {
            Accounts = serviceProvider.GetRequiredService<AccountManager>();
            Billing = serviceProvider.GetService<IsSubscriptionProvider>();
            Data = serviceProvider.GetRequiredService<AzureCosmosDbProvider>();
        }

        [Route("api/login")]
        public async Task Login(string returnUrl = "/") {

            await HttpContext.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties {
                RedirectUri = returnUrl/*,
                Parameters = {
                    {"login_hint", "signup"}
                }*/
            });
        }
        
        [Authorize]
        [Route("api/logout")]
        public async Task Logout() {

            await HttpContext.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme, new AuthenticationProperties {
                RedirectUri = ""
            });

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        
        [Authorize]
        [HttpPost, Route("api/users/{id}")]
        public async Task<IActionResult> SaveUser([FromRoute] string id, [FromQuery] AccountQueryOptions query) {
            
            var currentUser = Accounts.GetAccount();

            if(!currentUser.IsAdmin()) {
                return Unauthorized();
            }

            var account = Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account" && each.Id == id).ToList().FirstOrDefault();

            if(account != null) {
                account.Role = query.Role;

                await Data.DefaultCollection.SaveAsync(account);
            }

            return Ok(true);
        }
        
        /*[Authorize]
        [Route("api/userinfo")]
        public async Task<object> GetUserInfo() {
            var accessToken = User.Claims.FirstOrDefault(c => c.Type == "access_token")?.Value;
            return await Authentication.GetUserInfoAsync(accessToken);
        }*/

        [Authorize]
        [Route("api/deleteuser")]
        public async Task<IActionResult> DeleteUser() {

            if(User == null || User.Identity == null || !User.Identity.IsAuthenticated) {
                return Ok();
            }
            
            var account = Accounts.GetAccount();

            if(Billing != null) {
                var details = account.Get<SubscriptionDetails>("subscription");
                await Billing.CancelSubscriptionAsync(details.SubscriptionId);
            }
            
            account.ValidUntil = DateTime.UtcNow;
            await Data.DefaultCollection.SaveAsync(account);

            await Logout();

            //return RedirectToAction("Index", "Home");
            return Ok();
        }
        
        [Authorize]
        [Route("api/user")]
        public async Task<IActionResult> GetUserProfile() {
            
            if(User == null || User.Identity == null || !User.Identity.IsAuthenticated) {
                return Ok(new UserProfile());
            }
            
            var account = Accounts.GetAccount();
            
            /*if(Billing != null) {
                var details = Billing.GetSubscription(account);

                account.Components = new Dictionary<string, object> {
                    { "billing", JsonConvert.DeserializeObject(JsonConvert.SerializeObject(details)) }
                };

                await Data.DefaultCollection.SaveAsync(account);
            }*/

            var settings = Accounts.GetSettings();

            var invitationIds = Data.DefaultCollection.Get<CosmosDocument>()
                .Where(each => each.Type == "invitation" && each.Participants.Any(participant => participant.Id == account.Email))
                .Select(each => each.Owner)
                .ToList();

            var invitations = new List<object>();

            if(invitationIds.Any()) {
                var accounts = Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account" && invitationIds.Contains(each.Id)).ToList();

                invitations.AddRange(accounts.Select(each => new {
                    id = each.Id,
                    photo = each.Photo,
                    name = each.GetName()
                })
                .ToList());
            }
            
            return new JsonResult(new { account, settings, invitations }, Data.Settings.SerializerSettings);
        }

        private readonly AzureCosmosDbProvider Data;

        private readonly IsSubscriptionProvider Billing;

        private readonly AccountManager Accounts;
    }
}