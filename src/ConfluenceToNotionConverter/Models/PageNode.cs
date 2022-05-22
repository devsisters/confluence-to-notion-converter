using ConfluenceToNotionConverter.Contracts;
using System.Xml;

namespace ConfluenceToNotionConverter.Models
{
    internal sealed class PageNode : IUniqueEntity
	{
		public string Id { get; set; } = string.Empty;
		public string Title { get; set; } = string.Empty;
		public string ParentId { get; set; } = string.Empty;
		public string ContentStatus { get; set; } = string.Empty;
		public string OriginalVersionId { get; set; } = string.Empty;
		public int Version { get; set; }

		public List<BodyContentNode> BodyContents { get; } = new List<BodyContentNode>();
		internal List<ContentPropertyNode> ContentProperties { get; } = new List<ContentPropertyNode>();
		internal List<AttachmentNode> Attachments { get; } = new List<AttachmentNode>();

		internal XmlElement PageElement { get; set; }
		internal PageNode ParentPage { get; set; }
		internal List<PageNode> ChildPages { get; } = new List<PageNode>();
	}
}
