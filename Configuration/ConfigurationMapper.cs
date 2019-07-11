using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Starship.WebCore.Configuration {
    public static class ConfigurationMapper {
        
        public static string GetDefaultName<T>() where T : new() {
            return typeof(T).Name.Replace("Settings", "");
        }

        public static IConfigurationSection GetSection<T>(IConfiguration configuration) where T : new() {
            return configuration.GetSection(GetDefaultName<T>());
        }

        public static T Map<T>(IConfiguration configuration) where T : new() {
            return GetSection<T>(configuration).Get<T>();
        }

        public static T Map<T>(IConfiguration configuration, IServiceCollection services) where T : class, new() {
            var section = GetSection<T>(configuration);
            services.Configure<T>(section);
            return section.Get<T>();
        }
    }
}