namespace LocalizeStay.Modules.Inventory.Domain.PropertyOnboardings;

internal sealed class CommunicationRecord
{
    internal Guid Id { get; private set; }
    internal CommunicationChannel Channel { get; private set; }
    internal DateTimeOffset ReceivedAt { get; private set; }
    internal DateTimeOffset ProcessedAt { get; private set; }
    internal string ResultSummary { get; private set; } = string.Empty;
    internal bool ProcessedWithinSla { get; private set; }
    internal string CreatedBy { get; private set; } = string.Empty;
    internal DateTimeOffset CreatedAt { get; private set; }

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
