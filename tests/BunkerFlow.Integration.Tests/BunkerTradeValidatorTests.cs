using BunkerFlow.Integration.Normalization;
using BunkerFlow.Integration.Tests.TestSupport;
using BunkerFlow.Integration.Validation;

namespace BunkerFlow.Integration.Tests;

public sealed class BunkerTradeValidatorTests
{
    private readonly TestClock _clock = new();
    private readonly BunkerTradeNormalizer _normalizer;
    private readonly BunkerTradeValidator _validator;

    public BunkerTradeValidatorTests()
    {
        _normalizer = new BunkerTradeNormalizer(_clock);
        _validator = new BunkerTradeValidator(_clock);
    }

    [Fact]
    public void Should_accept_a_well_formed_trade()
    {
        var result = Validate(RecordBuilder.Valid());

        Assert.True(result.IsValid);
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Should_reject_an_IMO_number_with_a_bad_check_digit()
    {
        var result = Validate(RecordBuilder.Valid().With("vesselImo", RecordBuilder.InvalidImo));

        Assert.False(result.IsValid);
        Assert.Contains(result.Failures, failure => failure.Code == "invalid_imo");
    }

    [Theory]
    [InlineData("907472")]     // too short
    [InlineData("90747299")]   // too long
    [InlineData("90A4729")]    // not all digits
    public void Should_reject_an_IMO_number_that_is_not_seven_digits(string imo)
    {
        Assert.False(BunkerTradeValidator.IsValidImo(imo));
    }

    [Fact]
    public void Should_accept_a_known_good_IMO_number()
    {
        Assert.True(BunkerTradeValidator.IsValidImo(RecordBuilder.ValidImo));
    }

    [Fact]
    public void Should_reject_a_port_that_is_not_a_locode()
    {
        var result = Validate(RecordBuilder.Valid().With("port", "Fredericia"));

        Assert.Contains(result.Failures, failure => failure.Code == "invalid_locode");
    }

    [Fact]
    public void Should_reject_a_fuel_grade_the_platform_does_not_know()
    {
        var result = Validate(RecordBuilder.Valid().With("product", "JETA1"));

        Assert.Contains(result.Failures, failure => failure.Code == "unknown_product");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("25000")]
    public void Should_reject_an_implausible_quantity(string quantity)
    {
        var result = Validate(RecordBuilder.Valid().With("quantityMt", quantity));

        Assert.Contains(result.Failures, failure => failure.Field == "quantityMt");
    }

    [Fact]
    public void Should_reject_a_price_of_zero()
    {
        var result = Validate(RecordBuilder.Valid().With("priceUsdPerMt", "0"));

        Assert.Contains(result.Failures, failure => failure.Field == "priceUsdPerMt");
    }

    [Fact]
    public void Should_reject_a_trade_dated_in_the_future()
    {
        var tomorrow = TestClock.DefaultNow.AddDays(1).ToString("O");

        var result = Validate(RecordBuilder.Valid().With("tradedAtUtc", tomorrow));

        Assert.Contains(result.Failures, failure => failure.Code == "future_timestamp");
    }

    [Fact]
    public void Should_tolerate_small_clock_skew_on_a_source_timestamp()
    {
        var slightlyAhead = TestClock.DefaultNow.AddMinutes(2).ToString("O");

        var result = Validate(RecordBuilder.Valid().With("tradedAtUtc", slightlyAhead));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Should_report_every_broken_rule_not_just_the_first()
    {
        var result = Validate(RecordBuilder.Valid()
            .With("vesselImo", RecordBuilder.InvalidImo)
            .With("product", "JETA1")
            .With("priceUsdPerMt", "0"));

        Assert.Equal(3, result.Failures.Count);
    }

    private ValidationResult Validate(RecordBuilder builder) =>
        _validator.Validate(_normalizer.Normalize(builder.Build()));
}
