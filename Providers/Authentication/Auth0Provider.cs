using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Starship.Web.Security;
using Starship.Web.Services;

namespace Starship.WebCore.Providers.Authentication {
    public static class Auth0Provider {

        public static void AddAuth0CookieAuthentication(this IServiceCollection services, Auth0Settings settings, Action<ClaimsPrincipal> onAuthenticated) {
            
            var builder = services.AddAuthentication(options => {
                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options => {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                //options.AccessDeniedPath = "";
            });

            AddAuth0Authentication(builder, settings, onAuthenticated);
        }

        public static void AddAuth0BearerAuthentication(this IServiceCollection services, Auth0Settings settings, Action<ClaimsPrincipal> onAuthenticated) {

            services.AddAuthentication(options => {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options => {
                options.Authority = settings.Domain;
                options.Audience = settings.Identifier;
                options.SaveToken = true;

                options.Events = new JwtBearerEvents {
                    
                    OnTokenValidated = (context) => {

                        /*var token = context.SecurityToken as JwtSecurityToken;

                        var identity = new UserProfile(context.Principal);
                        var client = new HttpClient();
                        client.DefaultRequestHeaders.Add("authorization", "Bearer " + settings.AccessToken);

                        var url = settings.Identifier + "users/" + identity.Id;
                        var user = client.GetStringAsync(url).Result;*/

                        //onAuthenticated?.Invoke(context.Principal);
                        return Task.CompletedTask;
                    }
                };
            });
        }

        private static void AddAuth0Authentication(AuthenticationBuilder builder, Auth0Settings settings, Action<ClaimsPrincipal> onAuthenticated) {

            builder.AddOpenIdConnect("Auth0", options => {
                
                options.Authority = settings.Domain;
                options.ClientId = settings.ClientId;
                options.ClientSecret = settings.ClientSecret;
                options.ResponseType = "code";
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("email");
                options.Scope.Add("profile");

                options.CallbackPath = new PathString("/signin-auth0");
                options.ClaimsIssuer = "Auth0";
                options.SaveTokens = true;

                options.GetClaimsFromUserInfoEndpoint = true;

                options.TokenValidationParameters = new TokenValidationParameters {
                    NameClaimType = "name"
                };

                options.Prompt = "select_account";

                options.Events = new OpenIdConnectEvents {
                    
                    OnTokenValidated = (context) => {
                        onAuthenticated?.Invoke(context.Principal);
                        return Task.CompletedTask;
                    },
                    
                    OnRedirectToIdentityProviderForSignOut = (context) => {

                        var logoutUri = $"{settings.Domain}/v2/logout?client_id={settings.ClientId}";
                        var postLogoutUri = context.Properties.RedirectUri;

                        if (!string.IsNullOrEmpty(postLogoutUri)) {

                            if (postLogoutUri.StartsWith("/")) {
                                var request = context.Request;
                                postLogoutUri = request.Scheme + "://" + request.Host + request.PathBase + postLogoutUri;
                            }

                            logoutUri += $"&returnTo={Uri.EscapeDataString(postLogoutUri)}";
                        }

                        context.Response.Redirect(logoutUri);
                        context.HandleResponse();

                        return Task.CompletedTask;
                    }
                };
            });
        }
    }
}