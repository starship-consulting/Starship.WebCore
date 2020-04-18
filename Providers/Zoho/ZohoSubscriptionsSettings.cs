using System;
using Starship.Integration.Zoho.Configuration;

namespace Starship.WebCore.Providers.Zoho {

    public class ZohoSubscriptionsSettings : ZohoApiSettings {
        public string DefaultSubscriptionId { get; set; }
    }
}