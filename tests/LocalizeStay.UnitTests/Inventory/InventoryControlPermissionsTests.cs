using System.Security.Claims;
using AwesomeAssertions;
using LocalizeStay.SharedKernel.Correlation;
using LocalizeStay.SharedKernel.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace LocalizeStay.UnitTests.Inventory;

public sealed class InventoryControlPermissionsTests
{
    public static TheoryData<string> AllPermissions =>
    [
        InventoryControlPermissions.Read,
        InventoryControlPermissions.Write,
        InventoryControlPermissions.Block,
        InventoryControlPermissions.Hold,
        InventoryControlPermissions.Metrics,
    ];

    private static PermissionHandler CreateHandler()
    {
        return new PermissionHandler(
            new StubCorrelationIdAccessor(),
            Options.Create(new LogToOptions()),
            Mock.Of<ILogger<PermissionHandler>>());
    }

    private static ClaimsPrincipal CreatePrincipal(string? scope = "staff", params string[] permissions)
    {
        var claims = new List<Claim>();
        if (scope is not null)
        {
            claims.Add(new Claim("scope", scope));
        }

        foreach (var permission in permissions)
        {
            claims.Add(new Claim("permission", permission));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private static async Task<AuthorizationHandlerContext> EvaluateAsync(ClaimsPrincipal principal, string permission)
    {
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], principal, resource: null);
        await CreateHandler().HandleAsync(context);
        return context;
    }

    // --- 2.2 Policies ---

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public async Task GetPolicyAsync_ForEachInventoryPermission_ShouldResolvePolicyRequiringAuthenticationAndPermission(string permission)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LogTo:ValidateConfiguration"] = "false",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLocalizeStaySecurity(configuration);
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync(permission);

        policy.Should().NotBeNull();
        policy!.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().Should().ContainSingle();
        policy.Requirements.OfType<PermissionRequirement>().Should().ContainSingle(r => r.Permission == permission);
        policy.AuthenticationSchemes.Should().Contain(SecurityServiceCollectionExtensions.AuthenticationScheme);
    }

    // --- 2.3 Handler ---

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public async Task Handle_WithStaffScopeAndMatchingPermission_ShouldSucceed(string permission)
    {
        var principal = CreatePrincipal(permissions: permission);

        var context = await EvaluateAsync(principal, permission);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WithWritePermissionOnly_ShouldFailReadRequirement()
    {
        var principal = CreatePrincipal(permissions: InventoryControlPermissions.Write);

        var context = await EvaluateAsync(principal, InventoryControlPermissions.Read);

        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public async Task Handle_WithoutStaffScope_ShouldFailEveryPolicy(string permission)
    {
        var principal = CreatePrincipal(scope: null, permissions: permission);

        var context = await EvaluateAsync(principal, permission);

        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeTrue();
    }

    private sealed class StubCorrelationIdAccessor : ICorrelationIdAccessor
    {
        public string CorrelationId => "test-correlation-id";
    }
}
