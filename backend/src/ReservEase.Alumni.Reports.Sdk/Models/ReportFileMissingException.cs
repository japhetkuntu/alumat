namespace ReservEase.Alumni.Reports.Sdk.Models;

/// <summary>A finished report whose file is not in storage at all (not just briefly unreachable): it is gone, or was written somewhere this process cannot find. The person should request the report again.</summary>
public class ReportFileMissingException(string message, Exception inner) : Exception(message, inner);
