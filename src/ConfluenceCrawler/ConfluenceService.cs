using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Headers;

namespace ConfluenceCrawler;

public sealed class ConfluenceService
{
	public ConfluenceService(ILogger<ConfluenceService> logger, IHttpClientFactory hcFactory, SettingsManager settingsManager)
	{
		_logger = logger;
		_hcFactory = hcFactory;
		_settingsManager = settingsManager;

		_client = _hcFactory.CreateClient("confluence");
		_settings = _settingsManager.LoadSettings()
			?? throw new Exception("Cannot load the settings");
		_context = _settings.Confluence.Context.TrimEnd('/') + "/";
	}

	private readonly ILogger _logger;
	private readonly IHttpClientFactory _hcFactory;
	private readonly SettingsManager _settingsManager;

	private readonly HttpClient _client;
	private readonly CrawlerSettings _settings;
	private readonly string _context;

	public IList<JObject> GetGlobalSpaces()
	{
		var objects = new List<JObject>();

		var content = _client.GetStringAsync($"{_context}rest/api/space").Result;
		var o = JObject.Parse(content);

		foreach (var eachSpace in o.SelectTokens("$.results[?(@.type == 'global')]"))
			objects.Add((JObject)eachSpace);

		var context = o.SelectToken("$._links.context")?.Value<string>();
		var next = o.SelectToken("$._links.next")?.Value<string>();

		while (!string.IsNullOrWhiteSpace(next))
		{
			content = _client.GetStringAsync($"{context}{next}").Result;
			o = JObject.Parse(content);

			foreach (var eachSpace in o.SelectTokens("$.results[*]"))
				objects.Add((JObject)eachSpace);

			context = o.SelectToken("$._links.context")?.Value<string>();
			next = o.SelectToken("$._links.next")?.Value<string>();
		}

		return objects;
	}

	public JObject? GetSpaceHomepage(JObject? eachSpace)
	{
		var targetUrl = eachSpace?.SelectToken("$._expandable.homepage")?.Value<string>();
		if (targetUrl == null)
			return null;

		try
		{
			var content = _client.GetStringAsync($"{_context}{targetUrl}").Result;
			var o = JObject.Parse(content);
			return o;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex.ToString());
			return null;
		}
	}

	public JObject? GetChildrenInfo(JObject? eachSpaceHomepage)
	{
		var targetUrl = eachSpaceHomepage?.SelectToken("$._expandable.children")?.Value<string>();
		if (targetUrl == null)
			return null;

		try
		{
			var content = _client.GetStringAsync($"{_context}{targetUrl}").Result;
			var o = JObject.Parse(content);
			return o;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex.ToString());
			return null;
		}
	}

	public IList<JObject> GetAttachments(JObject? eachChildren)
	{
		var objects = new List<JObject>();

		var targetUrl = eachChildren?.SelectToken("$._expandable.attachment")?.Value<string>();
		if (targetUrl == null)
			return objects;

		var content = _client.GetStringAsync($"{_context}{targetUrl}").Result;
		var o = JObject.Parse(content);

		foreach (var eachSpace in o.SelectTokens("$.results[*]"))
			objects.Add((JObject)eachSpace);

		var context = o.SelectToken("$._links.context")?.Value<string>();
		var next = o.SelectToken("$._links.next")?.Value<string>();

		while (!string.IsNullOrWhiteSpace(next))
		{
			content = _client.GetStringAsync($"{context}{next}").Result;
			o = JObject.Parse(content);

			foreach (var eachSpace in o.SelectTokens("$.results[*]"))
				objects.Add((JObject)eachSpace);

			context = o.SelectToken("$._links.context")?.Value<string>();
			next = o.SelectToken("$._links.next")?.Value<string>();
		}

		return objects;
	}

	public IList<JObject> GetPages(JObject? eachSpaceChildren)
	{
		var objects = new List<JObject>();

		var targetUrl = eachSpaceChildren?.SelectToken("$._expandable.page")?.Value<string>();
		if (targetUrl == null)
			return objects;

		var content = _client.GetStringAsync($"{_context}{targetUrl}").Result;
		var o = JObject.Parse(content);

		foreach (var eachSpace in o.SelectTokens("$.results[*]"))
			objects.Add((JObject)eachSpace);

		var context = o.SelectToken("$._links.context")?.Value<string>();
		var next = o.SelectToken("$._links.next")?.Value<string>();

		while (!string.IsNullOrWhiteSpace(next))
		{
			content = _client.GetStringAsync($"{context}{next}").Result;
			o = JObject.Parse(content);

			foreach (var eachSpace in o.SelectTokens("$.results[*]"))
				objects.Add((JObject)eachSpace);

			context = o.SelectToken("$._links.context")?.Value<string>();
			next = o.SelectToken("$._links.next")?.Value<string>();
		}

		return objects;
	}

	public JObject GetContentDetail(string contentId)
	{
		var content = _client.GetStringAsync($"{_context}rest/api/content/{contentId}?expand=space,body.storage").Result;
		var o = JObject.Parse(content);
		return o;
	}

	public string ConvertContentBody(string contentId)
	{
		var o = GetContentDetail(contentId);
		var body = o.SelectToken("body.storage.value")?.Value<string>();
		var repr = o.SelectToken("body.storage.representation")?.Value<string>();
		var id = o.SelectToken("id")?.Value<string>();
		var space = o.SelectToken("space.key")?.Value<string>();

		var requestContent = new StringContent(JsonConvert.SerializeObject(new
		{
			value = body,
			representation = repr,
		}));
		requestContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

		var to = "styled_view";
		var content = _client.PostAsync($"{_context}rest/api/contentbody/convert/{to}?spaceKeyContext={space}&contentIdContext={id}", requestContent).Result;
		var resp = JObject.Parse(content.Content.ReadAsStringAsync().Result);
		var converted = resp.SelectToken("value")?.Value<string>();
		return converted ?? string.Empty;
	}

	public bool SendGetRequest(string url, out HttpResponseMessage? responseMessage, out Exception? exception)
    {
		try
		{
			var requestMessage = new HttpRequestMessage(HttpMethod.Get, url);
			responseMessage = _client.SendAsync(requestMessage).Result;
			exception = null;
			return true;
		}
		catch (Exception ex)
        {
			_logger.LogWarning(ex, $"Cannot send get request due to error: {ex.Message}");
			responseMessage = null;
			exception = ex;
			return false;
        }
    }
}

public record PageTraverseResult(int Depth, JObject PageObject);
