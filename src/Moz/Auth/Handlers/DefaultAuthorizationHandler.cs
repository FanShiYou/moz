﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moz.Auth.Attributes;
using Moz.Bus.Models.Members;
using Moz.Core;

namespace Moz.Auth.Handlers
{
    public class DefaultAuthorizationHandler : AuthorizationHandler<DefaultAuthorizationRequirement>
    {
        private readonly IWorkContext _workContext;

        public DefaultAuthorizationHandler(IWorkContext workContext)
        {
            _workContext = workContext;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <param name="requirement"></param>
        /// <returns></returns>
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,DefaultAuthorizationRequirement requirement)
        {
            if (!(context.Resource is DefaultHttpContext defaultHttpContext))
            {
                context.Fail();
                return Task.CompletedTask;
            }

            var endpoint = defaultHttpContext.GetEndpoint();
            if (endpoint==null)
            {
                context.Fail();
                return Task.CompletedTask;
            }
            
            //context.Resource.

            //获取当前用户
            var member = _workContext.CurrentMember;
            if (member == null)
            {
                context.Fail();
                return Task.CompletedTask;
            }

            if (requirement.AdminOrMember == "admin")
            {
                if (!member.IsAdmin)
                {
                    context.Fail();
                    return Task.CompletedTask;
                }
                if (member.IsAdministrator)
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }

            //获取所有permission标签
            var attributes = endpoint.Metadata.GetOrderedMetadata<AdminAuthAttribute>();
            if (!attributes.Any())
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            //检查角色
            var isRoleAuth = CheckRole(member, attributes);
            var isPermissionAuth = CheckPermission(member, attributes);
            if (isRoleAuth && isPermissionAuth)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            context.Fail();
            return Task.CompletedTask;
        }

        /// <summary>
        /// </summary>
        /// <param name="member"></param>
        /// <param name="attributes"></param>
        /// <returns></returns>
        private bool CheckRole(SimpleMember member, IReadOnlyCollection<AdminAuthAttribute> attributes)
        {
            var allRoles = attributes.Where(t => !t.Roles.IsNullOrEmpty()).SelectMany(t =>
            {
                var rolesAry = new[] {t.Roles};
                if (t.Roles.Contains(",")) rolesAry = t.Roles.Split(",");
                return rolesAry;
            }).ToList();
            return !allRoles.Any() || allRoles.Any(member.InRole);
        }

        private bool CheckPermission(SimpleMember member, IEnumerable<AdminAuthAttribute> attributes)
        {
            var allPermissions = attributes
                .Where(t => !t.Permissions.IsNullOrEmpty())
                .SelectMany(t =>
                {
                    var permissionNameAry = new[] {t.Permissions};
                    if (t.Permissions.Contains(",")) permissionNameAry = t.Permissions.Split(",");
                    return permissionNameAry;
                }).ToList();
            return allPermissions.All(member.HasPermission);
        }
    }
}