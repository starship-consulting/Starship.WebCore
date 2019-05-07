using System;
using Microsoft.Extensions.Configuration;

namespace Starship.WebCore.Configuration {
    public static class ConfigurationMapper {
        
        public static string GetDefaultName<T>() where T : new() {
            return typeof(T).Name.Replace("Settings", "");
        }

        public static IConfigurationSection GetSection<T>(IConfiguration configuration) where T : new() {
            return configuration.GetSection(typeof(T).Name.Replace("Settings", ""));
        }

        public static T Map<T>(IConfiguration configuration) where T : new() {
            return configuration.GetSection(typeof(T).Name.Replace("Settings", "")).Get<T>();
        }
    }
}