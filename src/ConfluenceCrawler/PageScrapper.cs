using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

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
			_fileSystemHelper.SaveHtmlContent(eachChildPageId, target.CompiledContent);
		else
			_logger.LogError("Cannot compile the content.");

		var eachChildPageChildren = _service.GetChildrenInfo(target.PageObject);
		var list = new List<ScrapTarget>();

		foreach (var eachChildPageChild in _service.GetPages(eachChildPageChildren))
			list.Add(new ScrapTarget(target.SpaceKey, target.Depth + 1, eachChildPageChild));

		target.Children.AddRange(list);
		return list;
	}

	private void CompilePage(ScrapTarget target)
    {
		var htmlDoc = new HtmlDocument();
		htmlDoc.LoadHtml(target.PageContent);

		var baseElement = htmlDoc.DocumentNode.SelectSingleNode("/html/head/base");
		if (baseElement != null)
			baseElement.Remove();

		var headStyleNodes = htmlDoc.DocumentNode.SelectNodes("/html/head/style") ?? Enumerable.Empty<HtmlNode>();
		foreach (var eachStyleNode in headStyleNodes)
			eachStyleNode.Remove();

		var imageNodes = htmlDoc.DocumentNode.SelectNodes("//img") ?? Enumerable.Empty<HtmlNode>();
		imageNodes = imageNodes.Where(img => !string.IsNullOrWhiteSpace(img.GetAttributeValue("src", null)));

		foreach (var eachImageTag in imageNodes)
        {
			var imageSrc = HttpUtility.HtmlDecode(eachImageTag.GetAttributeValue("src", string.Empty));
			_logger.LogInformation($"Found Image: {imageSrc}");

			var response = _service.SendGetRequest(imageSrc);
			var fileName = _fileSystemHelper.SaveImageResource(response);
			eachImageTag.SetAttributeValue("src", "images/" + fileName);
        }

		var anchorNodes = htmlDoc.DocumentNode.SelectNodes("//a") ?? Enumerable.Empty<HtmlNode>();
		anchorNodes = anchorNodes.Where(a => !string.IsNullOrWhiteSpace(a.GetAttributeValue("href", null)));

		foreach (var eachATag in anchorNodes)
        {
			var aHref = HttpUtility.HtmlDecode(eachATag.GetAttributeValue("href", string.Empty));
			var match = Regex.Match(aHref, @"/pages/(?<PageId>[^/?]+)/?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
			var pageId = match.Groups["PageId"].Value;

			if (!string.IsNullOrWhiteSpace(pageId))
				eachATag.SetAttributeValue("href", $"{pageId}.html");
		}

		var buffer = new StringBuilder();
		using (var writer = new StringWriter(buffer))
		{
			htmlDoc.Save(writer);
		}

		target.CompiledContent = buffer.ToString();
	}
}
