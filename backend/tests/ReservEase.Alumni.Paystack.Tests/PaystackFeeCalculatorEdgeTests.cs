using ReservEase.Alumni.Paystack.Sdk.Services;

namespace ReservEase.Alumni.Paystack.Tests;

public class PaystackFeeCalculatorEdgeTests
{
    [Fact]
    public void Platform_fee_rounds_half_away_from_zero()
    {
        // 1.5% of 10 pesewas = 0.15 -> 0; 1.5% of 50 = 0.75 -> 1; 5% of 10 = 0.5 -> 1
        Assert.Equal(0, PaystackFeeCalculator.CalculateZeroDeductionCharge(10, 1.5m, 1.95m).PlatformFeeSubunit);
        Assert.Equal(1, PaystackFeeCalculator.CalculateZeroDeductionCharge(50, 1.5m, 1.95m).PlatformFeeSubunit);
        Assert.Equal(1, PaystackFeeCalculator.CalculateZeroDeductionCharge(10, 5m, 1.95m).PlatformFeeSubunit);
    }

    [Fact]
    public void Transaction_charge_is_platform_fee_plus_gateway_fee()
    {
        var r = PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 1.5m, 1.95m, gatewayFeeSafetyBufferSubunit: 2);
        Assert.Equal(r.PlatformFeeSubunit + r.GatewayFeeSubunit, r.TransactionChargeSubunit);
    }

    [Fact]
    public void Charge_always_equals_school_plus_platform_plus_gateway()
    {
        foreach (var amount in new long[] { 1, 99, 100, 12345, 999999, 50_000_000 })
        {
            var r = PaystackFeeCalculator.CalculateZeroDeductionCharge(amount, 2.5m, 1.95m, 10, null, 3);
            Assert.Equal(r.SchoolAmountSubunit + r.PlatformFeeSubunit + r.GatewayFeeSubunit, r.ChargeAmountSubunit);
            Assert.Equal(amount, r.SchoolAmountSubunit);
        }
    }

    [Fact]
    public void Gateway_fee_cap_makes_the_fee_flat_for_large_amounts()
    {
        var capped = PaystackFeeCalculator.CalculateZeroDeductionCharge(100_000_000, 1m, 1.95m, gatewayFeeCapSubunit: 10_000);
        Assert.Equal(10_000, capped.GatewayFeeSubunit);
        Assert.Equal(100_000_000 + capped.PlatformFeeSubunit + 10_000, capped.ChargeAmountSubunit);
    }

    [Fact]
    public void Gateway_fee_cap_is_ignored_when_the_fee_is_below_it()
    {
        var uncapped = PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 1m, 1.95m);
        var withHighCap = PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 1m, 1.95m, gatewayFeeCapSubunit: 1_000_000);
        Assert.Equal(uncapped, withHighCap);
    }

    [Fact]
    public void Safety_buffer_is_added_after_a_cap_too()
    {
        var r = PaystackFeeCalculator.CalculateZeroDeductionCharge(100_000_000, 0m, 1.95m, gatewayFeeCapSubunit: 10_000, gatewayFeeSafetyBufferSubunit: 5);
        Assert.Equal(10_005, r.GatewayFeeSubunit);
    }

    [Fact]
    public void Zero_platform_fee_and_zero_gateway_rate_charge_exactly_the_school_amount()
    {
        var r = PaystackFeeCalculator.CalculateZeroDeductionCharge(5000, 0m, 0m);
        Assert.Equal(5000, r.ChargeAmountSubunit);
        Assert.Equal(0, r.GatewayFeeSubunit);
        Assert.Equal(0, r.PlatformFeeSubunit);
    }

    [Theory]
    [InlineData(-0.01)]
    public void Negative_platform_percentage_throws(double pct)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PaystackFeeCalculator.CalculateZeroDeductionCharge(100, (decimal)pct, 1.95m));

    [Fact]
    public void Negative_safety_buffer_throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => PaystackFeeCalculator.CalculateZeroDeductionCharge(100, 1m, 1.95m, gatewayFeeSafetyBufferSubunit: -1));

    [Fact]
    public void Negative_flat_fee_throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => PaystackFeeCalculator.CalculateZeroDeductionCharge(100, 1m, 1.95m, flatFeeThresholdSubunit: 10, flatFeeAmountSubunit: -5));

    [Theory]
    [InlineData(-1)]
    [InlineData(100)]
    [InlineData(150)]
    public void Gateway_percentage_outside_zero_to_hundred_throws(int pct)
        => Assert.Throws<ArgumentOutOfRangeException>(() => PaystackFeeCalculator.CalculateZeroDeductionCharge(100, 1m, pct));
}
