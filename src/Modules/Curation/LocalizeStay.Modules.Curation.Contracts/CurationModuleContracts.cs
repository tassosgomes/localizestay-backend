namespace LocalizeStay.Contracts.Curation;

/// <summary>
/// Public contract surface for the Curation module: DTOs, internal-API interfaces and integration events
/// consumed by other modules. Other modules may reference only this assembly — never
/// LocalizeStay.Modules.Curation internals (architecture baseline: Domain Interaction Principles —
/// "a dependência deve apontar para contratos estáveis do fornecedor, nunca para suas entidades,
/// tabelas, repositórios ou detalhes internos").
/// </summary>
public static class CurationModuleContracts
{
    public const string ModuleName = "Curation";
}
