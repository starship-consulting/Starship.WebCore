using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PostmarkDotNet;
using PostmarkDotNet.Legacy;
using PostmarkDotNet.Model;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Email;
using Starship.Core.Extensions;
using Starship.WebCore.Interfaces;
using Starship.WebCore.Providers.Authentication;
using Starship.WebCore.Providers.Postmark;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class EmailController : ApiController {
        
        public EmailController(AccountManager users, AzureDocumentDbProvider data, IsEmailProvider email) {
            Users = users;
            Data = data;
            Email = email;
        }

        [HttpGet, Route("api/email/verify")]
        public async Task<IActionResult> Verify([FromQuery] string email) {

            var account = Users.GetAccount();
            var confirmed = await Email.Verify(account, email);

            await Data.DefaultCollection.SaveAsync(account);
            
            return Ok(new { confirmed });
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

        private readonly AzureDocumentDbProvider Data;

        private readonly AccountManager Users;
    }
}