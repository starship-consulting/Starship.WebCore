using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starship.Azure.Providers.Cosmos;
using Starship.WebCore.Providers.Authentication;
using Starship.WebCore.Providers.ChargeBee;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class BillingController : ApiController {

        public BillingController(IsBillingProvider billing, AccountManager users, AzureDocumentDbProvider data) {
            Billing = billing;
            Users = users;
            Data = data;
        }

        [HttpGet, Route("api/billing")]
        public async Task<IActionResult> Get() {
            var account = Users.GetAccount();
            var subscriber = Billing.GetSubscription(account);

            await Data.DefaultCollection.SaveAsync(account);

            var session = Billing.GetSessionToken(subscriber.CustomerId);
            return Ok(session);
        }

        private readonly AzureDocumentDbProvider Data;
        
        private readonly IsBillingProvider Billing;

        private readonly AccountManager Users;
    }
}