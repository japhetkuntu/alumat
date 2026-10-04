using System.Text;
using ClosedXML.Excel;
using ReservEase.Alumni.Operations.Worker.Workflows.Reports;
using ReservEase.Alumni.Reports.Sdk.Models;

namespace ReservEase.Alumni.Operations.Worker.Tests;

public class ReportWritersTests
{
    private static async IAsyncEnumerable<object?[]> Rows(params object?[][] rows)
    {
        foreach (var row in rows) { await Task.Yield(); yield return row; }
    }

    private static readonly ReportColumn[] Columns =
    [
        new("Name"), new("Amount", ReportCellKind.Money), new("Paid on", ReportCellKind.Date), new("Paid up"),
    ];

    private static async Task<(byte[] Bytes, int Count)> Write(string format, params object?[][] rows)
    {
        using var output = new MemoryStream();
        var count = await ReportWriters.WriteAsync(format, new ReportData(Columns, Rows(rows)), output, _ => { }, CancellationToken.None);
        return (output.ToArray(), count);
    }

    [Fact]
    public async Task Csv_starts_with_a_byte_order_mark_so_excel_reads_accented_names_correctly()
    {
        var (bytes, _) = await Write(ReportFormats.Csv, ["Adwoa Ɔsɛe", 10m, null, true]);

        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes.Take(3));
        Assert.Contains("Adwoa Ɔsɛe", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Csv_writes_a_header_then_one_line_per_row_with_plain_money_and_dates()
    {
        var (bytes, count) = await Write(ReportFormats.Csv,
            ["Ama", 1234.5m, new DateTime(2026, 3, 9), true], ["Kofi", null, null, false]);

        var lines = Encoding.UTF8.GetString(bytes).TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal(2, count);
        Assert.Equal(["Name,Amount,Paid on,Paid up", "Ama,1234.50,2026-03-09,True", "Kofi,,,False"], lines);
    }

    [Fact]
    public async Task Csv_quotes_text_containing_commas_quotes_or_line_breaks()
    {
        var (bytes, _) = await Write(ReportFormats.Csv, ["Mensah, \"Big\" Kofi\nJr", 1m, null, null]);

        Assert.Contains("\"Mensah, \"\"Big\"\" Kofi\nJr\"", Encoding.UTF8.GetString(bytes));
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")")]
    [InlineData("+1+1")]
    [InlineData("-2+3")]
    [InlineData("@SUM(A1)")]
    public async Task Csv_defuses_text_a_spreadsheet_would_run_as_a_formula(string typedByAMember)
    {
        var (bytes, _) = await Write(ReportFormats.Csv, [typedByAMember, 1m, null, null]);

        var line = Encoding.UTF8.GetString(bytes).Split('\n')[1];
        Assert.StartsWith("'", line.TrimStart('"'));
    }

    [Fact]
    public async Task Csv_leaves_negative_numbers_as_numbers()
    {
        var (bytes, _) = await Write(ReportFormats.Csv, ["Refund", -25m, null, null]);

        Assert.Contains("Refund,-25.00,", Encoding.UTF8.GetString(bytes));
    }

    [Fact]
    public async Task Excel_keeps_money_and_dates_as_real_values_and_formula_like_text_as_text()
    {
        var (bytes, count) = await Write(ReportFormats.Xlsx,
            ["=1+1", 1234.5m, new DateTime(2026, 3, 9), true], ["Kofi", 10m, null, false]);

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var sheet = workbook.Worksheets.Single();
        Assert.Equal(2, count);
        Assert.Equal(["Name", "Amount", "Paid on", "Paid up"], sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        Assert.True(sheet.Row(1).Style.Font.Bold);

        Assert.Equal(XLDataType.Text, sheet.Cell(2, 1).DataType);
        Assert.False(sheet.Cell(2, 1).HasFormula);
        Assert.Equal("=1+1", sheet.Cell(2, 1).GetString());

        Assert.Equal(1234.5, sheet.Cell(2, 2).GetDouble());
        Assert.Equal(new DateTime(2026, 3, 9), sheet.Cell(2, 3).GetDateTime());
        Assert.Equal("Yes", sheet.Cell(2, 4).GetString());
        Assert.True(sheet.Cell(3, 3).IsEmpty());
        // A treasurer can total the column: the two amounts are numbers, not text that looks like numbers.
        Assert.Equal(1244.5, sheet.Range("B2:B3").CellsUsed().Sum(c => c.GetDouble()));
    }

    [Fact]
    public async Task An_empty_report_is_still_a_valid_file_with_its_header()
    {
        var (csv, csvCount) = await Write(ReportFormats.Csv);
        var (xlsx, xlsxCount) = await Write(ReportFormats.Xlsx);

        Assert.Equal((0, 0), (csvCount, xlsxCount));
        Assert.StartsWith("Name,Amount", Encoding.UTF8.GetString(csv).TrimStart('﻿'));
        using var workbook = new XLWorkbook(new MemoryStream(xlsx));
        Assert.Equal("Name", workbook.Worksheets.Single().Cell(1, 1).GetString());
    }

    [Fact]
    public async Task Progress_is_reported_while_a_long_report_is_written()
    {
        var reported = new List<int>();
        var rows = Enumerable.Range(0, 600).Select(i => new object?[] { $"m{i}", 1m, null, null }).ToArray();
        using var output = new MemoryStream();

        await ReportWriters.WriteAsync(ReportFormats.Csv, new ReportData(Columns, Rows(rows)), output, reported.Add, CancellationToken.None);

        Assert.Equal([250, 500], reported);
    }
}
