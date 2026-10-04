namespace ReservEase.Alumni.Operations.Worker.Workflows.Reports;

/// <summary>How a column's values are to be read, so each file format can present them properly (a money column sums in Excel; a date sorts as a date).</summary>
public enum ReportCellKind { Text, Number, Money, Date, Timestamp }

public record ReportColumn(string Header, ReportCellKind Kind = ReportCellKind.Text);

/// <summary>
/// A report as data, before it is any particular file format: named, typed columns and a stream of
/// rows. Rows are produced lazily, a page of the database at a time, so the size of a report is
/// bounded by the file it becomes and not by the worker's memory.
/// </summary>
/// <param name="Rows">Each row has one value per column, in column order. Null is an empty cell.</param>
public record ReportData(IReadOnlyList<ReportColumn> Columns, IAsyncEnumerable<object?[]> Rows);

/// <summary>Raised for a report that can't be produced as asked — an unknown type, say. Not retried: asking again gets the same answer.</summary>
public class ReportNotSupportedException(string message) : Exception(message);
