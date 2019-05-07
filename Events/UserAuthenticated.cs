using System;
using Starship.Web.Security;

namespace Starship.WebCore.Events {
    public class UserAuthenticated {

        public UserProfile User { get; set; }
    }
}