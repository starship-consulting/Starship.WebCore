using System;
using ChargeBee.Models;
using Newtonsoft.Json.Linq;
using Starship.Azure.Data;

namespace Starship.WebCore.Providers.ChargeBee {
    public interface IsBillingProvider {

        Subscription InitializeSubscription(Account account);

        JToken GetSessionToken(string customerId);

        void Apply(Account user);
    }
}