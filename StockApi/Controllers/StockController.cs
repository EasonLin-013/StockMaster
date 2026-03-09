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
        // 靜態緩存：用來存放書籤傳過來的資料
        private static string _manualBuffer = ""; 

        public StockController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            // 第一層：檢查是否有書籤 (Bookmarklet) 剛傳過來的資料
            if (!string.IsNullOrEmpty(_manualBuffer))
            {
                var data = _manualBuffer;
                _manualBuffer = ""; // 取用後清除
                Console.WriteLine($"[Stock:{id}] 使用書籤同步資料。");
                return Content(data, "application/json");
            }

            // 第二層：Google Apps Script 代理抓取 (含自動重試)
            int maxRetry = 2;
            string gasUrl = $"https://script.google.com/macros/s/AKfycbyI-FVA1NPcxwrREPtd_FeuHEDAwZ0--YsHj1yZ0iPa5wLwEYjxohNVfuYUs_jYQ0Rk4g/exec?id={id}";

            for (int i = 0; i < maxRetry; i++)
            {
                try
                {
                    // 關鍵：必須允許自動重定向 (AllowAutoRedirect)
                    var handler = new HttpClientHandler() { AllowAutoRedirect = true };
                    using var client = new HttpClient(handler);
                    client.Timeout = TimeSpan.FromSeconds(20); // 給足 20 秒處理 Google 跳轉
                    
                    // 偽裝成瀏覽器，降低被證交所 WAF 攔截機率
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                    var response = await client.GetAsync(gasUrl);
                    var content = await response.Content.ReadAsStringAsync();

                    // 檢查內容：必須包含 msgArray 才是真正的股票資料
                    if (content.Contains("msgArray") && !content.Contains("<html>"))
                    {
                        Console.WriteLine($"[Stock:{id}] 第 {i + 1} 次抓取成功！");
                        return Content(content, "application/json");
                    }

                    // 如果內容包含 <html> 或 頁面無法執行，代表被證交所攔截了
                    Console.WriteLine($"[Stock:{id}] 第 {i + 1} 次抓取遭攔截 (HTML)，準備重試...");
                    
                    if (i < maxRetry - 1) await Task.Delay(2000); // 重試前等待 2 秒，換個 Google IP
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Stock:{id}] 第 {i + 1} 次異常: {ex.Message}");
                }
            }

            // 第三層：Fallback - 回傳 502 讓前端顯示「請點擊書籤同步」
            return StatusCode(502, new { message = "自動抓取失敗，請執行書籤同步。" });
        }

        [HttpPost("manual-update")]
        public IActionResult PostManualData([FromBody] JsonElement data)
        {
            _manualBuffer = data.GetRawText();
            Console.WriteLine("--- ✅ 已接收書籤資料 ---");
            return Ok(new { status = "success" });
        }
    }
}
