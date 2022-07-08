using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace ConfluenceCrawler;

public sealed class ConfluenceService
{
	public ConfluenceService(IHttpClientFactory hcFactory, SettingsManager settingsManager)
	{
		_hcFactory = hcFactory;
		_settingsManager = settingsManager;

		_client = _hcFactory.CreateClient("confluence");
		_settings = _settingsManager.LoadSettings()
			?? throw new Exception("Cannot load the settings");
		_context = _settings.Confluence.Context.TrimEnd('/') + "/";
	}

	private readonly IHttpClientFactory _hcFactory;
	private readonly SettingsManager _settingsManager;

	private readonly HttpClient _client;
	private readonly CrawlerSettings _settings;
	private readonly string _context;

	public async IAsyncEnumerable<JObject> GetSpacesAsync(
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var content = await _client.GetStringAsync($"{_context}rest/api/space", cancellationToken).ConfigureAwait(false);
		var o = JObject.Parse(content);

		foreach (var eachSpace in o.SelectTokens("$.results[*]"))
			yield return (JObject)eachSpace;

		var context = o.SelectToken("$._links.context")?.Value<string>();
		var next = o.SelectToken("$._links.next")?.Value<string>();

		while (!string.IsNullOrWhiteSpace(next))
		{
			content = await _client.GetStringAsync($"{context}{next}", cancellationToken).ConfigureAwait(false);
			o = JObject.Parse(content);

			foreach (var eachSpace in o.SelectTokens("$.results[*]"))
				yield return (JObject)eachSpace;

			context = o.SelectToken("$._links.context")?.Value<string>();
			next = o.SelectToken("$._links.next")?.Value<string>();
		}
	}

	public async Task<JObject?> GetSpaceHomepageAsync(
		JObject? eachSpace,
		CancellationToken cancellationToken = default)
	{
		var targetUrl = eachSpace?.SelectToken("$._expandable.homepage")?.Value<string>();
		if (targetUrl == null)
			return null;

		try
		{
			var content = await _client.GetStringAsync($"{_context}{targetUrl}", cancellationToken).ConfigureAwait(false);
			var o = JObject.Parse(content);
			return o;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex.ToString());
			return null;
		}
	}

	public async Task<JObject?> GetChildrenInfoAsync(
		JObject? eachSpaceHomepage,
		CancellationToken cancellationToken = default)
	{
		var targetUrl = eachSpaceHomepage?.SelectToken("$._expandable.children")?.Value<string>();
		if (targetUrl == null)
			return null;

		try
		{
			var content = await _client.GetStringAsync($"{_context}{targetUrl}", cancellationToken).ConfigureAwait(false);
			var o = JObject.Parse(content);
			return o;
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine(ex.ToString());
			return null;
		}
	}

	public async IAsyncEnumerable<JObject> GetAttachmentsAsync(
		JObject? eachChildren,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var targetUrl = eachChildren?.SelectToken("$._expandable.attachment")?.Value<string>();
		if (targetUrl == null)
			yield break;

		var content = await _client.GetStringAsync($"{_context}{targetUrl}", cancellationToken).ConfigureAwait(false);
		var o = JObject.Parse(content);

		foreach (var eachSpace in o.SelectTokens("$.results[*]"))
			yield return (JObject)eachSpace;

		var context = o.SelectToken("$._links.context")?.Value<string>();
		var next = o.SelectToken("$._links.next")?.Value<string>();

		while (!string.IsNullOrWhiteSpace(next))
		{
			content = await _client.GetStringAsync($"{context}{next}", cancellationToken).ConfigureAwait(false);
			o = JObject.Parse(content);

			foreach (var eachSpace in o.SelectTokens("$.results[*]"))
				yield return (JObject)eachSpace;

			context = o.SelectToken("$._links.context")?.Value<string>();
			next = o.SelectToken("$._links.next")?.Value<string>();
		}
	}

	public async IAsyncEnumerable<JObject> GetPagesAsync(
		JObject? eachSpaceChildren,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var targetUrl = eachSpaceChildren?.SelectToken("$._expandable.page")?.Value<string>();
		if (targetUrl == null)
			yield break;

		var content = await _client.GetStringAsync($"{_context}{targetUrl}", cancellationToken).ConfigureAwait(false);
		var o = JObject.Parse(content);

		foreach (var eachSpace in o.SelectTokens("$.results[*]"))
			yield return (JObject)eachSpace;

		var context = o.SelectToken("$._links.context")?.Value<string>();
		var next = o.SelectToken("$._links.next")?.Value<string>();

		while (!string.IsNullOrWhiteSpace(next))
		{
			content = await _client.GetStringAsync($"{context}{next}", cancellationToken).ConfigureAwait(false);
			o = JObject.Parse(content);

			foreach (var eachSpace in o.SelectTokens("$.results[*]"))
				yield return (JObject)eachSpace;

			context = o.SelectToken("$._links.context")?.Value<string>();
			next = o.SelectToken("$._links.next")?.Value<string>();
		}
	}

	public async IAsyncEnumerable<PageTraverseResult> TraversePageAsync(
		JObject page,
		int depth,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var eachChildrenInfo = await this.GetChildrenInfoAsync(page, cancellationToken);

		await foreach (var eachSubPage in this.GetPagesAsync(eachChildrenInfo, cancellationToken))
		{
			yield return new PageTraverseResult(depth, eachSubPage);

			await foreach (var eachGrandSubPage in TraversePageAsync(eachSubPage, depth + 1, cancellationToken))
			{
				yield return eachGrandSubPage;
			}
		}
	}

	public async Task<JObject> GetContentDetailAsync(string contentId, CancellationToken cancellationToken = default)
	{
		var content = await _client.GetStringAsync($"{_context}rest/api/content/{contentId}?expand=space,body.storage", cancellationToken).ConfigureAwait(false);
		var o = JObject.Parse(content);
		return o;
	}

	public async Task<string> ConvertContentBodyAsync(string contentId, CancellationToken cancellationToken = default)
	{
		var o = await GetContentDetailAsync(contentId, cancellationToken);
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
		var content = await _client.PostAsync($"{_context}rest/api/contentbody/convert/{to}?spaceKeyContext={space}&contentIdContext={id}", requestContent, cancellationToken).ConfigureAwait(false);
		var resp = JObject.Parse(await content.Content.ReadAsStringAsync(cancellationToken));
		var converted = resp.SelectToken("value")?.Value<string>();
		return converted ?? string.Empty;
	}
}

public record PageTraverseResult(int Depth, JObject PageObject);
