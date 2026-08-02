using BunkerFlow.Api.Endpoints;
using BunkerFlow.Integration.Validation;

namespace BunkerFlow.Integration.Tests;

/// <summary>
/// The simulated sources feed demos and the batch worker. If their data is
/// quietly wrong, the rejection rate looks alarming for no real reason.
/// </summary>
public sealed class MockSourceDataTests
{
    [Fact]
    public void Every_mock_vessel_should_have_a_valid_IMO_check_digit()
    {
        Assert.All(MockSourceEndpoints.Vessels,
            imo => Assert.True(BunkerTradeValidator.IsValidImo(imo), $"{imo} has a bad check digit."));
    }

    [Fact]
    public void The_deliberately_broken_IMO_should_actually_be_broken()
    {
        Assert.False(BunkerTradeValidator.IsValidImo(MockSourceEndpoints.InvalidVesselImo));
    }
}
