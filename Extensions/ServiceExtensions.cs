using System;
using System.IO.Compression;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
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

        public static AzureDocumentDbProvider UseCosmosDb(this IServiceCollection services, IConfiguration configuration) {
            var settings = ConfigurationMapper.Map<DataSettings>(configuration);
            var provider = new AzureDocumentDbProvider(settings);
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
        
        public static Auth0Provider UseAuth0(this IServiceCollection services, IConfiguration configuration, Action<Auth0Settings> configureSettings = null) {
            var settings = ConfigurationMapper.Map<Auth0Settings>(configuration);
            configureSettings?.Invoke(settings);

            var provider = new Auth0Provider(settings);

            if(settings.UseCookies) {
                provider.AddAuth0CookieAuthentication(services);
            }
            
            if(settings.UseJwtBearer) {
                provider.AddAuth0BearerAuthentication(services);
            }

            services.AddSingleton<IsAuthenticationProvider>(provider);
            return provider;
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