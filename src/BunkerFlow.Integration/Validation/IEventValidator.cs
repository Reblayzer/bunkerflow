using BunkerFlow.Contracts;

namespace BunkerFlow.Integration.Validation;

/// <summary>
/// Schema and data-quality gate. Runs after normalization and before anything
/// is published, so a bad record never reaches a downstream consumer.
/// </summary>
public interface IEventValidator
{
    ValidationResult Validate(IntegrationEvent integrationEvent);
}
