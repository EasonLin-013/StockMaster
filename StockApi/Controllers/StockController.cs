using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace StockAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        // 使用 static 讓多個 Request 共用 Proxy 池，並避免頻繁刷新
        private static List<string> _proxyPool = new();
        private readonly Random _random = new();

        public StockController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            // 1. 如果 Proxy 池太少，則嘗試更新
            if (_proxyPool.Count < 5)
            {
                await RefreshProxyPool();
            }

            // 2. 執行抓取邏輯（最多重試 8 次）
            string jsonResult = await FetchWithRetry(id, 0);

            if (string.IsNullOrEmpty(jsonResult))
            {
                return StatusCode(500, new { message = "Proxy 連線全數失敗，請稍後再試" });
            }

            return Content(jsonResult, "application/json");
        }

        private async Task RefreshProxyPool()
        {
            try
            {
                Console.WriteLine("--- 正在更新 Proxy 清單 ---");
                var client = _httpClientFactory.CreateClient();
                
                // 篩選匿名度高 (elite) 且支援 SSL 的 Proxy，提升成功率
                string apiUrl = "https://api.proxyscrape.com/v2/?request=displayproxies&protocol=http&timeout=5000&country=all&ssl=yes&anonymity=elite";
                var response = await client.GetStringAsync(apiUrl);
                
                var list = response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(p => p.Trim())
                                   .Where(p => !string.IsNullOrEmpty(p))
                                   .ToList();

                _proxyPool = list;
                Console.WriteLine($"更新成功！目前取得 {_proxyPool.Count} 個 Proxy 可供輪替。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新清單發生錯誤: {ex.Message}");
            }
        }

        private async Task<string> FetchWithRetry(string stockId, int retryCount)
        {
            // 提高重試次數，因為免費 Proxy 的存活率極低
            if (retryCount >= 8 || !_proxyPool.Any())
            {
                Console.WriteLine($"[Stock:{stockId}] 已達重試上限或 Proxy 池已空。");
                return null;
            }

            string selectedProxy = _proxyPool[_random.Next(_proxyPool.Count)];
            
            var handler = new HttpClientHandler
            {
                Proxy = new WebProxy(selectedProxy),
                UseProxy = true,
                // 忽略免費 Proxy 常見的憑證問題
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            // 設定較短的 Timeout，失敗了就趕快換下一個，不要讓 User 等太久
            using var proxyClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4) };

            try
            {
                string url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch=tse_{stockId}.tw";
                
                // 模擬常見瀏覽器 Header，避免被證交所直接阻擋
                proxyClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await proxyClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    // 檢查內容是否真的包含證交所的數據欄位，避免抓到 Proxy 的錯誤頁面
                    if (content.Contains("msgArray"))
                    {
                        Console.WriteLine($"[Stock:{stockId}] 使用 Proxy {selectedProxy} 連線成功！");
                        return content;
                    }
                }

                // 若狀態碼不正確或內容非預期，視為失效
                throw new Exception("無效的資料格式或連線被阻擋");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Stock:{stockId}] Proxy {selectedProxy} 失效 (重試 {retryCount + 1}): {ex.Message}");
                _proxyPool.Remove(selectedProxy); // 從池中移除壞掉的 IP
                return await FetchWithRetry(stockId, retryCount + 1);
            }
        }
    }
}
