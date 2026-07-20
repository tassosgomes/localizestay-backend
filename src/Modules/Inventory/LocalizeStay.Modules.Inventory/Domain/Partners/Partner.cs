using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.Modules.Inventory.Domain.Partners;

internal sealed class Partner
{
    internal Guid Id { get; private set; }
    internal string PreselectionId { get; private set; } = string.Empty;
    internal string LegalName { get; private set; } = string.Empty;
    internal string? TradeName { get; private set; }
    internal LegalIdentifier LegalIdentifier { get; private set; } = null!;
    internal Contact PrimaryContact { get; private set; } = null!;
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }

    private Partner()
    {
    }

    internal static Partner Create(
        Guid id,
        string preselectionId,
        string legalName,
        string? tradeName,
        LegalIdentifier legalIdentifier,
        Contact primaryContact,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(preselectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(legalName);
        ArgumentNullException.ThrowIfNull(legalIdentifier);
        ArgumentNullException.ThrowIfNull(primaryContact);

        if (preselectionId.Length > 100)
        {
            throw new ArgumentException("PreselectionId must be at most 100 characters.", nameof(preselectionId));
        }

        if (legalName.Length is < 2 or > 180)
        {
            throw new ArgumentException("LegalName must be between 2 and 180 characters.", nameof(legalName));
        }

        if (tradeName is not null && tradeName.Length > 180)
        {
            throw new ArgumentException("TradeName must be at most 180 characters.", nameof(tradeName));
        }

        return new Partner
        {
            Id = id,
            PreselectionId = preselectionId.Trim(),
            LegalName = legalName.Trim(),
            TradeName = tradeName?.Trim(),
            LegalIdentifier = legalIdentifier,
            PrimaryContact = primaryContact,
            CreatedAt = createdAt.ToUniversalTime(),
            UpdatedAt = createdAt.ToUniversalTime(),
        };
    }

    internal void UpdateLegalName(string legalName, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legalName);

        if (legalName.Length is < 2 or > 180)
        {
            throw new ArgumentException("LegalName must be between 2 and 180 characters.", nameof(legalName));
        }

        LegalName = legalName.Trim();
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    internal void UpdateTradeName(string? tradeName, DateTimeOffset updatedAt)
    {
        if (tradeName is not null && tradeName.Length > 180)
        {
            throw new ArgumentException("TradeName must be at most 180 characters.", nameof(tradeName));
        }

        TradeName = tradeName?.Trim();
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    internal void UpdatePrimaryContact(Contact primaryContact, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(primaryContact);

        PrimaryContact = primaryContact;
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    internal void ChangeLegalIdentifier(LegalIdentifier legalIdentifier, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(legalIdentifier);

        LegalIdentifier = legalIdentifier;
        UpdatedAt = updatedAt.ToUniversalTime();
    }
}
