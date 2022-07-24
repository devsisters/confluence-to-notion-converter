using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace ConfluenceCrawler;

public sealed class CrawlerService
{
    private readonly ILogger _logger;
    private readonly ConfluenceService _service;
	private readonly SettingsManager _settingsManager;
	private readonly FileSystemHelper _fileSystemHelper;

    public CrawlerService(
		ILogger<CrawlerService> logger,
        ConfluenceService service,
        SettingsManager settingsManager,
        FileSystemHelper fileSystemHelper)
    {
        _logger = logger;
        _service = service;
        _settingsManager = settingsManager;
        _fileSystemHelper = fileSystemHelper;
    }

    public void DoCrawling()
    {
		var _ = _settingsManager.LoadSettings() ??
			throw new Exception("Cannot load settings.");

		_fileSystemHelper.EnsureDirectoryExists();

		var spaces = _service.GetGlobalSpaces();
		var queue = new Queue<JObject>();

		foreach (var eachSpace in spaces)
		{
			var spaceKey = eachSpace.Value<string>("key");

			if (spaceKey == null)
				continue;

			_logger.LogInformation($"! Space Name: {eachSpace.Value<string>("name")}, Space Key: {spaceKey}");

			var spaceHomepage = _service.GetSpaceHomepage(eachSpace);
			_logger.LogInformation($"Space Homepage Title: {spaceHomepage?.Value<string>("title")}");

			if (spaceHomepage == null)
				continue;

			queue.Enqueue(spaceHomepage);

			var spaceHomepageChildren = _service.GetChildrenInfo(spaceHomepage);

			foreach (var eachChildPage in _service.GetPages(spaceHomepageChildren))
				queue.Enqueue(eachChildPage);

			while (queue.Count > 0)
            {
				if (!queue.TryDequeue(out JObject? eachChildPage))
					continue;

				var eachChildPageId = eachChildPage.Value<string>("id");

				if (string.IsNullOrWhiteSpace(eachChildPageId))
				{
					_logger.LogWarning("Cannot obtain content ID of child page.");
					continue;
				}

				_logger.LogInformation($"Child Page Id: {eachChildPageId}");
				_logger.LogInformation($"Child Page Title: {eachChildPage.Value<string>("title")}");

				var eachChildPageContent = _service.ConvertContentBody(eachChildPageId);
				_logger.LogTrace(eachChildPageContent);

				_fileSystemHelper.SaveHtmlContent(spaceKey, eachChildPageId, eachChildPageContent);

				var eachChildPageChildren = _service.GetChildrenInfo(eachChildPage);

				foreach (var eachChildPageChild in _service.GetPages(eachChildPageChildren))
					queue.Enqueue(eachChildPageChild);
			}
		}
	}
}
