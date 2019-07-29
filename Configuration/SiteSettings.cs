using System;

namespace Starship.WebCore.Configuration {
    public class SiteSettings {

        public string Name { get; set; }

        public string Url { get; set; }

        public string Environment { get; set; }
        
        public bool IsProduction() {
            return Environment.ToLower() == "production";
        }
    }
}