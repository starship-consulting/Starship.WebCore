﻿using System;
using System.Collections.Generic;
using System.Linq;
using ChargeBee.Api;
using ChargeBee.Exceptions;
using ChargeBee.Models;
using Newtonsoft.Json.Linq;
using Starship.Azure.Data;

namespace Starship.WebCore.Providers.ChargeBee {

    public class ChargeBeeProvider : IsBillingProvider {

        public ChargeBeeProvider(ChargeBeeSettings settings) {
            Settings = settings;
            ApiConfig.Configure(settings.Site, settings.Key);
        }

        public void Apply(Account account) {

            var customer = GetCustomer(account.ChargeBeeId);
            var subscription = GetSubscription(customer);
            account.IsTrial = subscription.TrialStart != null && subscription.TrialEnd != null && subscription.TrialEnd > DateTime.UtcNow;
            account.SubscriptionEndDate = subscription.CurrentTermEnd ?? subscription.TrialEnd ?? DateTime.UtcNow;
        }

        public Subscription InitializeSubscription(Account account) {

            Customer customer = null;

            try {
                customer = GetCustomer(account);
            }
            catch(InvalidRequestException) {
            }

            if(customer == null) {
                customer = CreateCustomer(account);
            }
            
            if(string.IsNullOrEmpty(account.ChargeBeeId)) {
                account.ChargeBeeId = customer.Id;
            }

            return GetSubscription(customer);
        }

        private Customer GetCustomer(Account account) {

            if(string.IsNullOrEmpty(account.ChargeBeeId)) {
                return FindCustomerByEmail(account.Email);
            }

            return GetCustomer(account.ChargeBeeId);
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

        public Customer CreateCustomer(Account account) {
            
            return Customer.Create()
                .FirstName(account.FirstName)
                .LastName(account.LastName)
                .Email(account.Email)
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

        private ChargeBeeSettings Settings { get; set; }
    }
}