using Microsoft.Extensions.Logging;
using System.Text;

namespace ConfluenceCrawler;

public sealed class FileSystemHelper
{
    private readonly ILogger _logger;
    private readonly string _workingDirectoryPath;

    public FileSystemHelper(ILogger<FileSystemHelper> logger)
    {
        _logger = logger;
        _workingDirectoryPath = Path.Combine(Environment.CurrentDirectory, $"confluence-scrap-{DateTime.Now:yyyy-MM-dd-HH-MM-ss}");
    }

    public void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_workingDirectoryPath))
            Directory.CreateDirectory(_workingDirectoryPath);
    }

    public void SaveHtmlContent(string spaceKey, string contentId, string content)
    {
        _logger.LogInformation($"* Saving HTML content ID: {spaceKey}/{contentId}");

        var path = Path.Combine(_workingDirectoryPath, spaceKey);

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        File.WriteAllText(
            Path.Combine(path, $"{contentId}.html"),
            content, new UTF8Encoding(false));
    }
}
