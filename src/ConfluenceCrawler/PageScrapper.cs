using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace ConfluenceCrawler;

public sealed class PageScrapper
{
    private readonly ILogger _logger;
	private readonly ConfluenceService _service;
	private readonly FileSystemHelper _fileSystemHelper;

	public PageScrapper(ILogger<PageScrapper> logger, ConfluenceService service, FileSystemHelper fileSystemHelper)
    {
        _logger = logger;
        _service = service;
        _fileSystemHelper = fileSystemHelper;
    }

    public IEnumerable<ScrapTarget> ScrapPages(string spaceKey, ScrapTarget target)
    {
		if (target == null)
			return Enumerable.Empty<ScrapTarget>();

		var eachChildPageId = target.PageObject.Value<string>("id");

		if (string.IsNullOrWhiteSpace(eachChildPageId))
		{
			_logger.LogWarning("Cannot obtain content ID of child page.");
			return Enumerable.Empty<ScrapTarget>();
		}

		_logger.LogInformation($"{new string('>', target.Depth + 1)} [{eachChildPageId}] {target.PageObject.Value<string>("title")}");

		var content = _service.ConvertContentBody(eachChildPageId);
		target.PageId = eachChildPageId;
		target.PageContent = content;

		_logger.LogTrace(content);

		CompilePage(target);

		if (target.CompiledContent != null)
			_fileSystemHelper.SaveHtmlContent(spaceKey, eachChildPageId, target.CompiledContent);
		else
			_logger.LogError("Cannot compile the content.");

		var eachChildPageChildren = _service.GetChildrenInfo(target.PageObject);
		var list = new List<ScrapTarget>();

		foreach (var eachChildPageChild in _service.GetPages(eachChildPageChildren))
			list.Add(new ScrapTarget(target.SpaceKey, target.Depth + 1, eachChildPageChild));

		return list;
	}

	private void CompilePage(ScrapTarget target)
    {
		var htmlDoc = new HtmlDocument();
		htmlDoc.LoadHtml(target.PageContent);

		var headStyleNodes = htmlDoc.DocumentNode.SelectNodes("/html/head/style") ?? Enumerable.Empty<HtmlNode>();
		foreach (var eachStyleNode in headStyleNodes)
			eachStyleNode.Remove();

		//var baseElement = htmlDoc.DocumentNode.SelectSingleNode("/html/head/base");
		//var baseUri = baseElement?.GetAttributeValue("href", string.Empty);
		var imageNodes = htmlDoc.DocumentNode.SelectNodes("//img") ?? Enumerable.Empty<HtmlNode>();
		imageNodes = imageNodes.Where(img => !string.IsNullOrWhiteSpace(img.GetAttributeValue("src", null)));

		foreach (var eachImageTag in imageNodes)
        {
			var imageSrc = eachImageTag.GetAttributeValue("src", string.Empty);
			// To Do: imageSrc HTML escape를 unescape해야 함. (&quot; -> ")
			if (imageSrc.Contains(".key", StringComparison.OrdinalIgnoreCase))
				Debugger.Break();
			_logger.LogInformation($"Found Image: {imageSrc}");

			var response = _service.SendGetRequest(imageSrc);
			_fileSystemHelper.SaveResource(target.SpaceKey, response);
        }

		var buffer = new StringBuilder();
		using (var writer = new StringWriter(buffer))
		{
			htmlDoc.Save(writer);
		}

		target.CompiledContent = buffer.ToString();
	}
}
