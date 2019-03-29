using System;
using ChargeBee.Models;
using Newtonsoft.Json.Linq;
using Starship.Azure.Data;
using Starship.Web.Security;

namespace Starship.WebCore.Providers.ChargeBee {
    public interface IsBillingProvider {

        Subscription InitializeSubscription(UserProfile user);

        JToken GetSessionToken(string customerId);

        void Apply(Account user);
    }
}