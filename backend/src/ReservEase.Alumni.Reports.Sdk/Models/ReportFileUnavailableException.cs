namespace ReservEase.Alumni.Reports.Sdk.Models;

/// <summary>A finished report exists but its file could not be fetched from storage (storage is down or misconfigured). Not the same as "expired".</summary>
public class ReportFileUnavailableException(string message, Exception inner) : Exception(message, inner);
