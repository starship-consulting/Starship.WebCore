using System;
using ChargeBee.Models;
using Newtonsoft.Json.Linq;
using Starship.Azure.Data;
using Starship.Web.Security;

namespace Starship.WebCore.Providers.Interfaces {
    public interface IsSubscriptionProvider {

        Subscription InitializeSubscription(UserProfile user);

        JToken CreateSessionToken(string customerId);

        void Apply(Account user);
    }
}