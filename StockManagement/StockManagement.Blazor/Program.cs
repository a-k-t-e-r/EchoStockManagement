using StockManagement.Blazor.Components;
using StockManagement.Domain.Interfaces;
using StockManagement.Infrastructure.Data;
using StockManagement.Infrastructure.Services;
using StockManagement.Reports.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddControllers();

var cs = builder.Configuration.GetConnectionString("StockDb")
         ?? throw new InvalidOperationException("ConnectionStrings:StockDb is missing.");

builder.Services.AddSingleton<IDbConnectionFactory>(new SqlConnectionFactory(cs));
builder.Services.AddScoped<IStockService, StockService>();
builder.Services.AddScoped<IItemService, ItemService>();

builder.Services.AddScoped<RdlcReportService>();
builder.Services.AddScoped<IReportService>(sp => new ReportServiceAdapter(sp.GetRequiredService<RdlcReportService>(),
                                                                          sp.GetRequiredService<IStockService>())
                                          );

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.MapControllers();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

await DatabaseInitializer.InitializeAsync(cs);

app.Run();

internal class ReportServiceAdapter(RdlcReportService rdlc, IStockService stock) : IReportService
{
    private readonly RdlcReportService _rdlc = rdlc;
    private readonly IStockService _stock = stock;

    public async Task<byte[]> RenderStockLedgerPdfAsync(int storeId, DateTime from, DateTime to)
    {
        var rows = await _stock.GetLedgerAsync(storeId, from, to);
        var store = rows.FirstOrDefault()?.StoreName ?? ("Store " + storeId);

        return _rdlc.RenderStockLedgerPdf(rows, store, from, to);
    }
}