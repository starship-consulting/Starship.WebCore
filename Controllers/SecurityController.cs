using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Email;
using Starship.WebCore.Configuration;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class SecurityController : ApiController {
        
        public SecurityController(AccountManager users, AzureDocumentDbProvider data, EmailClient email, DataSharingSettings dataSettings, SiteSettings siteSettings) {
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

        [HttpGet, Route("api/access/{id}")]
        public async Task<IActionResult> AcceptAccess([FromRoute] string id) {

            var account = Users.GetAccount();

            var existing = Data.DefaultCollection.Get<CosmosDocument>()
                .Where(each => each.Type == "invitation" && each.Owner == id && each.Participants.Any(participant => participant.Id == account.Email))
                .ToList()
                .FirstOrDefault();

            if(existing != null) {
                var otherAccount = Data.DefaultCollection.Get<CosmosDocument>().Where(each => each.Type == "account" && each.Id == id).ToList().FirstOrDefault();

                if(otherAccount != null) {

                    account.AddParticipant(id);
                    otherAccount.AddParticipant(account.Id);

                    var changeset = new List<Resource> { account, otherAccount };
                    await Data.DefaultCollection.CallProcedure<Document>(Data.Settings.SaveProcedureName, changeset);

                    await Data.DefaultCollection.DeleteAsync(existing.Id);
                }
            }

            return Ok();
        }
        
        [HttpPost, Route("api/access")]
        public async Task<IActionResult> RequestAccess([FromQuery] string email) {

            email = email.ToLower();

            var account = Users.GetAccount();

            var existing = Data.DefaultCollection.Get<CosmosDocument>()
                .Where(each => each.Type == "invitation" && each.Owner == account.Id && each.Participants.Any(participant => participant.Id == account.Email))
                .ToList();

            if(existing.Any()) {
                return BadRequest();
            }

            var invitation = new CosmosDocument {
                Id = Guid.NewGuid().ToString(),
                Type = "invitation",
                CreationDate = DateTime.UtcNow,
                Owner = account.Owner
            };

            invitation.AddParticipant(email, account.GetName());

            await Data.DefaultCollection.SaveAsync(invitation);
            
            if(SiteSettings.IsProduction()) {

                /*var body = DataSettings.InvitationEmailBody
                .Replace("{{url}}", SiteSettings.Url)
                .Replace("{{name}}", account.GetName());

                await EmailClient.SendAsync(string.Empty, email, DataSettings.InvitationEmailSubject, body);*/
            }

            return Ok(true);
        }
        
        private readonly AzureDocumentDbProvider Data;
        
        private readonly AccountManager Users;

        private readonly EmailClient EmailClient;

        private readonly DataSharingSettings DataSettings;

        private readonly SiteSettings SiteSettings;
    }
}