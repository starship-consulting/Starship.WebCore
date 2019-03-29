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
            var user = Users.GetUserProfile();
            Billing.InitializeSubscription(user);
            var session = Billing.GetSessionToken(user.Id);
            return Ok(session);
        }
        
        private readonly IsBillingProvider Billing;

        private readonly UserRepository Users;
    }
}