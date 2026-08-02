namespace BunkerFlow.Integration.Validation;

public sealed record ValidationFailure(string Field, string Code, string Message);

public sealed record ValidationResult
{
    private static readonly ValidationResult ValidResult = new() { Failures = [] };

    public required IReadOnlyList<ValidationFailure> Failures { get; init; }

    public bool IsValid => Failures.Count == 0;

    public static ValidationResult Valid() => ValidResult;

    public static ValidationResult Invalid(IReadOnlyList<ValidationFailure> failures) =>
        new() { Failures = failures };

    public string Summary() =>
        string.Join("; ", Failures.Select(failure => $"{failure.Field}: {failure.Message}"));
}
