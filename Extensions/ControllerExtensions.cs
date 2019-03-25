using Microsoft.AspNetCore.Mvc;
using Starship.Azure.Data;
using Starship.Azure.Providers.Cosmos;
using Starship.Web.Security;

namespace Starship.WebCore.Extensions {

    public static class ControllerExtensions {
        
        public static UserProfile GetUserProfile(this ControllerBase controller) {
            return controller.User.GetUserProfile();
        }

        public static Account GetAccount(this ControllerBase controller, AzureDocumentDbProvider provider) {
            return provider.GetAccount(controller.User);
        }
    }
}