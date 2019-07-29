using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Starship.WebCore.Controllers {

    public class ImpersonationController : Controller {
        
        [Authorize]
        [HttpGet("api/impersonate")]
        [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Impersonate([FromQuery] string email) {

            if(string.IsNullOrEmpty(email)) {
                HttpContext.Session.Remove("impersonate");
            }
            else {
                HttpContext.Session.SetString("impersonate", email);
            }

            return Redirect("/");
        }
    }
}