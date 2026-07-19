namespace LocalizeStay.Contracts.CustomerCare;

/// <summary>
/// Public contract surface for the CustomerCare module: DTOs, internal-API interfaces and integration events
/// consumed by other modules. Other modules may reference only this assembly — never
/// LocalizeStay.Modules.CustomerCare internals (architecture baseline: Domain Interaction Principles —
/// "a dependência deve apontar para contratos estáveis do fornecedor, nunca para suas entidades,
/// tabelas, repositórios ou detalhes internos").
/// </summary>
public static class CustomerCareModuleContracts
{
    public const string ModuleName = "CustomerCare";
}
