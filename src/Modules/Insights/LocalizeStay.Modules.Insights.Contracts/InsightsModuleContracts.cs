namespace LocalizeStay.Contracts.Insights;

/// <summary>
/// Public contract surface for the Insights module: DTOs, internal-API interfaces and integration events
/// consumed by other modules. Other modules may reference only this assembly — never
/// LocalizeStay.Modules.Insights internals (architecture baseline: Domain Interaction Principles —
/// "a dependência deve apontar para contratos estáveis do fornecedor, nunca para suas entidades,
/// tabelas, repositórios ou detalhes internos").
/// </summary>
public static class InsightsModuleContracts
{
    public const string ModuleName = "Insights";
}
