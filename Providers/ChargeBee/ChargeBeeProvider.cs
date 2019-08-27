using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using ChargeBee.Api;
using ChargeBee.Exceptions;
using ChargeBee.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Extensions;
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

            GetSubscription(state.Account, plan);
        }

        public List<Plan> GetPlans() {
            return GetResults(Plan.List()).Select(each => each.Plan).ToList();
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
        
        public Subscription GetSubscription(Account account, string planId = "") {
            
            var chargebee = account.GetComponent<ChargeBeeComponent>();

            Customer customer = null;

            if(!chargebee.ChargeBeeId.IsEmpty()) {
                customer = GetCustomer(chargebee.ChargeBeeId);
            }

            if(customer == null || customer.Email.ToLower() != account.Email.ToLower()) {
                customer = FindCustomerByEmail(account.Email.ToLower());
            }

            if(customer == null) {
                customer = CreateCustomer(account.FirstName, account.LastName, account.Email);
            }

            if(customer == null) {
                chargebee.Clear(planId);
                //account.SetComponent(chargebee);

                account.Components = new Dictionary<string, object> {
                    { "chargeBee", JsonConvert.DeserializeObject(JsonConvert.SerializeObject(chargebee)) }
                };

                return null;
            }

            var plan = GetPlans().FirstOrDefault(each => each.Id.ToLower() == planId.ToLower());
            var subscription = GetPrimarySubscription(customer);
            
            if(subscription == null && plan?.TrialPeriod != null) {
                subscription = GetOrCreateSubscription(customer, planId);
            }

            if(subscription != null) {
                chargebee.PlanId = subscription.PlanId;
                chargebee.IsTrial = subscription.TrialStart != null && subscription.TrialEnd != null && subscription.TrialEnd > DateTime.UtcNow;
                chargebee.SubscriptionEndDate = subscription.CurrentTermEnd ?? subscription.TrialEnd ?? DateTime.UtcNow;
            }
            else {
                chargebee.Clear(planId);
            }
            
            chargebee.ChargeBeeId = customer.Id;

            account.Components = new Dictionary<string, object> {
                { "chargeBee", JsonConvert.DeserializeObject(JsonConvert.SerializeObject(chargebee)) }
            };

            //account.SetComponent(chargebee);

            return subscription;
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
        
        private Customer GetOrCreateCustomer(string customerId, string firstName, string lastName, string email) {

            Customer customer = null;

            if(!string.IsNullOrEmpty(customerId)) {
                try {
                    customer = GetCustomer(customerId);
                }
                catch(InvalidRequestException) {
                }
            }

            if(customer == null) {
                try {
                    customer = FindCustomerByEmail(email);
                }
                catch(InvalidRequestException) {
                }
            }

            if(customer == null) {
                customer = CreateCustomer(firstName, lastName, email);
            }

            return customer;
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

        public Subscription GetOrCreateSubscription(Customer customer, string planId = "") {

            var subscription = GetPrimarySubscription(customer);

            if(subscription != null) {
                return subscription;
            }
            
            return CreateSubscription(customer.Id, ResolvePlanId(planId));
        }

        private string ResolvePlanId(string planId = "") {

            if(planId.IsEmpty()) {
                planId = Settings.DefaultSubscriptionId;
            }

            return planId;
        }

        public Subscription CreateSubscription(string customerId, string planId) {
            return Subscription.CreateForCustomer(customerId).PlanId(planId).Request().Subscription;
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

        public JToken GetCheckoutToken(Account account, string plan = "") {
            
            var planId = ResolvePlanId(plan);

            var checkout = HostedPage.CheckoutNew();
            var chargebee = account.GetComponent<ChargeBeeComponent>();

            if(chargebee != null) {
                if(!chargebee.ChargeBeeId.IsEmpty()) {
                    checkout = checkout.CustomerId(chargebee.ChargeBeeId);
                }
            }

            var result = checkout
                .CustomerEmail(account.Email)
                .CustomerFirstName(account.FirstName)
                .CustomerLastName(account.LastName)
                .SubscriptionPlanId(planId)
                .BillingAddressFirstName(account.FirstName)
                .BillingAddressLastName(account.LastName)
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

        private JToken CreateCheckoutToken(Customer customer) {

            /*var user = this.GetUser();

            var firstName = user.Name;
            var lastName = string.Empty;

            if(user.Name.Contains(" ")) {
                firstName = user.Name.Split(" ").First();
                lastName = user.Name.Split(" ").Skip(1).First();
            }*/
            
            var checkout = HostedPage.CheckoutNew()
                .CustomerEmail(customer.Email)
                .CustomerFirstName(customer.FirstName)
                .CustomerLastName(customer.LastName)
                .CustomerLocale("en-US")
                /*.CustomerPhone("+1-949-999-9999")
                .SubscriptionPlanId("new-plan")
                .BillingAddressFirstName("John")
                .BillingAddressLastName("Doe")
                .BillingAddressLine1("PO Box 9999")
                .BillingAddressCity("Walnut")
                .BillingAddressState("California")
                .BillingAddressZip("91789")*/
                .BillingAddressCountry("US")
                //.Embed(true)
                .Request();

            var token = checkout.HostedPage.GetJToken();

            return token;
        }

        private readonly AzureCosmosDbProvider Data;

        private readonly ChargeBeeSettings Settings;

        private readonly AccountManager AccountManager;
    }
}