using Microsoft.Extensions.Logging;
using MimeDetective;
using MimeDetective.Definitions;

namespace ConfluenceCrawler
{
    public sealed class ContentInspector
    {
        private readonly ILogger _logger;
        private readonly MimeTypeToFileExtensionLookup _mimeTypeToExtensionLookup;
        private readonly FileExtensionToMimeTypeLookup _extensionToMimeTypeLookup;

        public ContentInspector(ILogger<ContentInspector> logger)
        {
            _logger = logger;

            var mtfBuilder = new MimeTypeToFileExtensionLookupBuilder()
            {
                Definitions = Default.All(),
            };
            _mimeTypeToExtensionLookup = mtfBuilder.Build();

            var ftmBuilder = new FileExtensionToMimeTypeLookupBuilder()
            {
                Definitions = Default.All(),
            };
            _extensionToMimeTypeLookup = ftmBuilder.Build();
        }

        public IEnumerable<string> GetExtensions(string contentType)
            => _mimeTypeToExtensionLookup.TryGetValues(contentType).Select(x => x.Extension);

        public IEnumerable<string> GetContentTypes(string extension)
            => _extensionToMimeTypeLookup.TryGetValues(extension).Select(x => x.MimeType);
    }
}
