using System;
using Microsoft.AspNetCore.Authorization;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class EventController : ApiController {
    }
}