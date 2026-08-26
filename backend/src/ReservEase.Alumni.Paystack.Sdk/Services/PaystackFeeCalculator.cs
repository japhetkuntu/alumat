namespace ReservEase.Alumni.Paystack.Sdk.Services;

/// <summary>
/// Result of a Zero-Deduction gross-up: what the payer is actually charged so
/// that the institution nets its full intended amount and the platform still
/// collects its fee, with Paystack's own processing fee absorbed on top —
/// all without ever deducting from the institution's share.
/// </summary>
public sealed record ZeroDeductionCharge(
    long SchoolAmountSubunit,
    long PlatformFeeSubunit,
    long GatewayFeeSubunit,
    long ChargeAmountSubunit)
{
    /// <summary>The flat amount routed to the platform's main account (platform fee + gateway fee estimate) — pass as Paystack's transaction_charge.</summary>
    public long TransactionChargeSubunit => PlatformFeeSubunit + GatewayFeeSubunit;
}

/// <summary>
/// Computes the "Zero-Deduction" gross-up: given what the institution must
/// net (SchoolAmount) and our platform fee %, works out the total the payer
/// must be charged so that after Paystack's own fee is taken — from OUR
/// share only, never the institution's — everyone ends up whole. All math is
/// in integer subunits (pesewas for GHS) to avoid float rounding drift.
/// </summary>
public static class PaystackFeeCalculator
{
    public static ZeroDeductionCharge CalculateZeroDeductionCharge(
        long schoolAmountSubunit,
        decimal platformFeePercentage,
        decimal gatewayFeePercentage,
        long gatewayFixedFeeSubunit = 0,
        long? gatewayFeeCapSubunit = null,
        long gatewayFeeSafetyBufferSubunit = 0)
    {
        if (schoolAmountSubunit <= 0)
            throw new ArgumentOutOfRangeException(nameof(schoolAmountSubunit), "Amount must be greater than zero.");
        if (platformFeePercentage < 0)
            throw new ArgumentOutOfRangeException(nameof(platformFeePercentage), "Platform fee percentage cannot be negative.");
        if (gatewayFeeSafetyBufferSubunit < 0)
            throw new ArgumentOutOfRangeException(nameof(gatewayFeeSafetyBufferSubunit), "Safety buffer cannot be negative.");
        var gatewayRate = gatewayFeePercentage / 100m;
        if (gatewayRate < 0 || gatewayRate >= 1m)
            throw new ArgumentOutOfRangeException(nameof(gatewayFeePercentage), "Gateway fee percentage must be between 0 and 100 (exclusive).");

        var platformFee = (long)Math.Round(schoolAmountSubunit * platformFeePercentage / 100m, MidpointRounding.AwayFromZero);
        var subtotal = schoolAmountSubunit + platformFee;

        // Gross-up so that chargeAmount - (chargeAmount * rate + fixed) == subtotal.
        // Ceiling ensures the institution+platform subtotal always survives intact
        // even after integer rounding — any residual pesewa lands in our own
        // gatewayFeeSubunit share, never short-changing the school.
        var grossedUp = (long)Math.Ceiling((subtotal + gatewayFixedFeeSubunit) / (1 - gatewayRate));
        var feeAtGrossedUp = (long)Math.Round(grossedUp * gatewayRate + gatewayFixedFeeSubunit, MidpointRounding.AwayFromZero);

        long chargeAmount;
        long gatewayFeePortion;
        if (gatewayFeeCapSubunit.HasValue && feeAtGrossedUp > gatewayFeeCapSubunit.Value)
        {
            // Fee is flat once capped — no further gross-up needed.
            gatewayFeePortion = gatewayFeeCapSubunit.Value;
            chargeAmount = subtotal + gatewayFeePortion;
        }
        else
        {
            chargeAmount = grossedUp;
            gatewayFeePortion = chargeAmount - subtotal;
        }

        // Safety buffer: our gross-up assumes we know Paystack's exact fee
        // formula, but the real figure at settlement could round a pesewa or
        // two differently than our estimate. Since bearer="account" makes
        // Paystack deduct its real fee from OUR share (transaction_charge),
        // any shortfall between our estimate and reality would otherwise come
        // straight out of the platform's cut. Padding gatewayFeePortion with
        // a buffer — charged to the payer, never to the school — makes the
        // platform's exact fee a hard guarantee instead of an approximation.
        if (gatewayFeeSafetyBufferSubunit > 0)
        {
            gatewayFeePortion += gatewayFeeSafetyBufferSubunit;
            chargeAmount += gatewayFeeSafetyBufferSubunit;
        }

        return new ZeroDeductionCharge(schoolAmountSubunit, platformFee, gatewayFeePortion, chargeAmount);
    }
}
