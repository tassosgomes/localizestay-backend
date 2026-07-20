namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal sealed record Property
{
    internal string Name { get; }
    internal string DestinationId { get; }
    internal Address Address { get; }

    /// <summary>
    /// Normalized key used to detect concurrent active onboarding cycles for the same physical
    /// property: <c>destinationId:countryCode:postalCode:normalized(street + ' ' + number)</c>.
    /// </summary>
    internal string SimilarityKey =>
        FormattableString.Invariant(
            $"{DestinationId}:{Address.CountryCode}:{Address.PostalCode}:{(Address.Street + ' ' + Address.Number).ToLowerInvariant().Replace(" ", "")}");

    private Property()
    {
        Name = string.Empty;
        DestinationId = string.Empty;
        Address = null!;
    }

    internal Property(string name, string destinationId, Address address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationId);
        ArgumentNullException.ThrowIfNull(address);

        if (name.Length is < 2 or > 180)
        {
            throw new ArgumentException("Name must be between 2 and 180 characters.", nameof(name));
        }

        if (destinationId.Length > 120)
        {
            throw new ArgumentException("DestinationId must be at most 120 characters.", nameof(destinationId));
        }

        Name = name.Trim();
        DestinationId = destinationId.Trim();
        Address = address;
    }
}

internal sealed record Address
{
    internal string Street { get; }
    internal string Number { get; }
    internal string? Complement { get; }
    internal string District { get; }
    internal string City { get; }
    internal string State { get; }
    internal string PostalCode { get; }
    internal string CountryCode { get; }

    private Address()
    {
        Street = string.Empty;
        Number = string.Empty;
        District = string.Empty;
        City = string.Empty;
        State = string.Empty;
        PostalCode = string.Empty;
        CountryCode = string.Empty;
    }

    internal Address(
        string street,
        string number,
        string? complement,
        string district,
        string city,
        string state,
        string postalCode,
        string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(street);
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        ArgumentException.ThrowIfNullOrWhiteSpace(district);
        ArgumentException.ThrowIfNullOrWhiteSpace(city);
        ArgumentException.ThrowIfNullOrWhiteSpace(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(postalCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        if (street.Length > 180)
        {
            throw new ArgumentException("Street must be at most 180 characters.", nameof(street));
        }

        if (number.Length > 30)
        {
            throw new ArgumentException("Number must be at most 30 characters.", nameof(number));
        }

        if (complement is not null && complement.Length > 120)
        {
            throw new ArgumentException("Complement must be at most 120 characters.", nameof(complement));
        }

        if (district.Length > 120)
        {
            throw new ArgumentException("District must be at most 120 characters.", nameof(district));
        }

        if (city.Length > 120)
        {
            throw new ArgumentException("City must be at most 120 characters.", nameof(city));
        }

        if (state.Length is < 2 or > 80)
        {
            throw new ArgumentException("State must be between 2 and 80 characters.", nameof(state));
        }

        if (postalCode.Length > 20)
        {
            throw new ArgumentException("PostalCode must be at most 20 characters.", nameof(postalCode));
        }

        if (countryCode.Length != 2 || !countryCode.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException("CountryCode must be a two-letter uppercase ISO code.", nameof(countryCode));
        }

        Street = street.Trim();
        Number = number.Trim();
        Complement = complement?.Trim();
        District = district.Trim();
        City = city.Trim();
        State = state.Trim();
        PostalCode = postalCode.Trim();
        CountryCode = countryCode.Trim();
    }
}
