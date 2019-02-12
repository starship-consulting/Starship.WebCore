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
using Starship.Web.Security;
using Starship.Web.Services;
using Starship.WebCore.Azure;
using Starship.WebCore.Configuration;

namespace Starship.WebCore.Extensions {
    public static class ServiceExtensions {

        public static AzureDocumentDbProvider UseCosmosDb(this IServiceCollection services, IConfiguration configuration) {
            var settings = ConfigurationMapper.Map<CosmosDbSettings>(configuration);
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