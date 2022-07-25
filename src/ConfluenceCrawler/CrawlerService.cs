using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ConfluenceCrawler;

public sealed class CrawlerService
{
    private readonly ILogger _logger;
    private readonly ConfluenceService _service;
	private readonly SettingsManager _settingsManager;
	private readonly FileSystemHelper _fileSystemHelper;
	private readonly PageScrapper _pageScrapper;

    public CrawlerService(
		ILogger<CrawlerService> logger,
        ConfluenceService service,
        SettingsManager settingsManager,
        FileSystemHelper fileSystemHelper,
        PageScrapper pageScrapper)
    {
        _logger = logger;
        _service = service;
        _settingsManager = settingsManager;
        _fileSystemHelper = fileSystemHelper;
        _pageScrapper = pageScrapper;
    }

    public IDictionary<string, ScrapTarget> DoCrawling()
    {
		var _ = _settingsManager.LoadSettings() ??
			throw new Exception("Cannot load settings.");

		_fileSystemHelper.EnsureDirectoryExists();

		var results = new Dictionary<string, ScrapTarget>();
		var spaces = _service.GetGlobalSpaces();

		foreach (var eachSpace in spaces)
		{
			var queue = new Queue<ScrapTarget>();
			var spaceKey = eachSpace.Value<string>("key");

			if (spaceKey == null)
				continue;

			_logger.LogInformation($"! Space Name: {eachSpace.Value<string>("name")}, Space Key: {spaceKey}");

			var spaceHomepage = _service.GetSpaceHomepage(eachSpace);
			_logger.LogInformation($"Space Homepage Title: {spaceHomepage?.Value<string>("title")}");

			if (spaceHomepage == null)
				continue;

			var targetRoot = new ScrapTarget(spaceKey, 0, spaceHomepage);
			results.Add(spaceKey, targetRoot);

			queue.Enqueue(targetRoot);

			while (queue.Count > 0)
            {
				if (!queue.TryDequeue(out ScrapTarget? eachChildPage))
					continue;

				foreach (var eachSubTarget in _pageScrapper.ScrapPages(spaceKey, eachChildPage))
					queue.Enqueue(eachSubTarget);
			}
		}

		return results;
	}
}

public record class ScrapTarget(string SpaceKey, int Depth, JObject PageObject)
{
	public string? PageId { get; set; }
	public string? PageContent { get; set; }
	public string? CompiledContent { get; set; }

	public List<ScrapTarget> Children { get; } = new List<ScrapTarget>();
}
