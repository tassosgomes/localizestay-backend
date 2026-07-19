namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal sealed class ContractReference
{
    internal string RepositoryReference { get; private set; } = string.Empty;
    internal string? ContractNumber { get; private set; }
    internal DateTimeOffset SignedAt { get; private set; }
    internal List<string> ResponsibleParties { get; private set; } = [];
    private ContractReference() { }

    internal ContractReference(string repositoryReference, string? contractNumber, DateTimeOffset signedAt, IReadOnlyList<string> responsibleParties)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryReference);
        ArgumentNullException.ThrowIfNull(responsibleParties);
        if (repositoryReference.Length > 500 || contractNumber?.Length > 80 || responsibleParties.Count == 0 || responsibleParties.Any(party => string.IsNullOrWhiteSpace(party) || party.Length > 180))
            throw new ArgumentException("Contract reference contains invalid fields.");
        RepositoryReference = repositoryReference.Trim();
        ContractNumber = string.IsNullOrWhiteSpace(contractNumber) ? null : contractNumber.Trim();
        SignedAt = signedAt.ToUniversalTime();
        ResponsibleParties.AddRange(responsibleParties.Select(party => party.Trim()));
    }
}
