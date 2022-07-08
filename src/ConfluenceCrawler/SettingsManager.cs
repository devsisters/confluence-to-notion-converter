namespace ConfluenceCrawler;

using Microsoft.Extensions.Logging;
using SharpYaml.Serialization;

public sealed class SettingsManager
{
    private readonly ILogger<SettingsManager> _logger;
    private readonly Serializer _serializer;

    public SettingsManager(ILogger<SettingsManager> logger, Serializer serializer)
    {
        _logger = logger;
        _serializer = serializer;
    }

    public CrawlerSettings? LoadSettings()
    {
        var settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "confluence_crawler_settings.yaml");

        if (!File.Exists(settingsFilePath))
            return null;

        using var fileStream = File.OpenRead(settingsFilePath);
        return _serializer.Deserialize<CrawlerSettings>(fileStream);
    }
}

public sealed class ConfluenceSettings
{
    [YamlMember("domain")]
    public string Domain { get; set; } = string.Empty;

    [YamlMember("context")]
    public string Context { get; set; } = "wiki/";

    [YamlMember("userName")]
    public string UserName { get; set; } = string.Empty;

    [YamlMember("token")]
    public string Token { get; set; } = string.Empty;
}

public sealed class NotionSettings
{
    [YamlMember("authToken")]
    public string AuthToken { get; set; } = string.Empty;

    [YamlMember("pageId")]
    public string PageId { get; set; } = string.Empty;
}

public sealed class CrawlerSettings
{
    [YamlMember("confluence")]
    public ConfluenceSettings Confluence { get; set; } = new ConfluenceSettings();

    [YamlMember("notion")]
    public NotionSettings Notion { get; set; } = new NotionSettings();
}
