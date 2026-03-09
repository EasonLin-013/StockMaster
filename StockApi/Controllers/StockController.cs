using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace StockAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StockController : ControllerBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        // 使用 static 確保所有 Request 共用 Proxy 池
        private static List<string> _proxyPool = new();
        private readonly Random _random = new();

        public StockController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(string id)
        {
            // 1. 如果 Proxy 池太少，嘗試更新
            if (_proxyPool.Count < 5)
            {
                await RefreshProxyPool();
            }

            // 2. 執行抓取邏輯
            string jsonResult = await FetchWithRetry(id, 0);

            if (string.IsNullOrEmpty(jsonResult))
            {
                // 如果 Proxy 方案全滅，這裡可以作為最後防線回傳錯誤，或導向備援 API
                return StatusCode(500, new { message = "Proxy 連線全數失敗，伺服器 IP 可能遭封鎖，請稍後再試" });
            }

            return Content(jsonResult, "application/json");
        }

        private async Task RefreshProxyPool()
        {
            try
            {
                Console.WriteLine("--- 啟動 Proxy 清單更新程序 ---");
                var client = _httpClientFactory.CreateClient();
                
                // 來源 A: ProxyScrape (放寬條件至 anonymity=all 以確保數量)
                string sourceA = "https://api.proxyscrape.com/v2/?request=getproxies&protocol=http&timeout=10000&country=all&ssl=yes&anonymity=all";
                
                var response = await client.GetStringAsync(sourceA);
                var list = ParseProxyList(response);

                // 來源 B: 如果 A 失敗或數量太少，嘗試備援來源
                if (list.Count < 10)
                {
                    Console.WriteLine("來源 A 供應不足，嘗試來源 B (Proxy-List.download)...");
                    string sourceB = "https://www.proxy-list.download/api/v1/get?type=https";
                    var responseB = await client.GetStringAsync(sourceB);
                    list.AddRange(ParseProxyList(responseB));
                }

                _proxyPool = list.Distinct().ToList();
                Console.WriteLine($"更新完成！目前 Proxy 池共計: {_proxyPool.Count} 個可用 IP。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新清單時發生非預期錯誤: {ex.Message}");
            }
        }

        private List<string> ParseProxyList(string rawData)
        {
            return rawData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(p => p.Trim())
                          .Where(p => !string.IsNullOrEmpty(p) && p.Contains(":"))
                          .ToList();
        }

        private async Task<string> FetchWithRetry(string stockId, int retryCount)
        {
            // 最大重試次數設定為 10 次，應對免費 Proxy 的高失效率
            if (retryCount >= 10 || !_proxyPool.Any())
            {
                Console.WriteLine($"[Stock:{stockId}] 重試已達上限，無法取得資料。");
                return null;
            }

            string selectedProxy = _proxyPool[_random.Next(_proxyPool.Count)];
            
            var handler = new HttpClientHandler
            {
                Proxy = new WebProxy(selectedProxy),
                UseProxy = true,
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            // 縮短 Timeout (3秒)，失敗快一點換下一個，體驗會比較好
            using var proxyClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3) };

            try
            {
                string url = $"https://mis.twse.com.tw/stock/api/getStockInfo.jsp?ex_ch=tse_{stockId}.tw";
                proxyClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                var response = await proxyClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    // 驗證是否為證交所原始 JSON
                    if (content.Contains("msgArray"))
                    {
                        Console.WriteLine($"[Stock:{stockId}] 成功！透過 Proxy: {selectedProxy}");
                        return content;
                    }
                }
                throw new Exception("無效回應或非 JSON 內容");
            }
            catch (Exception ex)
            {
                // 失敗時從池中移除，並立即重試
                _proxyPool.Remove(selectedProxy);
                Console.WriteLine($"[Stock:{stockId}] Proxy {selectedProxy} 失敗 (剩餘預選:{_proxyPool.Count}): {ex.Message}");
                return await FetchWithRetry(stockId, retryCount + 1);
            }
        }
    }
}
