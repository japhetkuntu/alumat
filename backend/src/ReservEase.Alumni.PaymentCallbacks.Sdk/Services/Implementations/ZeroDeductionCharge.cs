using ReservEase.Alumni.Paystack.Sdk.Options;
using ReservEase.Alumni.Paystack.Sdk.Services;
using Institution = ReservEase.Alumni.PostgresDb.Sdk.Entities.Institution;

namespace ReservEase.Alumni.PaymentCallbacks.Sdk.Services.Implementations;

/// <summary>
/// What to charge the payer so the institution still receives the full amount: our fee is added to the charge, never
/// taken from the amount. The same calculation store checkout and contributions use.
/// </summary>
public readonly record struct ZeroDeductionCharge(
    long AmountSubunit, long? TransactionChargeSubunit, string? Bearer,
    decimal PlatformFee, decimal GatewayFee, decimal TransactionChargeAmount, decimal GrossCharge)
{
    public static ZeroDeductionCharge Build(PaystackConfig paystackConfig, decimal totalAmount, Institution? institution)
    {
        var totalSubunit = (long)Math.Round(totalAmount * 100m, MidpointRounding.AwayFromZero);

        if (institution is null || string.IsNullOrEmpty(institution.PaystackSubaccountCode))
            return new ZeroDeductionCharge(totalSubunit, null, null, 0m, 0m, 0m, totalAmount);

        var charge = PaystackFeeCalculator.CalculateZeroDeductionCharge(
            totalSubunit,
            institution.PlatformFeePercentage,
            paystackConfig.GatewayFeePercentage,
            paystackConfig.GatewayFixedFeeSubunit,
            paystackConfig.GatewayFeeCapSubunit,
            paystackConfig.GatewayFeeSafetyBufferSubunit,
            institution.PlatformFeeFlatThreshold.HasValue ? (long)Math.Round(institution.PlatformFeeFlatThreshold.Value * 100m, MidpointRounding.AwayFromZero) : null,
            institution.PlatformFeeFlatAmount.HasValue ? (long)Math.Round(institution.PlatformFeeFlatAmount.Value * 100m, MidpointRounding.AwayFromZero) : null);

        return new ZeroDeductionCharge(
            charge.ChargeAmountSubunit, charge.TransactionChargeSubunit, "account",
            charge.PlatformFeeSubunit / 100m, charge.GatewayFeeSubunit / 100m,
            charge.TransactionChargeSubunit / 100m, charge.ChargeAmountSubunit / 100m);
    }
}
