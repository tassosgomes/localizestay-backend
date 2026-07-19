using System.Reflection;
using AwesomeAssertions;
using NetArchTest.Rules;

namespace LocalizeStay.ArchitectureTests;

/// <summary>
/// Architecture baseline — Domain Interaction Principles: "a dependência deve apontar para
/// contratos estáveis do fornecedor, nunca para suas entidades, tabelas, repositórios ou detalhes
/// internos". A module may depend on other modules' Contracts assemblies (namespace
/// <c>LocalizeStay.Contracts.*</c>) and on SharedKernel, but never on another module's own internal
/// namespace (<c>LocalizeStay.Modules.&lt;Other&gt;</c>).
/// </summary>
public class ModuleBoundaryTests
{
    public static IEnumerable<object[]> ModulesWithForbiddenNamespaces()
    {
        foreach (var (moduleName, assembly) in ModuleAssemblies.Modules)
        {
            var forbiddenNamespaces = ModuleAssemblies.Modules.Keys
                .Where(otherModuleName => otherModuleName != moduleName)
                .Select(otherModuleName => $"LocalizeStay.Modules.{otherModuleName}")
                .ToArray();

            yield return [moduleName, assembly, forbiddenNamespaces];
        }
    }

    [Theory]
    [MemberData(nameof(ModulesWithForbiddenNamespaces))]
    public void Module_should_not_depend_on_another_modules_internals(string moduleName, Assembly assembly, string[] forbiddenNamespaces)
    {
        var result = Types.InAssembly(assembly)
            .Should()
            .NotHaveDependencyOnAny(forbiddenNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"module '{moduleName}' must depend only on other modules' Contracts assemblies, but " +
            $"the following types reference another module's internals: " +
            $"{string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
