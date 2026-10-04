using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using ReservEase.Alumni.Reports.Sdk.Models;

namespace ReservEase.Alumni.Operations.Worker.Workflows.Reports;

/// <summary>Turns a <see cref="ReportData"/> into a file on <paramref name="output"/>. Returns the number of rows written.</summary>
public static class ReportWriters
{
    /// <param name="onProgress">Called every few hundred rows with the running count — the activity's heartbeat.</param>
    public static Task<int> WriteAsync(string format, ReportData data, Stream output, Action<int> onProgress, CancellationToken ct) =>
        format == ReportFormats.Csv ? WriteCsvAsync(data, output, onProgress, ct) : WriteXlsxAsync(data, output, onProgress, ct);

    private const int ProgressEvery = 250;

    private static async Task<int> WriteCsvAsync(ReportData data, Stream output, Action<int> onProgress, CancellationToken ct)
    {
        // With a byte-order mark: without it Excel reads a UTF-8 CSV as Windows-1252 and garbles any accented name.
        await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), leaveOpen: true);
        await writer.WriteLineAsync(string.Join(",", data.Columns.Select(c => Escape(c.Header))));

        var count = 0;
        await foreach (var row in data.Rows.WithCancellation(ct))
        {
            await writer.WriteLineAsync(string.Join(",", row.Select((value, i) => Escape(CsvText(value, data.Columns[i].Kind)))));
            if (++count % ProgressEvery == 0) onProgress(count);
        }
        return count;
    }

    private static string CsvText(object? value, ReportCellKind kind) => value switch
    {
        null => string.Empty,
        DateTime d => kind == ReportCellKind.Date ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : d.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
        decimal m => m.ToString(kind == ReportCellKind.Money ? "0.00" : "0.##", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        // A member can type "=HYPERLINK(...)" as their job title; opened in a spreadsheet, a cell that
        // starts like a formula runs as one. A leading apostrophe makes the spreadsheet treat it as text.
        string s when s.Length > 0 && "=+-@\t\r".Contains(s[0]) => "'" + s,
        _ => value.ToString() ?? string.Empty,
    };

    private static string Escape(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r')
            ? '"' + value.Replace("\"", "\"\"") + '"'
            : value;

    private static async Task<int> WriteXlsxAsync(ReportData data, Stream output, Action<int> onProgress, CancellationToken ct)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Report");

        for (var c = 0; c < data.Columns.Count; c++)
            sheet.Cell(1, c + 1).SetValue(data.Columns[c].Header);
        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);

        var count = 0;
        await foreach (var row in data.Rows.WithCancellation(ct))
        {
            var r = count + 2;
            for (var c = 0; c < row.Length; c++)
            {
                var cell = sheet.Cell(r, c + 1);
                switch (row[c])
                {
                    case null: break;
                    // SetValue(string) stores text — never a formula, whatever the text starts with.
                    case string s: cell.SetValue(s); break;
                    case DateTime d: cell.SetValue(d); break;
                    case decimal m: cell.SetValue(m); break;
                    case int n: cell.SetValue(n); break;
                    case long n: cell.SetValue(n); break;
                    case double n: cell.SetValue(n); break;
                    case bool b: cell.SetValue(b ? "Yes" : "No"); break;
                    default: cell.SetValue(row[c]!.ToString()); break;
                }
            }
            if (++count % ProgressEvery == 0) onProgress(count);
        }

        for (var c = 0; c < data.Columns.Count; c++)
        {
            var column = sheet.Column(c + 1);
            var format = data.Columns[c].Kind switch
            {
                ReportCellKind.Money => "#,##0.00",
                ReportCellKind.Date => "yyyy-mm-dd",
                ReportCellKind.Timestamp => "yyyy-mm-dd hh:mm",
                _ => null,
            };
            if (format is not null) column.Style.NumberFormat.Format = format;
        }
        if (count > 0) sheet.Range(1, 1, count + 1, data.Columns.Count).SetAutoFilter();
        // Widths from the header and the first rows only — measuring every cell of a long report costs more than writing it.
        sheet.Columns().AdjustToContents(1, Math.Min(count + 1, 200), 8, 60);

        workbook.SaveAs(output);
        return count;
    }
}
