using LocalizeStay.SharedKernel.ErrorHandling;

namespace LocalizeStay.Modules.Inventory.Domain.Partners;

public sealed class Partner
{
    public Guid Id { get; private set; }
    public string PreselectionId { get; private set; } = string.Empty;
    public string LegalName { get; private set; } = string.Empty;
    public string? TradeName { get; private set; }
    public LegalIdentifier LegalIdentifier { get; private set; } = null!;
    public Contact PrimaryContact { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Partner()
    {
    }

    public static Partner Create(
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

    public void UpdateLegalName(string legalName, DateTimeOffset updatedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legalName);

        if (legalName.Length is < 2 or > 180)
        {
            throw new ArgumentException("LegalName must be between 2 and 180 characters.", nameof(legalName));
        }

        LegalName = legalName.Trim();
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    public void UpdateTradeName(string? tradeName, DateTimeOffset updatedAt)
    {
        if (tradeName is not null && tradeName.Length > 180)
        {
            throw new ArgumentException("TradeName must be at most 180 characters.", nameof(tradeName));
        }

        TradeName = tradeName?.Trim();
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    public void UpdatePrimaryContact(Contact primaryContact, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(primaryContact);

        PrimaryContact = primaryContact;
        UpdatedAt = updatedAt.ToUniversalTime();
    }

    public void ChangeLegalIdentifier(LegalIdentifier legalIdentifier, DateTimeOffset updatedAt)
    {
        ArgumentNullException.ThrowIfNull(legalIdentifier);

        LegalIdentifier = legalIdentifier;
        UpdatedAt = updatedAt.ToUniversalTime();
    }
}
