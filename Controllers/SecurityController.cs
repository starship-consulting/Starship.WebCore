using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Email;
using Starship.Core.Security;
using Starship.WebCore.Configuration;
using Starship.WebCore.Extensions;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class SecurityController : ApiController {
        
        public SecurityController(UserRepository users, AzureDocumentDbProvider data, EmailClient email, DataSharingSettings dataSettings, SiteSettings siteSettings) {
            Users = users;
            Data = data;
            EmailClient = email;
            DataSettings = dataSettings;
            SiteSettings = siteSettings;
        }

        /*[HttpGet, Route("claims/{claimId}")]
        public async Task<IActionResult> GrantClaim([FromRoute] string claimId) {
            
            var claim = Collection.Find<ClaimEntity>(claimId);

            if(claim == null) {
                return StatusCode(404);
            }
            
            var user = Users.GetUserProfile();

            if(user.Email.ToLower() != claim.Value.ToLower()) {

                // Need to handle this better.  Inform user that he used an email that didn't match?
                return Redirect("/");
            }

            claim.Status = 1;
            claim.Value = user.Id;

            //await Collection.SaveAsync(claim);

            return Redirect("/");
        }*/

        /*[HttpPost, Route("api/groups/{groupId}/{accountId}")]
        public async Task<IActionResult> AddGroupMember([FromRoute] string groupId, [FromRoute] string accountId) {

            var account = Users.GetAccount();

            var group = 
            if(account.CanUpdate())

            return Ok();
        }*/

        [HttpPost, Route("api/access")]
        public async Task<IActionResult> RequestAccess([FromQuery] string email) {

            email = email.ToLower();

            var account = Users.GetAccount();
            
            var invitedAccount = Data.GetAccount(email);

            if(invitedAccount == null) {
                invitedAccount = new Account();
                invitedAccount.Id = Guid.NewGuid().ToString();
                invitedAccount.Type = "account";
                invitedAccount.CreationDate = DateTime.UtcNow;
                invitedAccount.Email = email;
                invitedAccount.Referrer = account.Id;
                invitedAccount.Owner = invitedAccount.Id;
            }

            if(invitedAccount.Participants == null) {
                invitedAccount.Participants = new List<EntityParticipant>();
            }

            if(account.Participants == null) {
                account.Participants = new List<EntityParticipant>();
            }
            
            if(account.Participants.Any(participant => participant.Id == invitedAccount.Id)) {
                return BadRequest();
            }

            if(invitedAccount.Participants.Any(participant => participant.Id == account.Id)) {
                return BadRequest();
            }

            var accountClaims = account.Participants.ToList();
            accountClaims.Add(new EntityParticipant(invitedAccount.Id, string.Empty));
            account.Participants = accountClaims.ToList();

            var invitedAccountClaims = invitedAccount.Participants.ToList();
            invitedAccountClaims.Add(new EntityParticipant(account.Id, string.Empty));
            invitedAccount.Participants = invitedAccountClaims.ToList();
            
            await Data.DefaultCollection.SaveAsync(new List<CosmosDocument> { account, invitedAccount });

            if(SiteSettings.IsProduction()) {

                /*var body = DataSettings.InvitationEmailBody
                .Replace("{{url}}", SiteSettings.Url)
                .Replace("{{name}}", account.GetName());

                await EmailClient.SendAsync(string.Empty, email, DataSettings.InvitationEmailSubject, body);*/
            }

            return Ok(true);
        }
        
        private readonly AzureDocumentDbProvider Data;
        
        private readonly UserRepository Users;

        private readonly EmailClient EmailClient;

        private readonly DataSharingSettings DataSettings;

        private readonly SiteSettings SiteSettings;
    }
}