using System.Collections.Generic;
using ChargeBee.Models;
using Newtonsoft.Json.Linq;
using Starship.Azure.Data;

namespace Starship.WebCore.Interfaces {
    public interface IsBillingProvider {
        
        JToken GetSessionToken(string customerId);

        JToken GetCheckoutToken(Account account, string planId = "");

        Subscription GetSubscription(Account account, string planId = "");
        
        List<Plan> GetPlans();
        
        void CancelSubscription(string subscriptionId);

        void ChangeCustomerEmail(string oldEmail, string newEmail);
    }
}