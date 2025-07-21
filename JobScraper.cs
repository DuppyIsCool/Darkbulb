using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace DarkbulbBot
{
    public class ScrapedJobSummary
    {
        public string RawId { get; set; }
        public string Title { get; set; }
        public string Craft { get; set; }
        public string ProductTeam { get; set; }
        public string Office { get; set; }
        public string Url { get; set; }
    }

    public class ScrapedJob : ScrapedJobSummary
    {
        public string RealJobId { get; set; }
        public string Description { get; set; }
        public DateTime RetrievedAt { get; set; }
    }

    public class JobScraper
    {
        private static readonly HttpClient _http;

        static JobScraper()
        {
            _http = new HttpClient();
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/115.0.0.0 Safari/537.36"
            );
            _http.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
            _http.Timeout = TimeSpan.FromSeconds(30);
        }

        private const string ListingsUrl = "https://www.riotgames.com/en/work-with-us/jobs";
        private const string BaseJobUrl = "https://www.riotgames.com/en/work-with-us/job/";

        /// <summary>
        /// Phase 1: fetch only the listing page and return metadata.
        /// </summary>
        public async Task<List<ScrapedJobSummary>> ScrapeListingAsync()
        {
            var html = await _http.GetStringAsync(ListingsUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var nodes = doc.DocumentNode
                           .SelectNodes("//a[contains(@class,'job-row__inner')]")
                       ?? Enumerable.Empty<HtmlNode>();

            return nodes.Select(link =>
            {
                var href = link.GetAttributeValue("href", "").TrimEnd('/');
                var rawId = href.Split('/').Last();
                var title = HtmlEntity.DeEntitize(
                    link.SelectSingleNode(".//div[contains(@class,'job-row__col--primary')]")
                        .InnerText.Trim()
                );
                var sec = link.SelectNodes(".//div[contains(@class,'job-row__col--secondary')]")
                              .ToArray();

                return new ScrapedJobSummary
                {
                    RawId = rawId,
                    Title = title,
                    Craft = HtmlEntity.DeEntitize(sec[0].InnerText.Trim()),
                    ProductTeam = HtmlEntity.DeEntitize(sec[1].InnerText.Trim()),
                    Office = HtmlEntity.DeEntitize(sec[2].InnerText.Trim()),
                    Url = BaseJobUrl + rawId
                };
            }).ToList();
        }

        /// <summary>
        /// Phase 2: fetch & parse one job’s detail page.
        /// </summary>
        public async Task<ScrapedJob> FetchJobDetailAsync(ScrapedJobSummary s)
        {
            var resp = await _http.GetAsync(s.Url);
            resp.EnsureSuccessStatusCode();

            var pageHtml = await resp.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(pageHtml);

            var realMatch = Regex.Match(pageHtml, @"REQ-\d+");
            var realId = realMatch.Success ? realMatch.Value : s.RawId;

            var descNode = doc.DocumentNode
                              .SelectSingleNode(
                                "//div[contains(@class,'job-description') or contains(@class,'rich-text')]"
                              );
            var description = descNode != null
                ? HtmlEntity.DeEntitize(descNode.InnerText.Trim())
                : "";

            return new ScrapedJob
            {
                RawId = s.RawId,
                Title = s.Title,
                Craft = s.Craft,
                ProductTeam = s.ProductTeam,
                Office = s.Office,
                Url = s.Url,
                RealJobId = realId,
                Description = description,
                RetrievedAt = DateTime.UtcNow
            };
        }
    }
}
