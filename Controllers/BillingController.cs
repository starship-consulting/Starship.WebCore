using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starship.WebCore.Providers.Authentication;
using Starship.WebCore.Providers.ChargeBee;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class BillingController : ApiController {

        public BillingController(IsBillingProvider billing, UserRepository users) {
            Billing = billing;
            Users = users;
        }

        [HttpGet, Route("api/billing")]
        public IActionResult Get() {
            var account = Users.GetAccount();
            Billing.InitializeSubscription(account);
            var session = Billing.GetSessionToken(account.ChargeBeeId);
            return Ok(session);
        }
        
        private readonly IsBillingProvider Billing;

        private readonly UserRepository Users;
    }
}