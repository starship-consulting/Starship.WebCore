using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Starship.WebCore.ErrorHandling {
    public class JsonExceptionHandlingMiddleware {

        private readonly RequestDelegate next;

        public JsonExceptionHandlingMiddleware(RequestDelegate next) {
            this.next = next;
        }

        public async Task Invoke(HttpContext context) {
            try {
                await next(context);
            }
            catch (Exception ex) {
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception ex) {
            var code = HttpStatusCode.InternalServerError;
            var result = JsonConvert.SerializeObject(new {error = ex.Message});
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int) code;
            return context.Response.WriteAsync(result);
        }
    }
}