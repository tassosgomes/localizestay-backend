namespace LocalizeStay.Contracts.Booking;

/// <summary>
/// Public contract surface for the Booking module: DTOs, internal-API interfaces and integration events
/// consumed by other modules. Other modules may reference only this assembly — never
/// LocalizeStay.Modules.Booking internals (architecture baseline: Domain Interaction Principles —
/// "a dependência deve apontar para contratos estáveis do fornecedor, nunca para suas entidades,
/// tabelas, repositórios ou detalhes internos").
/// </summary>
public static class BookingModuleContracts
{
    public const string ModuleName = "Booking";
}
