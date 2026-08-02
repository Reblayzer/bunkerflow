using BunkerFlow.Contracts;

namespace BunkerFlow.Integration.Normalization;

/// <summary>
/// Turns a source-shaped record into the common event contract. One
/// implementation per record type; the pipeline itself stays source-agnostic.
/// </summary>
public interface IRecordNormalizer
{
    bool CanNormalize(SourceRecord record);

    /// <exception cref="Errors.NormalizationException">
    /// The record is missing a required field or a value cannot be parsed.
    /// </exception>
    IntegrationEvent Normalize(SourceRecord record);
}
