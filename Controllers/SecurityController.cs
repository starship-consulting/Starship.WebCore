using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Data;
using Starship.Core.Email;
using Starship.WebCore.Configuration;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class SecurityController : ApiController {
        
        public SecurityController(AccountManager users, AzureCosmosDbProvider data, EmailClient email, DataSharingSettings dataSettings, SiteSettings siteSettings) {
            Users = users;
            Data = data;
            EmailClient = email;
            DataSettings = dataSettings;
            SiteSettings = siteSettings;
        }

        [HttpPost, Route("api/policy")]
        public async Task<IActionResult> SavePolicies([FromBody] List<CosmosPolicy> policies) {

            var account = Users.GetAccount();
            
            account.Policies = policies;

            await Data.DefaultCollection.SaveAsync(account);

            return Ok();
        }
        
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

                    var changeset = new List<CosmosResource> { account, otherAccount };
                    await Data.DefaultCollection.CallProcedure(Data.Settings.SaveProcedureName, changeset);

                    await Data.DefaultCollection.DeleteAsync(existing.Id);
                }
            }

            return Ok();
        }

        [HttpDelete, Route("api/access/{idOrEmail}")]
        public async Task<IActionResult> RejectAccess([FromRoute] string idOrEmail) {

            idOrEmail = idOrEmail.ToLower();

            var sourceAccount = Users.GetAccount();
            var targetAccount = Users.GetAccountByEmail(idOrEmail) ?? Users.GetAccountById(idOrEmail);

            if(targetAccount != null) {
                await DeleteInvitation(targetAccount, sourceAccount.Email);

                if(targetAccount.HasParticipant(sourceAccount.Id)) {
                    targetAccount.RemoveParticipant(sourceAccount.Id);
                    await Data.DefaultCollection.SaveAsync(targetAccount);
                }

                if(sourceAccount.HasParticipant(targetAccount.Id)) {
                    sourceAccount.RemoveParticipant(targetAccount.Id);
                    await Data.DefaultCollection.SaveAsync(sourceAccount);
                }
            }

            await DeleteInvitation(sourceAccount, idOrEmail);

            return Ok(true);
        }

        private async Task DeleteInvitation(Account senderAccount, string receiverEmail) {

            var invitation = Data.DefaultCollection.Get<CosmosDocument>()
                .Where(each => each.Type == "invitation" && each.Owner == senderAccount.Id && each.Participants.Any(participant => participant.Id == receiverEmail))
                .ToList()
                .FirstOrDefault();

            if(invitation != null) {
                await Data.DefaultCollection.DeleteAsync(invitation.Id);
            }
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
            
            /*if(SiteSettings.IsProduction()) {

                var body = DataSettings.InvitationEmailBody
                    .Replace("{{url}}", SiteSettings.Url)
                    .Replace("{{name}}", account.GetName());

                await EmailClient.SendAsync(string.Empty, email, DataSettings.InvitationEmailSubject, body);
            }*/

            return Ok(true);
        }
        
        private readonly AzureCosmosDbProvider Data;
        
        private readonly AccountManager Users;

        private readonly EmailClient EmailClient;

        private readonly DataSharingSettings DataSettings;

        private readonly SiteSettings SiteSettings;
    }
}