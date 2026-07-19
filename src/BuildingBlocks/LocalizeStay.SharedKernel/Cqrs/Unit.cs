namespace LocalizeStay.SharedKernel.Cqrs;

/// <summary>Represents the absence of a meaningful value for commands that do not return data.</summary>
public readonly struct Unit
{
    public static readonly Unit Value = new();
}
