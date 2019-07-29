using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ChargeBee.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.WebCore.Providers.Authentication;
using Starship.WebCore.Providers.ChargeBee;

namespace Starship.WebCore.Controllers {
    
    public class BillingController : ApiController {

        public BillingController(IsBillingProvider billing, AccountManager users, AzureCosmosDbProvider data) {
            Billing = billing;
            Users = users;
            Data = data;
        }

        [HttpPost, Route("api/billing")]
        public async Task<IActionResult> Post() {
            return Ok();
        }

        [HttpPost, Route("api/billing/sync")]
        public async Task<IActionResult> Sync() {
            return Ok();
        }
        
        [Authorize, HttpGet, Route("api/billing/plans")]
        public IActionResult Plans() {
            
            var plans = Billing.GetPlans().Select(each => new {
                id = each.Id,
                name = each.Name,
                friendlyName = each.InvoiceName,
                description = each.Description,
                status = (int) each.Status
            });

            return new JsonResult(plans, Data.Settings.SerializerSettings);
        }

        [Authorize, HttpGet, Route("api/billing/subscriptions")]
        public IActionResult GetSubscriptions() {

            var plans = Billing.GetPlans();
            //var customers = Billing.GetCustomers();
            var accounts = Data.DefaultCollection.Get<Account>().Where(each => each.Type == "account" && each.ValidUntil == null).ToList();
            
            var subscriptions = Billing.GetActiveSubscriptions()
                .Select(subscription => {

                    //var customer = customers.FirstOrDefault(each => each.Id == subscription.CustomerId);
                    var plan = plans.FirstOrDefault(each => each.Id == subscription.PlanId);

                    var planName = string.Empty;

                    if(plan != null) {
                        planName = plan.InvoiceName;
                    }

                    var account = accounts.FirstOrDefault(each => subscription.CustomerId == each.GetComponent<ChargeBeeComponent>().ChargeBeeId);
                
                    var accountId = string.Empty;
                    var email = string.Empty;

                    if(account != null) {
                        accountId = account.Id;
                        email = account.Email;
                    }
                
                    return new {
                        accountId = accountId,
                        email = email,
                        planId = subscription.PlanId,
                        planName = planName,
                        amount = subscription.PlanAmount,
                        billingDate = subscription.NextBillingAt,
                        status = subscription.Status
                    };
                })
                .ToList();

            return Ok(subscriptions);
        }

        [Authorize, HttpGet, Route("api/billing/portal")]
        public IActionResult GetPortalSessionToken() {

            var account = Users.GetAccount();
            var chargebee = account.GetComponent<ChargeBeeComponent>();

            //var subscriber = Billing.GetSubscription(account);

            //await Data.DefaultCollection.SaveAsync(account);

            //var session = Billing.GetSessionToken(subscriber.CustomerId);

            return Ok(Billing.GetSessionToken(chargebee.ChargeBeeId));
        }

        [Authorize, HttpGet, Route("api/billing/checkout")]
        public IActionResult Checkout([FromQuery] string plan) {
            var account = Users.GetAccount();
            return Ok(Billing.GetCheckoutToken(account, plan));
        }

        private void DeleteDuplicates() {
            
            var customers = Billing.GetCustomers();

            var deletedCustomers = new List<Customer>();
            var currentCustomers = new List<Customer>();
            
            foreach(var customer in customers.OrderByDescending(each => each.CreatedAt)) {
                if(currentCustomers.Any(each => each.Email.ToLower() == customer.Email.ToLower())) {
                    deletedCustomers.Add(customer);
                }

                currentCustomers.Add(customer);
            }

            foreach(var eachCustomer in deletedCustomers) {
                Billing.DeleteCustomer(eachCustomer);
            }
        }

        private readonly AzureCosmosDbProvider Data;
        
        private readonly IsBillingProvider Billing;

        private readonly AccountManager Users;
    }
}