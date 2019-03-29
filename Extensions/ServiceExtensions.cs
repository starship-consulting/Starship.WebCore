using System;
using System.IO.Compression;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Starship.Azure.Providers.Cosmos;
using Starship.Core.Email;
using Starship.Core.Storage;
using Starship.Data.Configuration;
using Starship.Data.Interfaces;
using Starship.Web.Security;
using Starship.Web.Services;
using Starship.WebCore.Azure;
using Starship.WebCore.Configuration;
using Starship.WebCore.Providers.ChargeBee;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection {
    public static class ServiceExtensions {

        /*public static void AddUserRepository(this IServiceCollection services, IConfiguration configuration) {
            //var settings = ConfigurationMapper.Map<ClientSettings>(configuration);
            services.AddSingleton<UserRepository>();
        }*/

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

        public static void UseAuth0Cookies(this IServiceCollection services, IConfiguration configuration, Action<ClaimsPrincipal> onAuthenticated) {
            var settings = ConfigurationMapper.Map<Auth0Settings>(configuration);
            services.AddAuth0CookieAuthentication(settings, onAuthenticated);
        }

        public static void UseAuth0Bearer(this IServiceCollection services, IConfiguration configuration, Action<ClaimsPrincipal> onAuthenticated) {
            var settings = ConfigurationMapper.Map<Auth0Settings>(configuration);
            services.AddAuth0BearerAuthentication(settings, onAuthenticated);
        }

        public static ChargeBeeProvider UseChargeBee(this IServiceCollection services, IConfiguration configuration) {
            var settings = ConfigurationMapper.Map<ChargeBeeSettings>(configuration);
            var provider = new ChargeBeeProvider(settings);
            services.AddSingleton<IsBillingProvider, ChargeBeeProvider>(service => provider);
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