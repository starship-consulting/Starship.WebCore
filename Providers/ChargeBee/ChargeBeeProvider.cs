using System;
using System.Collections.Generic;
using System.Linq;
using ChargeBee.Api;
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
        
        public Subscription GetSubscription(Account account) {
            
            Subscription subscription;

            var chargebee = account.GetComponent<ChargeBeeComponent>();

            if(string.IsNullOrEmpty(chargebee.ChargeBeeId)) {
                subscription = InitializeSubscription(account.FirstName, account.LastName, account.Email);
            }
            else {
                var customer = GetCustomer(chargebee.ChargeBeeId);
                subscription = GetSubscription(customer);
            }
            
            chargebee.ChargeBeeId = subscription.CustomerId;
            chargebee.IsTrial = subscription.TrialStart != null && subscription.TrialEnd != null && subscription.TrialEnd > DateTime.UtcNow;
            chargebee.SubscriptionEndDate = subscription.CurrentTermEnd ?? subscription.TrialEnd ?? DateTime.UtcNow;

            account.SetComponent(chargebee);

            return subscription;
        }

        private Subscription InitializeSubscription(string firstName, string lastName, string email) {

            //Customer customer = null;

            /*try {
                customer = GetCustomer(component.ChargeBeeId);
            }
            catch(InvalidRequestException) {
            }

            if(customer == null) {
                customer = CreateCustomer(component.ChargeBeeId);
            }*/
            
            var customer = CreateCustomer(firstName, lastName, email);
            return GetSubscription(customer);
        }

        /*private Customer GetCustomer(Account account) {

            if(string.IsNullOrEmpty(account.ChargeBeeId)) {
                return FindCustomerByEmail(account.Email);
            }

            return GetCustomer(account.ChargeBeeId);
        }*/

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

        public Subscription GetSubscription(Customer customer) {

            var subscriptions = Subscription.List()
                .CustomerId().Is(customer.Id)
                .Request()
                .List;
            
            if(subscriptions != null && subscriptions.Any()) {
                return subscriptions.First().Subscription;
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