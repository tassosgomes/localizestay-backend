namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

public sealed class CommunicationRecord
{
    public Guid Id { get; private set; }
    public CommunicationChannel Channel { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public DateTimeOffset ProcessedAt { get; private set; }
    public string ResultSummary { get; private set; } = string.Empty;
    public bool ProcessedWithinSla { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private CommunicationRecord()
    {
    }

    internal static CommunicationRecord Create(
        Guid id,
        CommunicationChannel channel,
        DateTimeOffset receivedAt,
        DateTimeOffset processedAt,
        string resultSummary,
        TimeSpan sla,
        string createdBy,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resultSummary);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        if (resultSummary.Length is < 3 or > 1000)
        {
            throw new ArgumentException("ResultSummary must be between 3 and 1000 characters.", nameof(resultSummary));
        }

        var processedUtc = processedAt.ToUniversalTime();
        var receivedUtc = receivedAt.ToUniversalTime();

        return new CommunicationRecord
        {
            Id = id,
            Channel = channel,
            ReceivedAt = receivedUtc,
            ProcessedAt = processedUtc,
            ResultSummary = resultSummary.Trim(),
            ProcessedWithinSla = processedUtc <= receivedUtc.Add(sla),
            CreatedBy = createdBy.Trim(),
            CreatedAt = createdAt.ToUniversalTime(),
        };
    }
}
