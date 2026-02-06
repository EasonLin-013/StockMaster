using Microsoft.AspNetCore.Mvc;

namespace StockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly IHttpClientFactory _clientFactory;
    public StockController(IHttpClientFactory clientFactory) => _clientFactory = clientFactory;

    [HttpGet("{code}")]
    public async Task<IActionResult> GetStock(string code)
    {
        var client = _clientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
        
        // 直連證交所 API
        string url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch=tse_{code}.tw";
        
        try {
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            return Content(content, "application/json");
        } catch (Exception ex) {
            return BadRequest(ex.Message);
        }
    }
}