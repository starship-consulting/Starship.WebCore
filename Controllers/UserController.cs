using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Web.Security;
using Starship.WebCore.Models;
using Starship.WebCore.Providers.Authentication;
using Starship.WebCore.Providers.ChargeBee;

namespace Starship.WebCore.Controllers {

    public class UserController : ApiController {

        public UserController(IServiceProvider serviceProvider) {
            Accounts = serviceProvider.GetRequiredService<AccountManager>();
            Billing = serviceProvider.GetService<IsBillingProvider>();
            Data = serviceProvider.GetRequiredService<AzureDocumentDbProvider>();
            HostingEnvironment = serviceProvider.GetRequiredService<IHostingEnvironment>();
        }
        
        /*[Authorize, Route("signup")]
        public IActionResult SignUp(string plan = "") {
            
            return File(System.IO.File.OpenRead(Path.Combine(HostingEnvironment.WebRootPath + "/index.html")), "text/html");
            //return Redirect("/");
        }*/

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
        [Route("api/user")]
        public async Task<IActionResult> GetUserProfile() {
            
            if(User == null || User.Identity == null || !User.Identity.IsAuthenticated) {
                return Ok(new UserProfile());
            }
            
            var account = Accounts.GetAccount();
            
            if(Billing != null) {
                Billing.GetSubscription(account);
                await Data.DefaultCollection.SaveAsync(account);
            }

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

        private readonly AzureDocumentDbProvider Data;

        private readonly IsBillingProvider Billing;

        private readonly AccountManager Accounts;

        private readonly IHostingEnvironment HostingEnvironment;
    }
}