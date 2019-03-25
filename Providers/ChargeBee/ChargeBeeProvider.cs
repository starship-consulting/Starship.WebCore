﻿using System;
using System.Collections.Generic;
using System.Linq;
using ChargeBee.Api;
using ChargeBee.Exceptions;
using ChargeBee.Models;
using Newtonsoft.Json.Linq;
using Starship.Azure.Data;
using Starship.Web.Security;
using Starship.WebCore.Providers.Interfaces;

namespace Starship.WebCore.Providers.ChargeBee {

    public class ChargeBeeProvider : IsSubscriptionProvider {

        public ChargeBeeProvider(ChargeBeeSettings settings) {
            Settings = settings;
            ApiConfig.Configure(settings.Site, settings.Key);
        }

        public void Apply(Account user) {
            var customer = GetCustomer(user.Id);
            var subscription = GetSubscription(customer);
            user.IsTrial = subscription.TrialStart != null && subscription.TrialEnd != null && subscription.TrialEnd > DateTime.UtcNow;
            user.SubscriptionEndDate = subscription.CurrentTermEnd ?? subscription.TrialEnd ?? DateTime.UtcNow;
        }

        public Subscription InitializeSubscription(UserProfile user) {

            Customer customer;

            try {
                customer = CreateCustomer(user);
            }
            catch(InvalidRequestException) {
                customer = GetCustomer(user.Id);
            }
            
            return GetSubscription(customer);
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

        public Customer CreateCustomer(UserProfile user) {
            
            var firstName = user.Name;
            var lastName = string.Empty;

            if(user.Name.Contains(" ")) {
                firstName = user.Name.Split(" ").First();
                lastName = user.Name.Split(" ").Skip(1).First();
            }

            Customer customer = null;

            try {
                customer = Customer.Retrieve(user.Id).Request().Customer;
            }
            catch {

            }

            if(customer == null) {
                customer = Customer.Create()
                    .Id(user.Id)
                    .FirstName(firstName)
                    .LastName(lastName)
                    .Email(user.Email)
                    .Locale("en-US")
                    .Request()
                    .Customer;
            }
            
            return customer;
        }

        public JToken CreateSessionToken(string customerId) {
            var session = PortalSession.Create()
                .CustomerId(customerId)
                .Request();

            return session.PortalSession.GetJToken();
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