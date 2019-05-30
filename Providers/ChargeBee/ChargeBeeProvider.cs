using System;
using System.Collections.Generic;
using System.Linq;
using ChargeBee.Api;
using ChargeBee.Exceptions;
using ChargeBee.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
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

        private void Apply(Account account) {
            GetSubscription(account);
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

        public void DeleteCustomer(Customer customer) {
            var subscriptions = GetSubscriptions(customer);

            foreach(var subscription in subscriptions) {
                Subscription.Delete(subscription.Id);
            }

            Customer.Delete(customer.Id).Request();
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
        
        public Subscription GetSubscription(Account account) {
            
            Subscription subscription;

            var chargebee = account.GetComponent<ChargeBeeComponent>();

            if(string.IsNullOrEmpty(chargebee.ChargeBeeId)) {
                subscription = InitializeSubscription(account.FirstName, account.LastName, account.Email);
            }
            else {
                var customer = GetCustomer(chargebee.ChargeBeeId);
                subscription = GetOrCreateSubscription(customer);
            }
            
            chargebee.ChargeBeeId = subscription.CustomerId;
            chargebee.IsTrial = subscription.TrialStart != null && subscription.TrialEnd != null && subscription.TrialEnd > DateTime.UtcNow;
            chargebee.SubscriptionEndDate = subscription.CurrentTermEnd ?? subscription.TrialEnd ?? DateTime.UtcNow;

            account.SetComponent(chargebee);

            return subscription;
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

        private Subscription InitializeSubscription(string firstName, string lastName, string email) {

            Customer customer;

            try {
                customer = FindCustomerByEmail(email);

                if(customer != null) {
                    return GetOrCreateSubscription(customer);
                }
            }
            catch(InvalidRequestException) {
            }
            
            customer = CreateCustomer(firstName, lastName, email);
            return GetOrCreateSubscription(customer);
        }
        
        private List<ListResult.Entry> HostedPages() {
            return HostedPage.List()
                .Request()
                .List;
        }

        public Customer GetCustomer(string customerId) {
            return Customer.Retrieve(customerId)
                .Request()
                .Customer;
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

        public Subscription GetOrCreateSubscription(Customer customer) {

            var subscriptions = GetSubscriptions(customer);
            
            if(subscriptions != null && subscriptions.Any()) {
                return subscriptions.First();
            }

            return Subscription.CreateForCustomer(customer.Id)
                .PlanId(Settings.DefaultSubscriptionId)
                .Request()
                .Subscription;
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

        private readonly AzureDocumentDbProvider Data;

        private readonly ChargeBeeSettings Settings;

        private readonly AccountManager AccountManager;
    }
}