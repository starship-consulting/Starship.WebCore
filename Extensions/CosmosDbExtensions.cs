﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Starship.Data.Entities;

namespace Starship.WebCore.Extensions {
    public static class CosmosDbExtensions {
        
        public static IActionResult ToJsonResult(this DocumentEntity document, JsonSerializerSettings settings) {
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(document));
            return new JsonResult(data, settings);
        }

        public static IActionResult ToJsonResult(this IEnumerable<object> documents, JsonSerializerSettings settings) {
            var data = documents.Select(each => JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(each)));
            return new JsonResult(data, settings);
        }

        public static string ToJson(this IEnumerable<object> documents) {
            var data = documents.Select(each => JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(each)));
            return JsonConvert.SerializeObject(data);
        }
    }
}