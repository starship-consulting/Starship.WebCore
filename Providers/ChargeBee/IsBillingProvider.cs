using System;
using ChargeBee.Models;
using Newtonsoft.Json.Linq;
using Starship.Azure.Data;

namespace Starship.WebCore.Providers.ChargeBee {
    public interface IsBillingProvider {
        
        JToken GetSessionToken(string customerId);

        Subscription GetSubscription(Account account);
    }
}