using ReservEase.Alumni.Paystack.Sdk.Services;
using Xunit;

namespace ReservEase.Alumni.Institution.Api.Tests;

public class PaystackFeeCalculatorTests
{
    [Fact]
    public void CalculateZeroDeductionCharge_ReconcilesExactly_ForTypicalAmount()
    {
        // GHS 100 school amount, 1.5% platform fee, 1.95% gateway fee, no fixed fee/cap.
        var result = PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 1.5m, 1.95m);

        Assert.Equal(10000, result.SchoolAmountSubunit);
        Assert.Equal(150, result.PlatformFeeSubunit); // 1.5% of 10000
        // Invariant: school + platform + gateway == charge, always exact — no leaks.
        Assert.Equal(result.ChargeAmountSubunit, result.SchoolAmountSubunit + result.PlatformFeeSubunit + result.GatewayFeeSubunit);
        Assert.Equal(result.PlatformFeeSubunit + result.GatewayFeeSubunit, result.TransactionChargeSubunit);

        // After Paystack deducts its real percentage fee from the charge, at least the subtotal must remain.
        var subtotal = result.SchoolAmountSubunit + result.PlatformFeeSubunit;
        var actualGatewayFeeAtCharge = (long)Math.Round(result.ChargeAmountSubunit * 0.0195m, MidpointRounding.AwayFromZero);
        Assert.True(result.ChargeAmountSubunit - actualGatewayFeeAtCharge >= subtotal);
    }

    [Fact]
    public void CalculateZeroDeductionCharge_ReconcilesExactly_ForSmallAmount()
    {
        // GHS 1 school amount — tests rounding edge cases at the pesewa level.
        var result = PaystackFeeCalculator.CalculateZeroDeductionCharge(100, 5m, 1.95m);

        Assert.Equal(result.ChargeAmountSubunit, result.SchoolAmountSubunit + result.PlatformFeeSubunit + result.GatewayFeeSubunit);
    }

    [Fact]
    public void CalculateZeroDeductionCharge_ReconcilesExactly_ForLargeAmount()
    {
        // GHS 1,000,000 — large transaction, exercises the fee cap branch.
        var result = PaystackFeeCalculator.CalculateZeroDeductionCharge(
            100_000_000, 2m, 1.95m, gatewayFeeCapSubunit: 300_00);

        Assert.Equal(result.ChargeAmountSubunit, result.SchoolAmountSubunit + result.PlatformFeeSubunit + result.GatewayFeeSubunit);
        Assert.Equal(300_00, result.GatewayFeeSubunit); // capped
    }

    [Fact]
    public void CalculateZeroDeductionCharge_WithFixedFee_ReconcilesExactly()
    {
        var result = PaystackFeeCalculator.CalculateZeroDeductionCharge(
            5000, 3m, 1.95m, gatewayFixedFeeSubunit: 10);

        Assert.Equal(result.ChargeAmountSubunit, result.SchoolAmountSubunit + result.PlatformFeeSubunit + result.GatewayFeeSubunit);
    }

    [Fact]
    public void CalculateZeroDeductionCharge_ZeroPlatformFee_StillGrossesUpGatewayFee()
    {
        var result = PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 0m, 1.95m);

        Assert.Equal(0, result.PlatformFeeSubunit);
        Assert.True(result.GatewayFeeSubunit > 0);
        Assert.Equal(result.ChargeAmountSubunit, result.SchoolAmountSubunit + result.GatewayFeeSubunit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CalculateZeroDeductionCharge_ThrowsForNonPositiveAmount(long amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaystackFeeCalculator.CalculateZeroDeductionCharge(amount, 1m, 1.95m));
    }

    [Fact]
    public void CalculateZeroDeductionCharge_ThrowsForInvalidGatewayRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 1m, 100m));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(999)]
    [InlineData(12345)]
    [InlineData(999999)]
    [InlineData(1)]
    public void CalculateZeroDeductionCharge_NeverLeaksAPesewa_AcrossManyAmounts(long schoolAmount)
    {
        var result = PaystackFeeCalculator.CalculateZeroDeductionCharge(schoolAmount, 2.5m, 1.95m);
        Assert.Equal(result.ChargeAmountSubunit, result.SchoolAmountSubunit + result.PlatformFeeSubunit + result.GatewayFeeSubunit);
        Assert.True(result.ChargeAmountSubunit >= schoolAmount);
    }

    [Fact]
    public void CalculateZeroDeductionCharge_SafetyBuffer_PadsGatewayFeeAndCharge_ChargedToPayer()
    {
        var withoutBuffer = PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 1m, 1.95m, gatewayFeeSafetyBufferSubunit: 0);
        var withBuffer = PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 1m, 1.95m, gatewayFeeSafetyBufferSubunit: 2);

        // School and platform fee targets are identical either way.
        Assert.Equal(withoutBuffer.SchoolAmountSubunit, withBuffer.SchoolAmountSubunit);
        Assert.Equal(withoutBuffer.PlatformFeeSubunit, withBuffer.PlatformFeeSubunit);

        // The buffer inflates only the gateway-fee portion and the payer's charge.
        Assert.Equal(withoutBuffer.GatewayFeeSubunit + 2, withBuffer.GatewayFeeSubunit);
        Assert.Equal(withoutBuffer.ChargeAmountSubunit + 2, withBuffer.ChargeAmountSubunit);

        // Invariant still holds exactly with the buffer applied.
        Assert.Equal(withBuffer.ChargeAmountSubunit, withBuffer.SchoolAmountSubunit + withBuffer.PlatformFeeSubunit + withBuffer.GatewayFeeSubunit);
    }

    [Fact]
    public void CalculateZeroDeductionCharge_SafetyBuffer_GuaranteesPlatformNet_EvenIfRealFeeExceedsEstimate()
    {
        var result = PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 1m, 1.95m, gatewayFeeSafetyBufferSubunit: 2);

        // Simulate Paystack's real fee coming in slightly higher than our
        // unbuffered estimate would have predicted (e.g. off-by-one rounding
        // in their implementation vs. ours).
        var unbufferedEstimate = PaystackFeeCalculator.CalculateZeroDeductionCharge(10000, 1m, 1.95m, gatewayFeeSafetyBufferSubunit: 0);
        var realFeeSlightlyHigher = unbufferedEstimate.GatewayFeeSubunit + 1;

        var actualPlatformNet = result.TransactionChargeSubunit - realFeeSlightlyHigher;

        Assert.True(actualPlatformNet >= result.PlatformFeeSubunit,
            $"Platform net {actualPlatformNet} fell below its target fee {result.PlatformFeeSubunit}");
    }
}
