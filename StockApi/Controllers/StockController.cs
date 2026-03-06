using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using System.Linq;

namespace StockApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StockController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public StockController(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    [HttpGet("{codes}")]
    public async Task<IActionResult> GetStock(string codes)
    {
        try
        {
            // 官方 OpenAPI 網址
            var openApiUrl = "https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_ALL";
            
            // 1. 先抓取原始字串，避免直接解析 JSON 失敗
            var rawContent = await _httpClient.GetStringAsync(openApiUrl);

            // 2. 檢查是否抓到 HTML (通常代表被導向了錯誤頁面)
            if (rawContent.Trim().StartsWith("<"))
            {
                return StatusCode(503, new { error = "證交所目前回傳了非 JSON 內容 (可能是流量管制頁)，請稍後再試。" });
            }

            // 3. 手動解析 JSON
            var allData = JsonSerializer.Deserialize<List<OpenApiStock>>(rawContent, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

            if (allData == null) return NotFound("Data is empty");

            var targetCodes = codes.Split(',').Select(c => c.Trim()).ToList();

            // 4. 偽裝成前端認得的結構
            var formattedList = allData
                .Where(x => targetCodes.Contains(x.Code))
                .Select(s => new
                {
                    c = s.Code,
                    n = s.Name,
                    z = s.ClosingPrice,
                    h = s.HighPrice,
                    l = s.LowPrice,
                    y = s.OpeningPrice,
                    t = DateTime.Now.ToString("HH:mm:ss"),
                    // 補齊五檔佔位符
                    b = "-_-_-_-_-", g = "-_-_-_-_-", a = "-_-_-_-_-", f = "-_-_-_-_-",
                });

            return Ok(new { msgArray = formattedList });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"系統異常: {ex.Message}" });
        }
    }

    public class OpenApiStock
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string OpeningPrice { get; set; } = "";
        public string HighPrice { get; set; } = "";
        public string LowPrice { get; set; } = "";
        public string ClosingPrice { get; set; } = "";
    }
}
