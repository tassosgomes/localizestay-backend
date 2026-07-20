namespace LocalizeStay.SharedKernel.Auditing;

/// <summary>
/// Appends a <see cref="BusinessAuditEntry"/> to the caller's module-owned audit sink without
/// committing its own transaction (ADR-003). The caller is responsible for invoking
/// <c>SaveChangesAsync</c> as part of the same unit of work that produced the audited change, so a
/// rollback removes both the business mutation and the audit row together.
/// </summary>
/// <remarks>
/// The writer is HTTP-agnostic and never writes to a shared schema: each module registers a closed
/// generic <c>BusinessAuditWriter&lt;TDbContext&gt;</c> against its own <c>DbContext</c>, which is
/// the single owner of its <c>audit_entries</c> table.
/// </remarks>
public interface IBusinessAuditWriter
{
    /// <summary>Tracks <paramref name="entry"/> on the module's <c>DbContext</c> for the next <c>SaveChangesAsync</c>.</summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry"/> is <c>null</c>.</exception>
    public void Record(BusinessAuditEntry entry);
}
