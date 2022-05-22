using ConfluenceToNotionConverter.Contracts;

namespace ConfluenceToNotionConverter.Models
{
    internal sealed class ContentPropertyNode : IUniqueEntity
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public object Value { get; set; }
		public ContentPropertyValueType ValueType { get; set; }
	}
}
