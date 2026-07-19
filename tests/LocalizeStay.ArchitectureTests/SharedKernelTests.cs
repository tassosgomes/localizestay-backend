using AwesomeAssertions;
using LocalizeStay.SharedKernel.Modules;
using NetArchTest.Rules;

namespace LocalizeStay.ArchitectureTests;

/// <summary>
/// Architecture baseline — Architecture Style: "Componentes compartilhados devem se limitar a
/// capacidades técnicas sem semântica de negócio". SharedKernel must never know about a specific
/// module, in either direction.
/// </summary>
public class SharedKernelTests
{
    [Fact]
    public void SharedKernel_should_not_depend_on_any_module()
    {
        var sharedKernelAssembly = typeof(IModule).Assembly;

        var result = Types.InAssembly(sharedKernelAssembly)
            .Should()
            .NotHaveDependencyOnAny("LocalizeStay.Modules", "LocalizeStay.Contracts")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "LocalizeStay.SharedKernel must stay free of business semantics and must not reference " +
            "any module or its contracts (architecture baseline: Estrutura interna).");
    }
}
