using Microsoft.AspNetCore.Authorization;

namespace LocalizeStay.SharedKernel.Security;

/// <summary>
/// Typed configuration for the LogTo JWT bearer integration. Secrets are never stored here —
/// signing keys and connection strings come from the secret store in non-local environments
/// (production-readiness baseline). The contract is: every endpoint requires the <c>staff</c> scope
/// plus the operation-specific permission declared on the policy.
/// </summary>
public sealed class LogToOptions
{
    public const string SectionName = "LogTo";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// When true (default outside Development), missing or invalid LogTo configuration fails fast at
    /// startup. In Development we keep the host bootable without a real IdP so the integration test
    /// factory can plug its own test issuer in.
    /// </summary>
    public bool ValidateConfiguration { get; set; } = true;

    /// <summary>Claim type that carries the OAuth/OIDC scopes (LogTo default: <c>scope</c>).</summary>
    public string ScopeClaimType { get; set; } = "scope";

    /// <summary>Claim type that carries the fine-grained permissions (<c>x-required-permissions</c>).</summary>
    public string PermissionClaimType { get; set; } = "permission";
}

/// <summary>
/// Catalog of the high-level permissions declared on the F01 contract. Endpoints reference these
/// via <see cref="AuthorizationOptions"/>, never by raw string literals, so the surface stays auditable
/// (restful-api + roles-naming baselines: declared once, referenced everywhere).
/// </summary>
public static class PortfolioOnboardingPermissions
{
    public const string Read = "portfolio-onboarding:read";
    public const string Write = "portfolio-onboarding:write";
    public const string Submit = "portfolio-onboarding:submit";
    public const string Close = "portfolio-onboarding:close";
    public const string Metrics = "portfolio-onboarding:metrics";

    /// <summary>Every policy registered by <see cref="SecurityServiceCollectionExtensions.AddLocalizeStaySecurity"/>.</summary>
    public static readonly IReadOnlyCollection<string> All =
    [
        Read,
        Write,
        Submit,
        Close,
        Metrics,
    ];
}
