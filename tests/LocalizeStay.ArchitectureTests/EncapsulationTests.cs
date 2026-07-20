using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;

namespace LocalizeStay.ArchitectureTests;

/// <summary>
/// Architecture baseline — Estrutura interna: only a module's Contracts assembly is public. Its
/// Domain, Application and Infrastructure layers default to <c>internal</c> so the compiler itself
/// blocks other modules from reaching in (dotnet-code-quality: DI/testability without leaking
/// implementation details).
/// </summary>
public class EncapsulationTests
{
    private static readonly string[] _internalLayers = ["Domain", "Application", "Infrastructure"];

    public static IEnumerable<object[]> ModulesWithLayers()
    {
        foreach (var (moduleName, assembly) in ModuleAssemblies.Modules)
        {
            foreach (var layer in _internalLayers)
            {
                yield return [moduleName, assembly, layer];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ModulesWithLayers))]
    public void Domain_application_and_infrastructure_types_should_not_be_public(string moduleName, Assembly assembly, string layer)
    {
        var result = Types.InAssembly(assembly)
            .That()
            .ResideInNamespaceStartingWith($"LocalizeStay.Modules.{moduleName}.{layer}")
            .Should()
            .NotBePublic()
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"types under '{moduleName}.{layer}' must stay internal; only '{moduleName}.Contracts' is public.");
    }
}
