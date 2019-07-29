using System;
using Newtonsoft.Json;

namespace Starship.WebCore.Providers.ChargeBee {
    public class ChargeBeeComponent {
        
        public void Clear(string defaultPlanId = "") {
            PlanId = defaultPlanId;
            IsTrial = false;
            SubscriptionEndDate = null;
            ChargeBeeId = string.Empty;
        }

        [JsonProperty(PropertyName="chargeBeeId")]
        public string ChargeBeeId { get; set; }

        [JsonProperty(PropertyName="isTrial")]
        public bool IsTrial { get; set; }

        [JsonProperty(PropertyName="subscriptionEndDate")]
        public DateTime? SubscriptionEndDate { get; set; }

        [JsonProperty(PropertyName="billingDate")]
        public DateTime? BillingDate { get; set; }

        [JsonProperty(PropertyName="planId")]
        public string PlanId { get; set; }

        [JsonProperty(PropertyName="excluded")]
        public bool Excluded { get; set; }
    }
}