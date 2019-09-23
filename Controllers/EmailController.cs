using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PostmarkDotNet.Exceptions;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Email;
using Starship.Core.Extensions;
using Starship.Core.Security;
using Starship.Core.Validation;
using Starship.WebCore.Configuration;
using Starship.WebCore.Interfaces;
using Starship.WebCore.Providers.Authentication;
using Starship.WebCore.Providers.ChargeBee;
using Starship.WebCore.Providers.Security;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class EmailController : ApiController {
        
        public EmailController(IServiceProvider services, IOptionsMonitor<SecuritySettings> securitySettings) {

            Users = services.GetService<AccountManager>();
            Data = services.GetService<AzureCosmosDbProvider>();
            Email = services.GetService<IsEmailProvider>();
            SecuritySettings = securitySettings.CurrentValue;
            EmailClient = services.GetService<EmailClient>();
            SiteSettings = services.GetService<SiteSettings>();
            Billing = services.GetService<IsBillingProvider>();
        }

        [Authorize]
        [HttpPost, Route("api/email/change")]
        public async Task<IActionResult> RequestChangeEmail([FromQuery] string email) {
            
            if(!Validation.Email(email)) {
                return BadRequest("Please enter a valid email address.");
            }

            var account = Users.GetAccount();

            if(account.Email.ToLower() == email.ToLower()) {
                return BadRequest("That is already your current email.");
            }

            var otherAccount = Users.GetAccountByEmail(email);

            if(otherAccount != null) {
                return BadRequest("An account with that email already exists.");
            }
            
            var token = Guid.NewGuid().ToString().Split("-").First().ToUpper();
            account.ChangeEmail = Hash.EncryptStringAES(email, token, SecuritySettings.Salt);
            await Data.DefaultCollection.SaveAsync(account);
            
            await EmailClient.SendAsync(email, SiteSettings.Name + " Email Verification", "A request has been made to change your email address.  Please enter this verification code to confirm:<div style='font-weight: bold;'>" + token + "</div>");
            
            return Ok();
        }

        [Authorize]
        [HttpPost, Route("api/email/approve")]
        public async Task<IActionResult> ApproveChangeEmail([FromQuery] string token) {

            var account = Users.GetAccount();

            if(string.IsNullOrEmpty(account.ChangeEmail)) {
                return BadRequest("Email already changed.");
            }

            // Todo:  Merge accounts / change Chargebee email
            // Todo:  Create Auth0 account if it doesn't exist?

            try {
                var email = Hash.DecryptStringAES(account.ChangeEmail, token, SecuritySettings.Salt);
                var otherAccount = Users.GetAccountByEmail(email);

                if(otherAccount != null) {
                    return BadRequest("An account with that email already exists.");
                }

                if(Billing != null) {
                    try {
                        Billing.ChangeCustomerEmail(account.Email, email);
                    }
                    catch {
                    }
                }

                account.Email = email;

                await Data.DefaultCollection.SaveAsync(account);
            }
            catch {
                return BadRequest("Verification code did not match.");
            }

            return Ok();
        }

        [HttpGet, Route("api/email/verify")]
        public async Task<IActionResult> Verify([FromQuery] string email) {

            try {
                var account = Users.GetAccount();
                var confirmed = await Email.Verify(account, email);

                await Data.DefaultCollection.SaveAsync(account);
                return Ok(new { confirmed });
            }
            catch(PostmarkValidationException ex) {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost, Route("api/email")]
        public async Task<IActionResult> Send([FromBody] EmailModel email) {
            
            if(email == null || email.Subject.IsEmpty() || email.Body.IsEmpty() || !email.To.Any()) {
                return BadRequest();
            }

            var account = Users.GetAccount();

            if(!string.IsNullOrEmpty(account.Signature)) {
                email.Body += "<br><br>" + account.Signature;
            }
            
            var success = await Email.Send(account, email);

            if(!success) {
                return BadRequest();
            }
            
            return Ok(new { success = true });
        }
        
        private readonly IsEmailProvider Email;

        private readonly AzureCosmosDbProvider Data;

        private readonly AccountManager Users;

        private readonly SecuritySettings SecuritySettings;

        private readonly EmailClient EmailClient;

        private readonly SiteSettings SiteSettings;

        private readonly IsBillingProvider Billing;
    }
}