using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;

namespace LocalizeStay.ArchitectureTests;

/// <summary>
/// Architecture baseline — Data Ownership Rules and Domain Interaction Principles: a module's
/// Contracts assembly is its only public surface; it must never leak the module's own
/// Infrastructure (or any other internal layer) to its consumers.
/// </summary>
public class ContractsTests
{
    public static IEnumerable<object[]> ContractAssemblies() =>
        ModuleAssemblies.Contracts.Select(pair => new object[] { pair.Key, pair.Value });

    [Theory]
    [MemberData(nameof(ContractAssemblies))]
    public void Contracts_should_not_depend_on_the_owning_modules_infrastructure(string moduleName, Assembly assembly)
    {
        var forbiddenNamespace = $"LocalizeStay.Modules.{moduleName}.Infrastructure";

        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(forbiddenNamespace)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"'{moduleName}.Contracts' must never reference '{forbiddenNamespace}': contracts describe " +
            "the module's public surface, they must not leak persistence details.");
    }

    [Theory]
    [MemberData(nameof(ContractAssemblies))]
    public void Contracts_should_not_depend_on_any_modules_internals(string moduleName, Assembly assembly)
    {
        var forbiddenNamespaces = ModuleAssemblies.Modules.Keys
            .Select(otherModuleName => $"LocalizeStay.Modules.{otherModuleName}")
            .ToArray();

        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"'{moduleName}.Contracts' must depend only on LocalizeStay.SharedKernel, never on any module's internals.");
    }
}
