using System;
using System.Collections.Generic;

namespace Starship.WebCore.Configuration {
    public class ClientSettings : Dictionary<string, string> {

        public bool HasImpersonation() {
            return ContainsKey("impersonate");
        }

        public string GetImpersonation() {

            if(HasImpersonation()) {
                return this["impersonate"];
            }

            return string.Empty;
        }
    }
}