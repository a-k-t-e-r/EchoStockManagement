using Microsoft.Reporting.NETCore;
using StockManagement.Domain.DTOs;
using System.Data;

namespace StockManagement.Reports.Services;

public class RdlcReportService
{
    private static readonly object _lock = new();
    private static LocalReport? _cached;
    private static string? _cachedPath;

    // Cache the compiled report — the port leaks a dynamic assembly on every load.
    private static LocalReport GetOrLoad(string templatePath)
    {
        if (_cached is not null && _cachedPath == templatePath) return _cached;

        lock (_lock)
        {
            if (_cached is not null && _cachedPath == templatePath) return _cached;

            using var stream = File.OpenRead(templatePath);
            var report = new LocalReport();
            report.LoadReportDefinition(stream);

            _cached = report;
            _cachedPath = templatePath;

            return report;
        }
    }

    public byte[] RenderStockLedgerPdf(
        IEnumerable<StockLedgerRowDto> ledgerRows,
        string storeName, DateTime from, DateTime to)
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "StockLedger.rdlc");
        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"RDLC template not found at {templatePath}");

        var dt = new DataTable("StockLedger");
        dt.Columns.Add("TransactionDate", typeof(DateTime));
        dt.Columns.Add("ReferenceNo", typeof(string));
        dt.Columns.Add("TransactionType", typeof(string));
        dt.Columns.Add("ItemCode", typeof(string));
        dt.Columns.Add("ItemName", typeof(string));
        dt.Columns.Add("InQty", typeof(decimal));
        dt.Columns.Add("OutQty", typeof(decimal));
        dt.Columns.Add("Balance", typeof(decimal));
        dt.Columns.Add("StoreName", typeof(string));

        foreach (var r in ledgerRows)
            dt.Rows.Add(r.TransactionDate, r.ReferenceNo, r.TransactionType,
                        r.ItemCode, r.ItemName, r.InQty, r.OutQty, r.Balance, r.StoreName);

        var report = GetOrLoad(templatePath);

        report.DataSources.Clear();
        report.DataSources.Add(new ReportDataSource("StockLedgerDataSet", dt));
        report.SetParameters(
        [
            new ReportParameter("StoreName", storeName ?? ""),
            new ReportParameter("FromDate",  from.ToString("yyyy-MM-dd")),
            new ReportParameter("ToDate",    to.ToString("yyyy-MM-dd"))
        ]);

        return report.Render("PDF");
    }
}