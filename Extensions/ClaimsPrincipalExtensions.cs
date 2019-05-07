using System.Security.Claims;
using Starship.Web.Security;

namespace Starship.WebCore.Extensions {

    public static class ClaimsPrincipalExtensions {
        
        public static UserProfile GetUserProfile(this ClaimsPrincipal principal) {
            
            if (principal?.Identity == null || !principal.Identity.IsAuthenticated) {
                return UserProfile.Guest();
            }
            
            return new UserProfile(principal);
        }
    }
}