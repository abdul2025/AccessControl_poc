using System.Security.Claims;
using AccessControl.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace AccessControl.Infrastructure.Auditing;

/// <summary>The signed-in web user. Hosts without an HTTP request (the Gateway) replace this registration.</summary>
internal sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public string? UserId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;
            return user?.Identity?.IsAuthenticated == true ? user.FindFirstValue(ClaimTypes.NameIdentifier) : null;
        }
    }
}
