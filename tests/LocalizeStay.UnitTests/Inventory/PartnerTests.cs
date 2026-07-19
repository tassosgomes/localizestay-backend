using AwesomeAssertions;
using LocalizeStay.Modules.Inventory.Domain.Partners;

namespace LocalizeStay.UnitTests.Inventory;

public class PartnerTests
{
    private static LegalIdentifier CreateLegalIdentifier()
        => new(LegalIdentifierType.Cnpj, "BR", "12.345.678/0001-90");

    private static Contact CreateContact()
        => new("Marina Almeida", "marina@example.com", "+55 81 99999-0101");

    private static Partner CreatePartner()
    {
        return Partner.Create(
            Guid.NewGuid(),
            "preselection-2026-0042",
            "Mar do Sol Hospedagens Ltda.",
            "Pousada Mar do Sol",
            CreateLegalIdentifier(),
            CreateContact(),
            DateTimeOffset.Parse("2026-07-18T13:00:00Z"));
    }

    [Fact]
    public void Create_WithValidData_ShouldCreatePartner()
    {
        var id = Guid.NewGuid();
        var legalIdentifier = CreateLegalIdentifier();
        var contact = CreateContact();
        var createdAt = DateTimeOffset.Parse("2026-07-18T13:00:00Z");

        var partner = Partner.Create(
            id,
            "preselection-2026-0042",
            "Mar do Sol Hospedagens Ltda.",
            "Pousada Mar do Sol",
            legalIdentifier,
            contact,
            createdAt);

        partner.Id.Should().Be(id);
        partner.PreselectionId.Should().Be("preselection-2026-0042");
        partner.LegalName.Should().Be("Mar do Sol Hospedagens Ltda.");
        partner.TradeName.Should().Be("Pousada Mar do Sol");
        partner.LegalIdentifier.Should().Be(legalIdentifier);
        partner.PrimaryContact.Should().Be(contact);
        partner.CreatedAt.Should().Be(createdAt);
        partner.UpdatedAt.Should().Be(createdAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidPreselectionId_ShouldThrow(string? preselectionId)
    {
        var act = () => Partner.Create(
            Guid.NewGuid(),
            preselectionId!,
            "Mar do Sol Hospedagens Ltda.",
            null,
            CreateLegalIdentifier(),
            CreateContact(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("A")]
    [InlineData("")]
    public void Create_WithInvalidLegalName_ShouldThrow(string legalName)
    {
        var act = () => Partner.Create(
            Guid.NewGuid(),
            "preselection-2026-0042",
            legalName,
            null,
            CreateLegalIdentifier(),
            CreateContact(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithLegalNameTooLong_ShouldThrow()
    {
        var legalName = new string('a', 181);

        var act = () => Partner.Create(
            Guid.NewGuid(),
            "preselection-2026-0042",
            legalName,
            null,
            CreateLegalIdentifier(),
            CreateContact(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithTradeNameTooLong_ShouldThrow()
    {
        var tradeName = new string('a', 181);

        var act = () => Partner.Create(
            Guid.NewGuid(),
            "preselection-2026-0042",
            "Mar do Sol Hospedagens Ltda.",
            tradeName,
            CreateLegalIdentifier(),
            CreateContact(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateLegalName_ShouldUpdateNameAndTimestamp()
    {
        var partner = CreatePartner();
        var updatedAt = DateTimeOffset.Parse("2026-07-19T10:00:00Z");

        partner.UpdateLegalName("Mar do Sol Hospedagens e Turismo Ltda.", updatedAt);

        partner.LegalName.Should().Be("Mar do Sol Hospedagens e Turismo Ltda.");
        partner.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void UpdateTradeName_ShouldUpdateTradeNameAndTimestamp()
    {
        var partner = CreatePartner();
        var updatedAt = DateTimeOffset.Parse("2026-07-19T10:00:00Z");

        partner.UpdateTradeName("Pousada Mar do Sol Boutique", updatedAt);

        partner.TradeName.Should().Be("Pousada Mar do Sol Boutique");
        partner.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void UpdatePrimaryContact_ShouldUpdateContactAndTimestamp()
    {
        var partner = CreatePartner();
        var updatedAt = DateTimeOffset.Parse("2026-07-19T10:00:00Z");
        var newContact = new Contact("Carlos Silva", "carlos@example.com", "+55 81 99999-0202");

        partner.UpdatePrimaryContact(newContact, updatedAt);

        partner.PrimaryContact.Should().Be(newContact);
        partner.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void ChangeLegalIdentifier_ShouldUpdateIdentifierAndTimestamp()
    {
        var partner = CreatePartner();
        var updatedAt = DateTimeOffset.Parse("2026-07-19T10:00:00Z");
        var newIdentifier = new LegalIdentifier(LegalIdentifierType.Other, "US", "EIN-123456789");

        partner.ChangeLegalIdentifier(newIdentifier, updatedAt);

        partner.LegalIdentifier.Should().Be(newIdentifier);
        partner.UpdatedAt.Should().Be(updatedAt);
    }

    [Fact]
    public void UpdateLegalName_WithInvalidName_ShouldThrow()
    {
        var partner = CreatePartner();

        var act = () => partner.UpdateLegalName("A", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithNullLegalIdentifier_ShouldThrow()
    {
        var act = () => Partner.Create(
            Guid.NewGuid(),
            "preselection-2026-0042",
            "Mar do Sol Hospedagens Ltda.",
            null,
            null!,
            CreateContact(),
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithNullContact_ShouldThrow()
    {
        var act = () => Partner.Create(
            Guid.NewGuid(),
            "preselection-2026-0042",
            "Mar do Sol Hospedagens Ltda.",
            null,
            CreateLegalIdentifier(),
            null!,
            DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("A", "email@example.com", "+55 81 99999-0101")]
    [InlineData("Valid Name", "email@example.com", "123")]
    [InlineData("Valid Name", "email@example.com", "a very long phone number that exceeds thirty characters")]
    public void Create_WithInvalidContact_ShouldThrow(string name, string email, string phone)
    {
        var act = () => new Contact(name, email, phone);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithEmailTooLong_ShouldThrow()
    {
        var email = new string('a', 250) + "@example.com";

        var act = () => new Contact("Valid Name", email, "+55 81 99999-0101");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithInvalidLegalIdentifierType_ShouldThrow()
    {
        var act = () => new LegalIdentifier((LegalIdentifierType)999, "BR", "12.345.678/0001-90");

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("BRA", "12.345.678/0001-90")]
    [InlineData("br", "12.345.678/0001-90")]
    [InlineData("B1", "12.345.678/0001-90")]
    public void Create_WithInvalidCountryCode_ShouldThrow(string countryCode, string value)
    {
        var act = () => new LegalIdentifier(LegalIdentifierType.Cnpj, countryCode, value);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("1234")]
    [InlineData(null)]
    public void Create_WithInvalidLegalIdentifierValue_ShouldThrow(string? value)
    {
        var act = () => new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", value!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_WithLegalIdentifierValueTooLong_ShouldThrow()
    {
        var value = new string('1', 41);

        var act = () => new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", value);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LegalIdentifier_MaskedValue_ShouldMaskMiddle()
    {
        var identifier = new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", "12.345.678/0001-90");

        var masked = identifier.MaskedValue;

        masked.Should().Contain("*");
    }

    [Fact]
    public void LegalIdentifier_NormalizedValue_ShouldRemoveNonDigitsForCnpj()
    {
        var identifier = new LegalIdentifier(LegalIdentifierType.Cnpj, "BR", "12.345.678/0001-90");

        identifier.NormalizedValue.Should().Be("12345678000190");
    }

    [Fact]
    public void LegalIdentifier_NormalizedValue_ShouldUpperCaseForOther()
    {
        var identifier = new LegalIdentifier(LegalIdentifierType.Other, "US", "ein-123456789");

        identifier.NormalizedValue.Should().Be("EIN-123456789");
    }
}
