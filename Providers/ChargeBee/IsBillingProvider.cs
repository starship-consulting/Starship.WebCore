using System;
using System.Collections.Generic;
using ChargeBee.Models;
using Newtonsoft.Json.Linq;
using Starship.Azure.Data;

namespace Starship.WebCore.Providers.ChargeBee {
    public interface IsBillingProvider {
        
        JToken GetSessionToken(string customerId);

        JToken GetCheckoutToken(Account account, string planId = "");

        Subscription GetSubscription(Account account, string planId = "");

        void ChangeSubscriptionPlan(Subscription subscription, string plan, bool immediate);

        List<Plan> GetPlans();

        List<Coupon> GetCoupons();

        List<Subscription> GetSubscriptions();

        List<Subscription> GetActiveSubscriptions();

        List<Customer> GetCustomers();

        void DeleteCustomer(Customer customer);

        void CancelSubscription(string subscriptionId);

        void ChangeCustomerEmail(string oldEmail, string newEmail);
    }
}