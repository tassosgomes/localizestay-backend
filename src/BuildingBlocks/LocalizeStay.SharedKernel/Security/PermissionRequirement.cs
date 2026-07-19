using System.Security.Claims;
using LocalizeStay.SharedKernel.Correlation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalizeStay.SharedKernel.Security;

/// <summary>
/// Authorization requirement that combines the staff scope (verified by the JWT bearer handler via
/// the standard <c>scope</c> claim) with a high-level permission declared on the contract
/// (<c>x-required-permissions</c>). The host registers one policy per known permission and endpoints
/// reference the policy by name — never by raw string — so that 401 (no token) stays distinct from
/// 403 (token without the required permission) (ADR-002: JWT LogTo + policies por escopo/permissão).
/// </summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Validates that the current principal carries the configured permission. Permissions are matched
/// case-sensitively against the configured claim (<c>permission</c> by default) and the canonical
/// <c>staff</c> scope is enforced at JWT validation time, so this handler focuses only on the
/// per-operation permission (roles-naming baseline: SCREAMING_SNAKE_CASE for roles, kebab-case for
/// fine-grained permissions as declared in the OpenAPI contract).
/// </summary>
public sealed class PermissionHandler(
    ICorrelationIdAccessor correlationIdAccessor,
    IOptions<LogToOptions> logToOptions,
    ILogger<PermissionHandler> logger) : AuthorizationHandler<PermissionRequirement>
{
    private const string StaffScope = "staff";

    private string ScopeClaimType => logToOptions.Value.ScopeClaimType;
    private string PermissionClaimType => logToOptions.Value.PermissionClaimType;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            // The authentication middleware emits 401 before we get here, but keep the guard explicit
            // so the handler stays correct even if policies are evaluated outside the HTTP pipeline.
            return Task.CompletedTask;
        }

        if (!HasStaffScope(context.User))
        {
            logger.LogWarning(
                "Access denied: principal is missing the {Scope} scope. CorrelationId: {CorrelationId}.",
                StaffScope,
                correlationIdAccessor.CorrelationId);
            context.Fail(new AuthorizationFailureReason(this, "The required staff scope is missing."));
            return Task.CompletedTask;
        }

        if (!HasPermission(context.User, requirement.Permission))
        {
            logger.LogWarning(
                "Access denied: principal is missing permission {Permission}. CorrelationId: {CorrelationId}.",
                requirement.Permission,
                correlationIdAccessor.CorrelationId);
            context.Fail(new AuthorizationFailureReason(this, "The required permission is missing."));
            return Task.CompletedTask;
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }

    private bool HasStaffScope(ClaimsPrincipal principal)
    {
        foreach (var claim in principal.FindAll(ScopeClaimType))
        {
            foreach (var token in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(token, StaffScope, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasPermission(ClaimsPrincipal principal, string permission)
    {
        foreach (var claim in principal.FindAll(PermissionClaimType))
        {
            foreach (var token in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(token, permission, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// Authorization middleware that turns 403 (and the implicit 401 produced by authentication) into
/// RFC 9457 Problem Details responses that match the OpenAPI contract field by field
/// (<c>code: FORBIDDEN</c> / <c>code: UNAUTHORIZED</c>).
/// </summary>
public sealed class ForbiddenProblemDetailsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ICorrelationIdAccessor correlationIdAccessor)
    {
        await next(context);

        if (context.Response.StatusCode != StatusCodes.Status403Forbidden)
        {
            return;
        }

        // Only rewrite empty 403 responses produced by the authorization layer. Anything that already
        // has a body (e.g. handler-produced Problem Details) is left untouched.
        if (context.Response.ContentLength is > 0 || context.Response.HasStarted)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Type = "https://api.localizestay.com/problems/forbidden",
            Title = "Operação não autorizada",
            Status = StatusCodes.Status403Forbidden,
            Detail = "A identidade não possui a permissão declarada para esta operação.",
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = "FORBIDDEN";
        problem.Extensions["traceId"] = correlationIdAccessor.CorrelationId;

        await ProblemDetailsWriter.WriteAsync(context.Response, problem);
    }
}
