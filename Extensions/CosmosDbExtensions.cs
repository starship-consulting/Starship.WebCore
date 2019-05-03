using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Documents;
using Newtonsoft.Json;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Extensions;

namespace Starship.WebCore.Extensions {
    public static class CosmosDbExtensions {

        public static Account GetAccountDocument(this AzureDocumentDbProvider data, string email) {
            email = email.ToLower();
            return data.DefaultCollection.Get<Account>().Where(each => each.Type == "account" && each.Email == email).ToList().FirstOrDefault();
        }

        public static Account GetAccount(this AzureDocumentDbProvider data, string email) {
            email = email.ToLower();
            return data.DefaultCollection.Get<Account>().Where(each => each.Type == "account" && each.Email.ToLower() == email).ToList().FirstOrDefault();
        }

        public static Account GetAccount(this AzureDocumentDbProvider data, ClaimsPrincipal principal) {
            var profile = principal.GetUserProfile();

            if(profile.Email.IsEmpty()) {
                return new Account { Id = profile.Id };
            }

            return data.GetAccount(profile.Email);
        }

        public static IActionResult ToJsonResult(this Resource resource, JsonSerializerSettings settings) {
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(resource));
            return new JsonResult(data, settings);
        }

        public static IActionResult ToJsonResult(this IEnumerable<object> documents, JsonSerializerSettings settings) {
            var data = documents.Select(each => JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(each)));
            return new JsonResult(data, settings);
        }

        public static string ToJson(this IEnumerable<object> documents) {
            var data = documents.Select(each => JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(each)));
            return JsonConvert.SerializeObject(data);
        }
    }
}