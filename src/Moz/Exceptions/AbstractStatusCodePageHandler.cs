using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moz.Bus.Dtos;
using Moz.Core;
using Moz.Core.Config;
using Newtonsoft.Json;

namespace Moz.Exceptions
{
    public abstract class AbstractStatusCodePageHandler:IStatusCodePageHandler
    {
        public async Task Process(StatusCodeContext context)
        {
            var statusCode = context.HttpContext.Response.StatusCode;
            if (IsApiCall(context.HttpContext))
            {
                await OnApiCallAsync(context, statusCode);
            }
            else
            {
                await OnWebCallAsync(context, statusCode);
            } 
        } 
        

        protected virtual bool IsApiCall(HttpContext httpContext)
        {
            var isAjaxRequest = httpContext.Request.IsAjaxRequest();
            if (isAjaxRequest)
                return true;

            var isAcceptJson = httpContext.Request.Headers["Accept"]
                                   .ToString()?
                                   .Contains("application/json", StringComparison.OrdinalIgnoreCase) ?? false;
            if (isAcceptJson)
                return true;

            var isApiController = httpContext.GetEndpoint()?.Metadata?.GetOrderedMetadata<ApiControllerAttribute>()?.Any() ?? false;
            if (isApiController)
                return true;

            return false;
        }

        protected virtual async Task OnApiCallAsync(StatusCodeContext context, int statusCode)
        {
            context.HttpContext.Response.ContentType = "application/json;charset=utf-8";
            context.HttpContext.Response.StatusCode = 200;
            await context.HttpContext.Response.WriteAsync(JsonConvert.SerializeObject(new
            {
                Code = statusCode,
                Message = ""
            }));
        }

        protected virtual async Task OnWebCallAsync(StatusCodeContext context, int statusCode)
        {
            var appConfig = EngineContext.Current.Resolve<IOptions<AppConfig>>()?.Value;
            
            var pathFormat = appConfig?.ErrorPage?.DefaultRedirect;
            var mode = ResponseMode.Redirect;
            
            var httpErrorConfig = appConfig?.ErrorPage?.HttpErrors?.FirstOrDefault(it => it.StatusCode == statusCode);
            if (httpErrorConfig != null)
            {
                pathFormat = httpErrorConfig.Path;
                mode = httpErrorConfig.Mode;
            }
            
            if (string.IsNullOrEmpty(pathFormat))
            {
                context.HttpContext.Response.ContentType = "text/html;charset=utf-8";
                context.HttpContext.Response.StatusCode = 200;
                await context.HttpContext.Response.WriteAsync($"???????????????????????????????????? {statusCode} ???????????????");
            }
            else
            {
                var originalPath = context.HttpContext.Request.Path;
                var originalQueryString = context.HttpContext.Request.QueryString;
                
                //?????? ?? ??? #question_mark#
                pathFormat = pathFormat.Replace("??", "__question_mark__");
  
                var newPath = new PathString(string.Format(CultureInfo.InvariantCulture, 
                    pathFormat, 
                    context.HttpContext.Response.StatusCode,
                    originalPath.Value,
                    originalQueryString.HasValue ? originalQueryString.Value : null));
                
                //?????? #question_mark# ??? ?
                var newPath1 = newPath.ToString().Replace("__question_mark__", "?");

                if (mode == ResponseMode.Redirect)
                {
                    context.HttpContext.Response.Redirect(newPath1);
                    return;
                }
                
                context.HttpContext.Features.Set<IStatusCodeReExecuteFeature>(new StatusCodeReExecuteFeature()
                {
                    OriginalPathBase = context.HttpContext.Request.PathBase.Value,
                    OriginalPath = originalPath.Value,
                    OriginalQueryString = originalQueryString.HasValue ? originalQueryString.Value : null,
                });

                context.HttpContext.SetEndpoint(endpoint: null);
                var routeValuesFeature = context.HttpContext.Features.Get<IRouteValuesFeature>();
                routeValuesFeature?.RouteValues?.Clear();

                context.HttpContext.Request.Path = new PathString(newPath1);
                try
                {
                    await context.Next(context.HttpContext);
                }
                finally
                {
                    context.HttpContext.Request.QueryString = originalQueryString;
                    context.HttpContext.Request.Path = originalPath;
                    context.HttpContext.Features.Set<IStatusCodeReExecuteFeature>(null);
                }
            }
        }
    }
}