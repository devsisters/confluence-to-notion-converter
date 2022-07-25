using Microsoft.Extensions.Logging;
using System.Text;

namespace ConfluenceCrawler;

public sealed class FileSystemHelper
{
    private readonly ILogger _logger;
    private readonly string _workingDirectoryPath;
    private readonly ContentInspector _contentInspector;

    public FileSystemHelper(ILogger<FileSystemHelper> logger, ContentInspector contentInspector)
    {
        _logger = logger;
        _workingDirectoryPath = Path.Combine(Environment.CurrentDirectory, $"confluence-scrap-{DateTime.Now:yyyy-MM-dd-HH-MM-ss}");
        _contentInspector = contentInspector;
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

    public string SaveResource(string spaceKey, HttpResponseMessage responseMessage)
    {
        if (responseMessage == null)
            throw new ArgumentNullException(nameof(responseMessage));

        _logger.LogInformation($"* Saving Resource: {spaceKey}, {responseMessage.RequestMessage?.RequestUri}");

        if (!responseMessage.IsSuccessStatusCode)
            _logger.LogWarning($"* Cannot save resource due to error code - {responseMessage.StatusCode}");

        var path = Path.Combine(_workingDirectoryPath, spaceKey, "attachments");

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        var fileName = responseMessage?.Content?.Headers?.ContentDisposition?.FileName?.Trim('"');

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"{Guid.NewGuid().ToString("n")}";

        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            var mediaType = responseMessage?.Content?.Headers?.ContentType?.MediaType;
            if (!string.IsNullOrWhiteSpace(mediaType))
            {
                var extension = _contentInspector.GetExtensions(mediaType).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(extension))
                    extension = "." + extension.TrimStart('.');
                fileName += extension;
            }
        }

        var fullPath = Path.Combine(path, fileName);

        using (var remoteStream = responseMessage.Content.ReadAsStream())
        using (var fileStream = File.OpenWrite(fullPath))
        {
            remoteStream.CopyTo(fileStream);
        }

        return fullPath;
    }
}
