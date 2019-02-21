using System;
using System.Collections.Generic;
using System.Linq;
using ChargeBee.Api;
using ChargeBee.Exceptions;
using ChargeBee.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Starship.Azure.Providers.Cosmos;
using Starship.WebCore.Extensions;

namespace Starship.WebCore.Controllers {

    [Authorize]
    public class ChargeBeeController : ApiController {

        public ChargeBeeController(AzureDocumentDbProvider provider) {
            Provider = provider;
        }

        [HttpGet, Route("api/billing")]
        public IActionResult Get() {
            
            var user = this.GetUser();
            
            try {
                var customer = CreateCustomer();
            }
            catch(InvalidRequestException) {
            }

            var session = CreateSessionToken(user.Id);

            return Ok(session);
        }

        private List<ListResult.Entry> HostedPages() {
            return HostedPage.List().Request().List;
        }

        private Customer GetCustomer(string customerId) {
            return Customer.Retrieve(customerId).Request().Customer;
        }

        private Customer CreateCustomer() {
            
            var user = this.GetUser();

            var firstName = user.Name;
            var lastName = string.Empty;

            if(user.Name.Contains(" ")) {
                firstName = user.Name.Split(" ").First();
                lastName = user.Name.Split(" ").Skip(1).First();
            }

            var result = Customer.Create()
                .Id(user.Id)
                .FirstName(firstName)
                .LastName(lastName)
                .Email(user.Email)
                .Locale("en-US")
                .Request();

            return result.Customer;
        }

        private JToken CreateSessionToken(string customerId) {
            var session = PortalSession.Create()
                .CustomerId(customerId)
                .Request();

            return session.PortalSession.GetJToken();
        }

        private JToken CreateCheckoutToken() {

            var user = this.GetUser();

            var firstName = user.Name;
            var lastName = string.Empty;

            if(user.Name.Contains(" ")) {
                firstName = user.Name.Split(" ").First();
                lastName = user.Name.Split(" ").Skip(1).First();
            }
            
            var checkout = HostedPage.CheckoutNew()
                .CustomerEmail(user.Email)
                .CustomerFirstName(firstName)
                .CustomerLastName(lastName)
                .CustomerLocale("en-US")
                .CustomerPhone("+1-949-999-9999")
                .SubscriptionPlanId("new-plan")
                .BillingAddressFirstName("John")
                .BillingAddressLastName("Doe")
                .BillingAddressLine1("PO Box 9999")
                .BillingAddressCity("Walnut")
                .BillingAddressState("California")
                .BillingAddressZip("91789")
                .BillingAddressCountry("US")
                .Embed(true)
                .Request();

            var token = checkout.HostedPage.GetJToken();

            return token;
        }

        private readonly AzureDocumentDbProvider Provider;
    }
}