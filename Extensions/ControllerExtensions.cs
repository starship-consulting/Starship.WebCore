using Microsoft.AspNetCore.Mvc;
using Starship.Web.Security;

namespace Starship.WebCore.Extensions {

    public static class ControllerExtensions {
        
        public static UserProfile GetUser(this ControllerBase controller) {
            if (controller.User?.Identity == null || !controller.User.Identity.IsAuthenticated) {
                return UserProfile.Guest();
            }

            return new UserProfile(controller.User);
        }

        public static UserProfile GetUser(this Controller controller) {
            if (controller.User?.Identity == null || !controller.User.Identity.IsAuthenticated) {
                return UserProfile.Guest();
            }

            return new UserProfile(controller.User);
        }
    }
}