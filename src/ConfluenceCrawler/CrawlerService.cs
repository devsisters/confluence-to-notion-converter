using Microsoft.Extensions.Logging;

namespace ConfluenceCrawler;

public sealed class CrawlerService
{
    private readonly ILogger<CrawlerService> _logger;
    private readonly ConfluenceService _service;
	private readonly SettingsManager _settingsManager;

    public CrawlerService(ILogger<CrawlerService> logger, ConfluenceService service, SettingsManager settingsManager)
    {
        _logger = logger;
        _service = service;
		_settingsManager = settingsManager;
    }

    public async Task DoCrawling(CancellationToken cancellationToken = default)
    {
		var settings = _settingsManager.LoadSettings() ??
			throw new Exception("Cannot load settings.");

		await foreach (var eachSpace in _service.GetSpacesAsync(cancellationToken))
		{
			_logger.LogInformation($"Space Name: {eachSpace.Value<string>("name")}, Space Key: {eachSpace.Value<string>("key")}");

			var spaceHomepage = await _service.GetSpaceHomepageAsync(eachSpace, cancellationToken);
			_logger.LogInformation($"Space Homepage Title: {spaceHomepage?.Value<string>("title")}");

			var spaceHomepageChildren = await _service.GetChildrenInfoAsync(spaceHomepage, cancellationToken);

			/*
			// To Do
			await foreach (var eachAttachment in _service.GetAttachmentsAsync(spaceHomepageChildren, cancellationToken))
			{
				continue;
			}
			*/

			var spaceHomepageContentId = spaceHomepage?.Value<string>("id");
			if (string.IsNullOrWhiteSpace(spaceHomepageContentId))
            {
				_logger.LogWarning("Cannot obtain content ID.");
				continue;
            }

			var spaceHomepageContent = await _service.ConvertContentBodyAsync(spaceHomepageContentId);
			_logger.LogTrace(spaceHomepageContent);

			await foreach (var eachChildPage in _service.GetPagesAsync(spaceHomepageChildren, cancellationToken))
			{
				_logger.LogInformation($"Child Page Title: {eachChildPage.Value<string>("title")}");

				await foreach (var eachSubPage in _service.TraversePageAsync(eachChildPage, 1, cancellationToken))
				{
					_logger.LogInformation(new string('>', eachSubPage.Depth) + eachSubPage.PageObject.Value<string>("title"));

					var subPageChildrenInfo = await _service.GetChildrenInfoAsync(eachSubPage.PageObject, cancellationToken);

					/*
					// To Do
					await foreach (var eachAttachment in _service.GetAttachmentsAsync(eachGrandChildrenInfo, cancellationToken))
					{
						continue;
					}
					*/

					var subPageContentId = subPageChildrenInfo?.Value<string>("id");
					if (string.IsNullOrWhiteSpace(subPageContentId))
					{
						_logger.LogWarning("Cannot obtain content ID of sub page.");
						continue;
					}

					var subPageContent = await _service.ConvertContentBodyAsync(subPageContentId);
					_logger.LogTrace(subPageContent);
				}
				continue;
			}
		}
	}
}
