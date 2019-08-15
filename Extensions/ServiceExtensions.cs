using System;
using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Email;
using Starship.Core.Storage;
using Starship.Data.Configuration;
using Starship.Web.Services;
using Starship.WebCore.Azure;
using Starship.WebCore.Configuration;
using Starship.WebCore.Interfaces;
using Starship.WebCore.Providers.Authentication;
using Starship.WebCore.Providers.ChargeBee;
using Starship.WebCore.Providers.Postmark;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection {
    public static class ServiceExtensions {

        /*public static void AddUserRepository(this IServiceCollection services, IConfiguration configuration) {
            //var settings = ConfigurationMapper.Map<ClientSettings>(configuration);
            services.AddSingleton<UserRepository>();
        }*/

        public static void UseAccounts(this IServiceCollection services, IConfiguration configuration) {
            services.Configure<AccountManagementSettings>(ConfigurationMapper.GetSection<AccountManagementSettings>(configuration));

            // Todo:  Move to UserImpersonationInterceptor
            services.AddSession(options => {
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = false;
                options.Cookie.MaxAge = TimeSpan.FromDays(1);
                options.IdleTimeout = TimeSpan.FromDays(1);
            });
            
            services.AddSingleton<AccountManager>();
        }

        public static AzureCosmosDbProvider UseCosmosDb(this IServiceCollection services, IConfiguration configuration) {
            var settings = ConfigurationMapper.Map<DataSettings>(configuration);
            var provider = new AzureCosmosDbProvider(settings);
            services.AddSingleton(provider);
            return provider;
        }

        public static void UseSmtp(this IServiceCollection services, IConfiguration configuration) {
            var settings = ConfigurationMapper.Map<SmtpSettings>(configuration);

            var client = new EmailClient(settings.Domain, settings.Username, settings.Password, settings.Host, settings.Port);
            client.DefaultFromAddress = settings.Username;

            services.AddSingleton(client);
        }

        public static void UseAzureFileStorage(this IServiceCollection services, IConfiguration configuration) {
            var settings = ConfigurationMapper.Map<AzureFileStorageSettings>(configuration);
            var provider = new AzureFileStorageProvider(settings);
            services.AddSingleton<IsFileStorageProvider>(provider);
        }

        public static void UseDataSharing(this IServiceCollection services, IConfiguration configuration) {
            var settings = ConfigurationMapper.Map<DataSharingSettings>(configuration);
            services.AddSingleton(settings);
        }

        public static void UseSiteSettings(this IServiceCollection services, IConfiguration configuration, string environment) {
            var settings = ConfigurationMapper.Map<SiteSettings>(configuration);
            settings.Environment = environment;
            services.AddSingleton(settings);
        }
        
        public static void UseAuth0(this IServiceCollection services, IConfiguration configuration) {
            var settings = ConfigurationMapper.Map<Auth0Settings>(configuration, services);

            var authProvider = new Auth0Provider();

            var builder = services.AddAuthentication(options => {

                if(settings.UseCookies) {
                    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                }
            });

            if(settings.UseCookies) {

                builder = builder.AddCookie(options => {
                    options.LoginPath = "/api/login";
                    options.LogoutPath = "/api/logout";
                    //options.AccessDeniedPath = "";
                });
            }
            
            if(settings.UseJwtBearer) {
                
                builder = builder.AddJwtBearer(options => {

                    options.Authority = settings.Domain;
                    options.Audience = settings.Audience;
                
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;

                    options.TokenValidationParameters = new TokenValidationParameters {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey =  new SymmetricSecurityKey(Encoding.ASCII.GetBytes(settings.ClientSecret)),
                        //ValidIssuer = Settings.Issuer,
                        ValidAudience = settings.Audience,
                        ValidateIssuer = false,
                        ValidateAudience = true
                    };

                    options.Events = new JwtBearerEvents {
                    
                        OnTokenValidated = context => {
                            
                            if (context.SecurityToken is JwtSecurityToken token) {
                                if (context.Principal.Identity is ClaimsIdentity identity) {
                                    identity.AddClaim(new Claim("access_token", token.RawData));

                                    //var apiClient = new AuthenticationApiClient(settings.Domain.Replace("https://", ""));
                                    //var userInfo = await apiClient.GetUserInfoAsync(token.RawData);
                                }
                            }
                            
                            authProvider.Authenticate(new AuthenticationState(context.Principal, context.Properties.RedirectUri));
                            return Task.CompletedTask;
                        }
                    };
                });
            }

            builder.AddOpenIdConnect(options => {
                
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

                //options.RequireHttpsMetadata = false; // Disable in production
                options.GetClaimsFromUserInfoEndpoint = true;
                
                options.TokenValidationParameters = new TokenValidationParameters {
                    NameClaimType = "name"
                };

                options.Prompt = "select_account";

                options.Events = new OpenIdConnectEvents {
                    
                    OnTokenValidated = (context) => {
                        authProvider.Authenticate(new AuthenticationState(context.Principal, context.Properties.RedirectUri));
                        return Task.CompletedTask;
                    },

                    OnRedirectToIdentityProvider = context => {
                        //context.ProtocolMessage.SetParameter("audience", settings.Identifier);
                        
                        if(context.HttpContext.Request.Path.HasValue && context.HttpContext.Request.Path.Value.ToLower().EndsWith("signup")) {
                            context.ProtocolMessage.SetParameter("signup", "1");
                        }
                        
                        return Task.FromResult(0);
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

            services.AddSingleton<IsAuthenticationProvider>(authProvider);
        }

        public static void UseChargeBee(this IServiceCollection services, IConfiguration configuration) {
            services.Configure<ChargeBeeSettings>(ConfigurationMapper.GetSection<ChargeBeeSettings>(configuration));
            services.AddSingleton<IsBillingProvider, ChargeBeeProvider>();
        }

        public static PostmarkProvider UsePostmark(this IServiceCollection services, IConfiguration configuration) {
            var settings = ConfigurationMapper.Map<PostmarkSettings>(configuration);
            var provider = new PostmarkProvider(settings);
            services.AddSingleton<IsEmailProvider, PostmarkProvider>(service => provider);
            return provider;
        }

        public static void UseCompression(this IServiceCollection services) {

            services.Configure<GzipCompressionProviderOptions>(opts => opts.Level = CompressionLevel.Optimal);

            services.AddResponseCompression(options => {

                options.Providers.Add<GzipCompressionProvider>();

                var types = new[] {
                    "text/plain",
                    "text/css",
                    "application/javascript",
                    "text/html",
                    "application/xml",
                    "text/xml",
                    "application/json",
                    "text/json",
                    "image/svg+xml",
                    "image/png",
                    "image/jpeg"
                };

                options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(types);
                options.EnableForHttps = true;
            });
        }
    }
}