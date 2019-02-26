using System;
using ChargeBee.Exceptions;
using ChargeBee.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starship.WebCore.Extensions;
using Starship.WebCore.Providers.ChargeBee;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class ChargeBeeController : ApiController {

        public ChargeBeeController(ChargeBeeProvider provider) {
            Provider = provider;
        }

        [HttpGet, Route("api/billing")]
        public IActionResult Get() {
            var user = this.GetUser();
            Provider.InitializeSubscription(user);
            var session = Provider.CreateSessionToken(user.Id);
            return Ok(session);
        }
        
        private readonly ChargeBeeProvider Provider;
    }
}