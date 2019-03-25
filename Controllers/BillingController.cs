using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starship.WebCore.Extensions;
using Starship.WebCore.Providers.Interfaces;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class BillingController : ApiController {

        public BillingController(IsSubscriptionProvider provider) {
            Provider = provider;
        }

        [HttpGet, Route("api/billing")]
        public IActionResult Get() {
            var user = this.GetUserProfile();
            Provider.InitializeSubscription(user);
            var session = Provider.CreateSessionToken(user.Id);
            return Ok(session);
        }
        
        private readonly IsSubscriptionProvider Provider;
    }
}