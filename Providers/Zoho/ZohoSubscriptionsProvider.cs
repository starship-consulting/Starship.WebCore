using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Options;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Extensions;
using Starship.Integration.Billing;
using Starship.Integration.Zoho;
using Starship.Integration.Zoho.Requests;
using Starship.WebCore.Providers.Authentication;

namespace Starship.WebCore.Providers.Zoho {

    public class ZohoSubscriptionsProvider : IsSubscriptionProvider {

        public ZohoSubscriptionsProvider(IOptionsMonitor<ZohoSubscriptionsSettings> settings, AccountManager accountManager) {
            Settings = settings.CurrentValue;
            HttpClient = new ZohoHttpClient(Settings.AuthorizationToken, Settings.OrganizationId);

            AccountManager = accountManager;
            AccountManager.AccountLoggedIn += Apply;
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
                subscription = GetSubscriptionAsync(subscription.SubscriptionId).Result;
            }

            account.Set("subscription", subscription);
        }

        public async Task<List<SubscriptionDetails>> GetCustomerSubscriptionsAsync(string customerId) {

            if(string.IsNullOrEmpty(customerId)) {
                return new List<SubscriptionDetails>();
            }

            var response = await new GetZohoSubscriptionSubscriptionsByCustomer(customerId).RequestAsync(HttpClient);
            return response.Subscriptions.Select(each => each.ToSubscriptionDetails()).ToList();
        }
        
        public async Task<SubscriptionDetails> GetSubscriptionAsync(string subscriptionId) {

            if(string.IsNullOrEmpty(subscriptionId)) {
                return new SubscriptionDetails();
            }

            var response = await new GetZohoSubscription(subscriptionId).RequestAsync(HttpClient);
            return response.Subscription.ToSubscriptionDetails();
        }

        public async Task CancelSubscriptionAsync(string subscriptionId) {
            await new CancelZohoSubscription(subscriptionId).RequestAsync(HttpClient);
        }

        public async Task ChangeCustomerEmailAsync(string oldEmail, string newEmail) {
            var response = await new GetZohoCustomers().RequestAsync(HttpClient);

            oldEmail = oldEmail.ToLower();

            var customer = response.Customers.FirstOrDefault(each => each.Email.ToLower() == oldEmail);

            if(customer != null) {
                customer.Email = newEmail.ToLower();
                await new UpdateZohoCustomer(customer).RequestAsync(HttpClient);
            }
        }

        private readonly AzureCosmosDbProvider Data;

        private readonly ZohoSubscriptionsSettings Settings;

        private readonly AccountManager AccountManager;

        private readonly ZohoHttpClient HttpClient;
    }
}