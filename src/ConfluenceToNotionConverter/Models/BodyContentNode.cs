using ConfluenceToNotionConverter.Contracts;
using System.Xml;

namespace ConfluenceToNotionConverter.Models
{
    internal sealed class BodyContentNode : IUniqueEntity
	{
		public string Id { get; set; } = string.Empty;
		public string Body { get; set; } = string.Empty;
		public string Markdown { get; set; } = string.Empty;
		public string Content { get; set; } = string.Empty;
		public string BodyType { get; set; } = string.Empty;

		internal XmlElement BodyContentElement { get; set; }
	}
}
