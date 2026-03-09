using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;

namespace StockAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        // 靜態暫存區：用來接收書籤 (Bookmarklet) 傳來的資料
        private static string _manualBuffer = ""; 

        public StockController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            // 第一層：檢查是否有書籤手動同步的資料（最優先，因為這是使用者主動觸發）
            if (!string.IsNullOrEmpty(_manualBuffer))
            {
                var data = _manualBuffer;
                _manualBuffer = ""; // 取用後清除，確保下次是新鮮資料
                Console.WriteLine($"[Stock:{id}] 成功讀取書籤同步緩存。");
                return Content(data, "application/json");
            }

            // 第二層：透過 Google Apps Script 自動抓取（含 2 次自動重試）
            int maxRetry = 2;
            string gasUrl = $"https://script.google.com/macros/s/AKfycbyI-FVA1NPcxwrREPtd_FeuHEDAwZ0--YsHj1yZ0iPa5wLwEYjxohNVfuYUs_jYQ0Rk4g/exec?id={id}";

            for (int i = 0; i < maxRetry; i++)
            {
                try
                {
                    var handler = new HttpClientHandler() { AllowAutoRedirect = true };
                    using var client = new HttpClient(handler);
                    client.Timeout = TimeSpan.FromSeconds(15);
                    
                    // 模擬真實瀏覽器標頭，降低被攔截機率
                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");

                    var response = await client.GetAsync(gasUrl);
                    var content = await response.Content.ReadAsStringAsync();

                    // 檢查是否為正確的 JSON 資料 (包含 msgArray)
                    if (content.Contains("msgArray"))
                    {
                        Console.WriteLine($"[Stock:{id}] 第 {i + 1} 次抓取成功。");
                        return Content(content, "application/json");
                    }

                    // 如果拿到的是證交所的錯誤 HTML 頁面
                    Console.WriteLine($"[Stock:{id}] 第 {i + 1} 次抓取遭防火牆攔截，準備重試...");
                    
                    if (i < maxRetry - 1) await Task.Delay(1000); // 重試前等 1 秒
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Stock:{id}] 第 {i + 1} 次連線異常: {ex.Message}");
                }
            }

            // 第三層：所有自動化手段均失敗，回傳 502 指導前端顯示書籤同步
            return StatusCode(502, new { message = "自動抓取失效，請執行書籤同步。" });
        }

        // 接收書籤傳來的 Post 請求
        [HttpPost("manual-update")]
        public IActionResult PostManualData([FromBody] JsonElement data)
        {
            _manualBuffer = data.GetRawText();
            Console.WriteLine("--- ✅ 已收到書籤傳入的資料 ---");
            return Ok(new { status = "success" });
        }
    }
}
