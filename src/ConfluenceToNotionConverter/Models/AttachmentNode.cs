using ConfluenceToNotionConverter.Contracts;
using System.IO.Compression;
using System.Xml;

namespace ConfluenceToNotionConverter.Models
{
    internal sealed class AttachmentNode : IUniqueEntity
	{
		public string Id { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public int Version { get; set; }
		public string OriginalVersionId { get; set; } = string.Empty;
		public string ParentPageId { get; set; } = string.Empty;
		public bool HasEmbedded { get; set; }

		internal ZipArchiveEntry RelatedEntry { get; set; }
		internal XmlElement AttachmentElement { get; set; }
		internal PageNode ParentPage { get; set; }
	}
}
