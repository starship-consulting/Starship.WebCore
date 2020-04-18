using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using ChargeBee.Api;
using ChargeBee.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Extensions;
using Starship.Integration.Billing;
using Starship.Integration.People;
using Starship.WebCore.Configuration;
using Starship.WebCore.Interfaces;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Providers.ChargeBee {

    public class ChargeBeeProvider : IsBillingProvider, IsClientSettingsProvider {

        public ChargeBeeProvider(IOptionsMonitor<ChargeBeeSettings> settings, AccountManager accountManager) {
            Settings = settings.CurrentValue;
            AccountManager = accountManager;
            AccountManager.ClientSettingsProviders.Add(this);
            AccountManager.AccountLoggedIn += Apply;

            ApiConfig.Configure(Settings.Site, Settings.Key);
        }

        private static SubscriptionDetails ToSubscriptionDetails(Subscription subscription) {
            
            return new SubscriptionDetails {
                CustomerId = subscription.CustomerId,
                IsTrial = subscription.TrialStart != null && subscription.TrialEnd != null && subscription.TrialEnd > DateTime.UtcNow,
                SubscriptionEndDate = subscription.CurrentTermEnd ?? subscription.TrialEnd ?? DateTime.UtcNow,
                BillingDate = subscription.NextBillingAt,
                PlanId = subscription.PlanId
            };
        }

        public void ApplySettings(ClientSettings settings) {
            settings["chargeBee"] = Settings.Site;
        }

        private void Apply(AuthenticationState state) {
            
            var plan = string.Empty;

            if(!state.RedirectUri.IsEmpty() && state.RedirectUri.Contains("?")) {
                var query = HttpUtility.ParseQueryString(state.RedirectUri.Split("?").Last());

                if(query.AllKeys.Any(key => key == "plan")) {
                    plan = query["plan"];
                }
            }

            var account = state.Account;
            var subscription = account.Get<SubscriptionDetails>("subscription");

            if(subscription != null) {

            }

            var customer = GetSubscriptionByCustomerId(account.Id);

            //GetSubscription(state.Account, plan);
        }

        public List<SubscriptionPlan> GetPlans() {

            return GetResults(Plan.List())
                .Select(each => each.Plan)
                .Select(each => new SubscriptionPlan {

                }).ToList();
        }

        public List<Coupon> GetCoupons() {
            return GetResults(Coupon.List()).Select(each => each.Coupon).ToList();
        }

        public List<Customer> GetCustomers() {
            return GetResults(Customer.List()).Select(each => each.Customer).ToList();
        }

        public List<Subscription> GetSubscriptions() {
            return GetResults(Subscription.List()).Select(each => each.Subscription).ToList();
        }

        public List<Subscription> GetActiveSubscriptions() {
            return GetResults(Subscription.List()
                .Status().IsNot(Subscription.StatusEnum.Cancelled))
                .Select(each => each.Subscription)
                .Where(each => each.Status != Subscription.StatusEnum.Paused)
                .ToList();
        }

        public void CancelSubscription() {

        }

        public void DeleteCustomer(Customer customer) {
            var subscriptions = GetSubscriptions(customer);

            foreach(var subscription in subscriptions) {
                Subscription.Delete(subscription.Id);
            }

            Customer.Delete(customer.Id).Request();
        }

        public void CancelSubscription(string subscriptionId) {
            var result = Subscription.Cancel(subscriptionId).EndOfTerm(true).Request();

            if(result.StatusCode != HttpStatusCode.OK) {
                throw new Exception("Unexpected Chargebee http status: " + result.StatusCode);
            }
        }

        public void ChangeCustomerEmail(string oldEmail, string newEmail) {
            var customer = FindCustomerByEmail(oldEmail.ToLower());

            if(customer != null) {
                Customer.Update(customer.Id).Email(newEmail.ToLower());
            }
        }

        public void ChangeSubscriptionPlan(Subscription subscription, string planId, bool immediate) {
            
            var request = Subscription.Update(subscription.Id).PlanId(planId);

            if(immediate) {
                request.Request();
            }
            else {
                request.EndOfTerm(true).Request();
            }
        }

        public SubscriptionDetails GetSubscriptionByEmail(string email) {

            var customer = FindCustomerByEmail(email.ToLower());

            if(customer == null) {
                return null;
            }

            return GetSubscription(customer);
        }

        public SubscriptionDetails GetSubscriptionByCustomerId(string customerId) {

            var customer = GetCustomer(customerId);

            if(customer == null) {
                return null;
            }

            return GetSubscription(customer);
        }
        
        public Customer GetCustomer(string customerId) {
            try {
                return Customer.Retrieve(customerId).Request().Customer;
            }
            catch(Exception) {
                return null;
            }
        }

        private List<ListResult.Entry> GetResults<T>(T list) where T : ListRequestBase<T> {

            var results = new List<ListResult.Entry>();
            var query = list.Limit(100);

            while(true) {
                var request = query.Request();
                results.AddRange(request.List.ToList());

                if(string.IsNullOrEmpty(request.NextOffset)) {
                    break;
                }

                query = query.Offset(request.NextOffset);
            }

            return results;
        }
        
        public List<Subscription> GetSubscriptions(Customer customer) {

            var subscriptions = Subscription.List()
                .CustomerId().Is(customer.Id)
                .Request()
                .List;
            
            if(subscriptions != null && subscriptions.Any()) {
                return subscriptions.Select(each => each.Subscription).ToList();
            }

            return new List<Subscription>();
        }

        public Subscription GetPrimarySubscription(Customer customer) {

            var subscriptions = GetSubscriptions(customer);
            
            if(subscriptions != null && subscriptions.Any()) {
                return subscriptions.First();
            }

            return null;
        }
        
        public SubscriptionDetails GetSubscription(Customer customer) {
            return ToSubscriptionDetails(GetPrimarySubscription(customer));
        }

        public SubscriptionDetails GetOrCreateSubscription(Customer customer, string planId = "") {
            return GetSubscription(customer) ?? CreateSubscription(customer.Id, ResolvePlanId(planId));
        }

        private string ResolvePlanId(string planId = "") {

            if(planId.IsEmpty()) {
                planId = Settings.DefaultSubscriptionId;
            }

            return planId;
        }

        public SubscriptionDetails CreateSubscription(string customerId, string planId) {
            return ToSubscriptionDetails(Subscription.CreateForCustomer(customerId).PlanId(planId).Request().Subscription);
        }

        public Customer CreateCustomer(string firstName, string lastName, string email) {
            
            return Customer.Create()
                .FirstName(firstName)
                .LastName(lastName)
                .Email(email)
                .Locale("en-US")
                .Request()
                .Customer;
        }

        public JToken GetSessionToken(string customerId) {
            var session = PortalSession.Create()
                .CustomerId(customerId)
                .Request();

            return session.PortalSession.GetJToken();
        }

        public JToken GetCheckoutToken(IsPerson person, string billingId = "", string plan = "") {
            
            var planId = ResolvePlanId(plan);

            var checkout = HostedPage.CheckoutNew();
            
            if(!string.IsNullOrEmpty(billingId)) {
                checkout = checkout.CustomerId(billingId);
            }

            var result = checkout
                .CustomerEmail(person.Email)
                .CustomerFirstName(person.FirstName)
                .CustomerLastName(person.LastName)
                .SubscriptionPlanId(planId)
                .BillingAddressFirstName(person.FirstName)
                .BillingAddressLastName(person.LastName)
                .BillingAddressCountry("US")
                .Embed(true)
                .Request();
            
            return result.HostedPage.GetJToken();
        }

        private Customer FindCustomerByEmail(string email) {
            var result = Customer.List().Email().Is(email).Request().List.FirstOrDefault();

            if(result != null && result.Customer != null) {
                return result.Customer;
            }

            return null;
        }
        
        private readonly AzureCosmosDbProvider Data;

        private readonly ChargeBeeSettings Settings;

        private readonly AccountManager AccountManager;
        private IsBillingProvider _isBillingProviderImplementation;
    }
}