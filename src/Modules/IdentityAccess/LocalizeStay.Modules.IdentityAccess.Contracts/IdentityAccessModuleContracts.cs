namespace LocalizeStay.Contracts.IdentityAccess;

/// <summary>
/// Public contract surface for the IdentityAccess module: DTOs, internal-API interfaces and integration events
/// consumed by other modules. Other modules may reference only this assembly — never
/// LocalizeStay.Modules.IdentityAccess internals (architecture baseline: Domain Interaction Principles —
/// "a dependência deve apontar para contratos estáveis do fornecedor, nunca para suas entidades,
/// tabelas, repositórios ou detalhes internos").
/// </summary>
public static class IdentityAccessModuleContracts
{
    public const string ModuleName = "IdentityAccess";
}
