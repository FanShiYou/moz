using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Moz.Core;
using Moz.Core.Config;

namespace Moz.Admin.Api.Common
{
    
    public class ApiAdminRouteAttribute:RouteAttribute
    {  
        private static readonly string AdminPath;
        public ApiAdminRouteAttribute() :  
            base($"{AdminPath}/api/[controller]") 
        {
        }

        static ApiAdminRouteAttribute()
        {
            AdminPath = EngineContext.Current.Resolve<IOptions<AppConfig>>()?.Value?.AdminPath ?? "myadmin";
        }
    }
}