using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;

namespace StockAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        // 靜態變數暫存手動同步的資料 (書籤用)
        private static string _manualBuffer = ""; 

        public StockController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            // 優先權 1: 檢查是否有書籤手動同步的資料 (最穩定的備援)
            if (!string.IsNullOrEmpty(_manualBuffer))
            {
                var data = _manualBuffer;
                _manualBuffer = ""; 
                Console.WriteLine($"[Stock:{id}] 使用手動同步緩存。");
                return Content(data, "application/json");
            }

            // 優先權 2: 透過 Google Apps Script 自動抓取
            try 
            {
                var client = _httpClientFactory.CreateClient();
                // 增加超時時間至 10 秒，因為經過 Google 轉發會多花幾秒
                client.Timeout = TimeSpan.FromSeconds(10);
                
                // 這是你剛剛建立的 Google 機器人網址
                string gasUrl = $"https://script.google.com/macros/s/AKfycbyI-FVA1NPcxwrREPtd_FeuHEDAwZ0--YsHj1yZ0iPa5wLwEYjxohNVfuYUs_jYQ0Rk4g/exec?id={id}";

                var response = await client.GetStringAsync(gasUrl);

                if (response.Contains("msgArray"))
                {
                    Console.WriteLine($"[Stock:{id}] 透過 Google 代理自動抓取成功！");
                    return Content(response, "application/json");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Stock:{id}] Google 代理失敗: {ex.Message}");
            }

            // 全部失敗：回傳 502 告知前端提示使用者點擊書籤
            return StatusCode(502, new { message = "自動抓取失效，請執行下方手動同步。" });
        }

        [HttpPost("manual-update")]
        public IActionResult PostManualData([FromBody] JsonElement data)
        {
            _manualBuffer = data.GetRawText();
            Console.WriteLine("--- ✅ 收到來自書籤的同步資料 ---");
            return Ok(new { status = "success" });
        }
    }
}
