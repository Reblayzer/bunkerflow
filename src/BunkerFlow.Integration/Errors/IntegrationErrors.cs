namespace BunkerFlow.Integration.Errors;

/// <summary>
/// Base type for every error raised at an integration boundary. Handlers map
/// these to an outcome; services never throw a bare Exception across a
/// boundary.
/// </summary>
public abstract class IntegrationException : Exception
{
    protected IntegrationException(string message, Exception? inner = null)
        : base(message, inner) { }

    public abstract string Code { get; }
}

/// <summary>A source record could not be turned into an integration event.</summary>
public sealed class NormalizationException : IntegrationException
{
    public NormalizationException(string message, Exception? inner = null)
        : base(message, inner) { }

    public override string Code => "normalization_failed";
}

/// <summary>
/// The broker refused the message in a way that is worth retrying: a timeout,
/// a throttle, a dropped connection.
/// </summary>
public sealed class TransientPublishException : IntegrationException
{
    public TransientPublishException(string message, Exception? inner = null)
        : base(message, inner) { }

    public override string Code => "publish_transient";
}

/// <summary>
/// The broker refused the message in a way retrying cannot fix: the entity is
/// missing, the payload is too large, credentials are wrong.
/// </summary>
public sealed class PermanentPublishException : IntegrationException
{
    public PermanentPublishException(string message, Exception? inner = null)
        : base(message, inner) { }

    public override string Code => "publish_permanent";
}
