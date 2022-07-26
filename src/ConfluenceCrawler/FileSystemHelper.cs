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

    public void SaveHtmlContent(string contentId, string content)
    {
        _logger.LogInformation($"* Saving HTML content ID: {contentId}");

        var path = _workingDirectoryPath;

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        File.WriteAllText(
            Path.Combine(path, $"{contentId}.html"),
            content, new UTF8Encoding(false));
    }

    public string SaveImageResource(HttpResponseMessage responseMessage)
    {
        var requestMessage = responseMessage.RequestMessage;

        if (requestMessage == null)
            throw new HttpRequestException("Cannot obtain the HTTP request object.");

        var responseContent = responseMessage.Content;

        if (responseContent == null)
            throw new HttpRequestException("Cannot obtain the HTTP  object.");

        _logger.LogInformation($"* Saving Resource: {requestMessage.RequestUri}");

        if (!responseMessage.IsSuccessStatusCode)
            _logger.LogWarning($"* Cannot save resource due to error code - {responseMessage.StatusCode}");

        var path = Path.Combine(_workingDirectoryPath, "images");

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        var fileName = responseContent.Headers.ContentDisposition?.FileNameStar?.Trim('"')?.Normalize();

        if (string.IsNullOrWhiteSpace(fileName))
            fileName = $"{Guid.NewGuid().ToString("n")}";

        if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
        {
            var mediaType = responseContent.Headers?.ContentType?.MediaType;
            if (!string.IsNullOrWhiteSpace(mediaType))
            {
                var extension = _contentInspector.GetExtensions(mediaType).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(extension))
                    extension = "." + extension.TrimStart('.');
                fileName += extension;
            }
        }

        var fullPath = Path.Combine(path, fileName);

        using (var remoteStream = responseContent.ReadAsStream())
        using (var fileStream = File.OpenWrite(fullPath))
        {
            remoteStream.CopyTo(fileStream);
        }

        return fileName;
    }
}
