namespace ReservEase.Alumni.PostgresDb.Sdk.Entities;

/// <summary>
/// The version of the Institution Agreement currently in force. Change this when the published wording changes:
/// every institution's Super Admin is then asked to accept the new version the next time they sign in.
/// </summary>
public static class InstitutionAgreement
{
    public const string CurrentVersion = "2026-09-25";
}
