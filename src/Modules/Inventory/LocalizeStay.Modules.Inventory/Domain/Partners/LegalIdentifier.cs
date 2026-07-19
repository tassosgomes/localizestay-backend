using System.Text.RegularExpressions;

namespace LocalizeStay.Modules.Inventory.Domain.Partners;

public sealed partial record LegalIdentifier
{
    private const int MaxValueLength = 40;
    private const int IsoCountryCodeLength = 2;

    public LegalIdentifierType Type { get; }
    public string CountryCode { get; }
    public string Value { get; }
    public string NormalizedValue { get; }

    public LegalIdentifier(LegalIdentifierType type, string countryCode, string value)
    {
        if (type != LegalIdentifierType.Cnpj
            && type != LegalIdentifierType.Cpf
            && type != LegalIdentifierType.Other)
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Unsupported legal identifier type.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (countryCode.Length != IsoCountryCodeLength || !countryCode.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException("Country code must be a two-letter uppercase ISO 3166-1 alpha-2 code.", nameof(countryCode));
        }

        var trimmedValue = value.Trim();
        if (trimmedValue.Length is < 5 or > MaxValueLength)
        {
            throw new ArgumentException($"Legal identifier value must be between 5 and {MaxValueLength} characters.", nameof(value));
        }

        Type = type;
        CountryCode = countryCode;
        Value = trimmedValue;
        NormalizedValue = Normalize(type, trimmedValue);
    }

    public string MaskedValue => Mask(Value);

    private static string Normalize(LegalIdentifierType type, string value)
    {
        var digitsOnly = DigitsOnly().Replace(value, string.Empty);

        return type switch
        {
            LegalIdentifierType.Cnpj or LegalIdentifierType.Cpf => digitsOnly,
            _ => value.ToUpperInvariant(),
        };
    }

    private static string Mask(string value)
    {
        if (value.Length <= 4)
        {
            return value;
        }

        var visiblePrefix = Math.Min(2, value.Length / 4);
        var visibleSuffix = Math.Min(2, value.Length / 4);
        var maskedLength = value.Length - visiblePrefix - visibleSuffix;

        return string.Concat(
            value.AsSpan(0, visiblePrefix),
            new string('*', maskedLength),
            value.AsSpan(value.Length - visibleSuffix));
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnly();
}
