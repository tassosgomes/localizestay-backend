namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

public sealed record Property
{
    public string Name { get; }
    public string DestinationId { get; }
    public Address Address { get; }

    public Property(string name, string destinationId, Address address)
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

public sealed record Address
{
    public string Street { get; }
    public string Number { get; }
    public string? Complement { get; }
    public string District { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string CountryCode { get; }

    public Address(
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
