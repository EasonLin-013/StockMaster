using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

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
            // 1. 呼叫官方 OpenAPI (此 API 對雲端機房友好，不會報 502)
            // 來源：證交所大盤所有個股即時行情摘要
            var openApiUrl = "https://openapi.twse.com.tw/v1/exchangeReport/STOCK_DAY_ALL";
            
            var allData = await _httpClient.GetFromJsonAsync<List<OpenApiStock>>(openApiUrl);

            if (allData == null) return NotFound("無法取得 OpenAPI 資料");

            // 2. 處理輸入的代碼 (例如 "0050,2330")
            var targetCodes = codes.Split(',')
                                  .Select(c => c.Trim())
                                  .ToList();

            // 3. 將 OpenAPI 格式 轉換為 前端 Blazor 預期的單字母縮寫格式 (msgArray)
            var formattedList = allData
                .Where(x => targetCodes.Contains(x.Code))
                .Select(s => new
                {
                    c = s.Code,             // 股票代號
                    n = s.Name,             // 公司名稱
                    z = s.ClosingPrice,     // 當前/收盤價 (對應前端 s.CurrentPrice)
                    h = s.HighPrice,        // 最高價 (對應前端 s.High)
                    l = s.LowPrice,         // 最低價 (對應前端 s.Low)
                    y = s.OpeningPrice,     // 昨收/開盤 (對應前端 s.YesterdayClose)
                    t = DateTime.Now.ToString("HH:mm:ss"), // 模擬成交時間
                    
                    // 因為 OpenAPI 不提供五檔資料，我們補上空格式字串
                    // 這樣前端的 s.SellPrices.Split('_') 才不會報錯
                    b = "-_-_-_-_-",         // 買進價 (模擬 L1~L5)
                    g = "-_-_-_-_-",         // 買進量
                    a = "-_-_-_-_-",         // 賣出價
                    f = "-_-_-_-_-",         // 賣出量
                });

            // 4. 包裝成前端認得的對象結構
            return Ok(new { msgArray = formattedList });
        }
        catch (Exception ex)
        {
            // 如果發生錯誤，回傳 500 與錯誤訊息供偵錯
            return StatusCode(500, new { error = $"OpenAPI 擷取失敗: {ex.Message}" });
        }
    }

    // 對應官方 OpenAPI JSON 欄位的內部類別
    public class OpenApiStock
    {
        public string Code { get; set; } = "";           // 證券代號
        public string Name { get; set; } = "";           // 證券名稱
        public string OpeningPrice { get; set; } = "";    // 開盤價
        public string HighPrice { get; set; } = "";       // 最高價
        public string LowPrice { get; set; } = "";        // 最低價
        public string ClosingPrice { get; set; } = "";    // 收盤價
    }
}
