namespace LocalizeStay.Contracts.Discovery;

/// <summary>
/// Public contract surface for the Discovery module: DTOs, internal-API interfaces and integration events
/// consumed by other modules. Other modules may reference only this assembly — never
/// LocalizeStay.Modules.Discovery internals (architecture baseline: Domain Interaction Principles —
/// "a dependência deve apontar para contratos estáveis do fornecedor, nunca para suas entidades,
/// tabelas, repositórios ou detalhes internos").
/// </summary>
public static class DiscoveryModuleContracts
{
    public const string ModuleName = "Discovery";
}
