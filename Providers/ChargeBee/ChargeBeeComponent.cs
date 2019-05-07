using System;
using Newtonsoft.Json;

namespace Starship.WebCore.Providers.ChargeBee {
    public class ChargeBeeComponent {
        
        [JsonProperty(PropertyName="chargeBeeId")]
        public string ChargeBeeId { get; set; }

        [JsonProperty(PropertyName="isTrial")]
        public bool IsTrial { get; set; }

        [JsonProperty(PropertyName="subscriptionEndDate")]
        public DateTime SubscriptionEndDate { get; set; }
    }
}