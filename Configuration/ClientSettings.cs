using System;
using System.Collections.Generic;

namespace Starship.WebCore.Configuration {
    public class ClientSettings : Dictionary<string, string> {

        public bool HasImpersonationEmail() {
            return ContainsKey("impersonate");
        }

        public string GetImpersonationEmail() {

            if(HasImpersonationEmail()) {
                return this["impersonate"];
            }

            return string.Empty;
        }
    }
}