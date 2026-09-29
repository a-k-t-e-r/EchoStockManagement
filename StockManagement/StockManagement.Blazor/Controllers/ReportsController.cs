using Microsoft.AspNetCore.Mvc;
using StockManagement.Domain.Interfaces;

namespace StockManagement.Blazor.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _report;

    public ReportsController(IReportService report) => _report = report;

    [HttpGet("stock-ledger")]
    public async Task<IActionResult> StockLedger(int storeId, DateTime from, DateTime to)
    {
        if (storeId <= 0) return BadRequest("storeId is required.");
        if (to < from) return BadRequest("'to' must be on or after 'from'.");

        var pdf = await _report.RenderStockLedgerPdfAsync(storeId, from, to);
        var name = $"StockLedger_{storeId}_{from:yyyyMMdd}_{to:yyyyMMdd}.pdf";
        
        return File(pdf, "application/pdf", name);
    }
}