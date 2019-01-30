using System;
using Microsoft.Extensions.Configuration;

namespace Starship.WebCore.Configuration {
    public static class ConfigurationMapper {
        
        public static T Map<T>(IConfiguration configuration) where T : new() {
            return configuration.GetSection(typeof(T).Name.Replace("Settings", "")).Get<T>();
        }
    }
}