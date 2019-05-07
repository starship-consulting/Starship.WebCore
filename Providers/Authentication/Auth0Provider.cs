using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Starship.Web.Services;

namespace Starship.WebCore.Providers.Authentication {
    public class Auth0Provider : IsAuthenticationProvider {

        public Auth0Provider(Auth0Settings settings) {
            Settings = settings;
        }

        public void AddAuth0CookieAuthentication(IServiceCollection services) {
            
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

            AddAuth0Authentication(builder);
        }

        public void AddAuth0BearerAuthentication(IServiceCollection services) {

            services.AddAuthentication(options => {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options => {
                options.Authority = Settings.Domain;
                options.Audience = Settings.Identifier;
                options.SaveToken = true;

                options.Events = new JwtBearerEvents {
                    
                    OnTokenValidated = (context) => {

                        /*var token = context.SecurityToken as JwtSecurityToken;

                        var identity = new UserProfile(context.Principal);
                        var client = new HttpClient();
                        client.DefaultRequestHeaders.Add("authorization", "Bearer " + settings.AccessToken);

                        var url = settings.Identifier + "users/" + identity.Id;
                        var user = client.GetStringAsync(url).Result;*/

                        Authenticated?.Invoke(context.Principal);
                        return Task.CompletedTask;
                    }
                };
            });
        }

        private void AddAuth0Authentication(AuthenticationBuilder builder) {

            builder.AddOpenIdConnect("Auth0", options => {
                
                options.Authority = Settings.Domain;
                options.ClientId = Settings.ClientId;
                options.ClientSecret = Settings.ClientSecret;
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
                        Authenticated?.Invoke(context.Principal);
                        return Task.CompletedTask;
                    },
                    
                    OnRedirectToIdentityProviderForSignOut = (context) => {

                        var logoutUri = $"{Settings.Domain}/v2/logout?client_id={Settings.ClientId}";
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

        public event Action<ClaimsPrincipal> Authenticated;

        public Auth0Settings Settings { get; set; }
    }
}