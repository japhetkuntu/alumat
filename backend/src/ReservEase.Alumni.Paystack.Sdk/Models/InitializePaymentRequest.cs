namespace ReservEase.Alumni.Paystack.Sdk.Models;

public class InitializePaymentRequest
{
    public string Email { get; set; } = string.Empty;
    public long Amount { get; set; } // in kobo (pesewas for GHS)
    public string Reference { get; set; } = Guid.NewGuid().ToString("N");
    public string? CallbackUrl { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// The institution's Paystack subaccount code. When set alongside
    /// <see cref="TransactionCharge"/>, that value overrides the subaccount's
    /// own percentage_charge for this transaction — the subaccount's default
    /// split is otherwise a fallback and should be 0 once every payment path
    /// sets TransactionCharge explicitly (see the Zero-Deduction model).
    /// </summary>
    public string? Subaccount { get; set; }

    /// <summary>
    /// Flat amount (in subunit) routed to our main platform account for this
    /// transaction; the remainder of <see cref="Amount"/> goes to the
    /// subaccount. Overrides the subaccount's own percentage_charge split.
    /// </summary>
    public long? TransactionCharge { get; set; }

    /// <summary>
    /// Who Paystack's own processing fee is deducted from: "account" (our
    /// main platform account) or "subaccount" (the institution). Use
    /// "account" for the Zero-Deduction model — the institution must never
    /// have Paystack's fee taken from its share, only our own.
    /// </summary>
    public string? Bearer { get; set; }
}
