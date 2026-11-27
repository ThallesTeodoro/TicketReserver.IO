namespace TR.HW.Repl.Domain.Models;

public enum CdcOperation
{
    Create,
    Update,
    Delete,
    Read
}

public sealed record CdcEnvelope<T>(
    CdcOperation Operation,
    T? Before,
    T? After,
    long TimestampMs,
    string? SourceTable);
