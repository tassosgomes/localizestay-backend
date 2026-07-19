using FluentValidation;
using LocalizeStay.Modules.Inventory.Domain.Partners;
using LocalizeStay.Modules.Inventory.Infrastructure;
using LocalizeStay.SharedKernel.Cqrs;
using LocalizeStay.SharedKernel.ErrorHandling;
using Microsoft.EntityFrameworkCore;

namespace LocalizeStay.Modules.Inventory.Application.Partners;

internal sealed record GetPartnerQuery(Guid PartnerId) : IQuery<PartnerResponse>;
internal sealed record ListPartnersQuery(int Page, int Size, string? Search, string? LegalIdentifierType) : IQuery<PartnerListResponse>;
internal sealed record PartnerResponse(Guid Id, string PreselectionId, string LegalName, string? TradeName, LegalIdentifierResponse LegalIdentifier, ContactResponse PrimaryContact, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
internal sealed record LegalIdentifierResponse(string Type, string CountryCode, string Value);
internal sealed record ContactResponse(string Name, string Email, string Phone);
internal sealed record PartnerSummaryResponse(Guid Id, string PreselectionId, string LegalName, string? TradeName, string LegalIdentifierType, string MaskedLegalIdentifier, DateTimeOffset UpdatedAt);
internal sealed record PaginationResponse(int Page, int Size, int Total, int TotalPages);
internal sealed record PartnerListResponse(IReadOnlyList<PartnerSummaryResponse> Data, PaginationResponse Pagination);

internal sealed class GetPartnerQueryHandler(InventoryDbContext dbContext) : IQueryHandler<GetPartnerQuery, PartnerResponse>
{
    public async Task<PartnerResponse> HandleAsync(GetPartnerQuery query, CancellationToken cancellationToken)
    {
        var partner = await dbContext.Partners.AsNoTracking().SingleOrDefaultAsync(item => item.Id == query.PartnerId, cancellationToken)
            ?? throw new NotFoundException("Partner was not found.", "PARTNER_NOT_FOUND");
        return PartnerMapper.ToResponse(partner);
    }
}

internal sealed class ListPartnersQueryHandler(InventoryDbContext dbContext, FluentValidation.IValidator<ListPartnersQuery> validator) : IQueryHandler<ListPartnersQuery, PartnerListResponse>
{
    public async Task<PartnerListResponse> HandleAsync(ListPartnersQuery query, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(query, cancellationToken);
        var partners = dbContext.Partners.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            partners = partners.Where(partner => EF.Functions.ILike(partner.LegalName, $"%{search}%") || (partner.TradeName != null && EF.Functions.ILike(partner.TradeName, $"%{search}%")) || EF.Functions.ILike(partner.LegalIdentifier.NormalizedValue, $"%{PartnerMapper.NormalizeSearch(search)}%"));
        }
        if (!string.IsNullOrWhiteSpace(query.LegalIdentifierType))
        {
            var type = PartnerMapper.ParseLegalIdentifierType(query.LegalIdentifierType);
            partners = partners.Where(partner => partner.LegalIdentifier.Type == type);
        }
        var total = await partners.CountAsync(cancellationToken);
        var data = await partners.OrderBy(partner => partner.LegalName).ThenBy(partner => partner.Id).Skip((query.Page - 1) * query.Size).Take(query.Size).Select(partner => new PartnerSummaryResponse(partner.Id, partner.PreselectionId, partner.LegalName, partner.TradeName, PartnerMapper.ToContractType(partner.LegalIdentifier.Type), PartnerMapper.Mask(partner.LegalIdentifier.Value), partner.UpdatedAt)).ToListAsync(cancellationToken);
        return new PartnerListResponse(data, new PaginationResponse(query.Page, query.Size, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)query.Size)));
    }
}

internal static class PartnerMapper
{
    internal static PartnerResponse ToResponse(Partner partner) => new(partner.Id, partner.PreselectionId, partner.LegalName, partner.TradeName, new LegalIdentifierResponse(ToContractType(partner.LegalIdentifier.Type), partner.LegalIdentifier.CountryCode, partner.LegalIdentifier.Value), new ContactResponse(partner.PrimaryContact.Name, partner.PrimaryContact.Email, partner.PrimaryContact.Phone), partner.CreatedAt, partner.UpdatedAt);
    internal static LegalIdentifier ToLegalIdentifier(LegalIdentifierInput input) => new(ParseLegalIdentifierType(input.Type), input.CountryCode, input.Value);
    internal static Contact ToContact(ContactInput input) => new(input.Name, input.Email, input.Phone);
    internal static LegalIdentifierType ParseLegalIdentifierType(string type) => type.Trim().ToLowerInvariant() switch { "cnpj" => LegalIdentifierType.Cnpj, "cpf" => LegalIdentifierType.Cpf, "other" => LegalIdentifierType.Other, _ => throw new ArgumentException("Legal identifier type must be cnpj, cpf, or other.", nameof(type)) };
    internal static string ToContractType(LegalIdentifierType type) => type.ToString().ToLowerInvariant();
    internal static string NormalizeSearch(string value) => new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
    internal static string Mask(string value) => value.Length <= 4 ? value : string.Concat(value.AsSpan(0, Math.Min(2, value.Length / 4)), new string('*', value.Length - (2 * Math.Min(2, value.Length / 4))), value.AsSpan(value.Length - Math.Min(2, value.Length / 4)));
}
