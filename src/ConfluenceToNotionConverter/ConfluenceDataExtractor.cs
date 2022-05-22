using ConfluenceToNotionConverter.Models;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace ConfluenceToNotionConverter
{
    internal static class ConfluenceDataExtractor
	{
		public static IEnumerable<KeyValuePair<string, string>> ExtractProperties(ZipArchive zipArchive)
		{
			var props = new List<KeyValuePair<string, string>>();
			var propFileEntry = zipArchive.GetEntry("exportDescriptor.properties");

			if (propFileEntry == null)
				return props.ToArray();

			var content = new StreamReader(propFileEntry.Open(), new UTF8Encoding(false), true);
			var eachLine = default(string);

			while ((eachLine = content.ReadLine()) != null)
			{
				eachLine = eachLine.Trim();
				if (eachLine.StartsWith("#"))
					continue;
				var parts = eachLine.Split(new char[] { '=', }, StringSplitOptions.RemoveEmptyEntries);
				var key = parts.FirstOrDefault();
				var val = parts.LastOrDefault();
				props.Add(new KeyValuePair<string, string>(key, val));
			}

			return props;
		}

		public static ItemCollection<AttachmentNode> ExtractAttachments(XmlDocument xmlDoc, ItemCollection<PageNode> pageNodes, ZipArchive zipArchive)
		{
			var collection = new ItemCollection<AttachmentNode>();

			foreach (var eachAttachmentNodeElement in ConfluenceNodeExtractor.PopulateAttachments(xmlDoc))
			{
				var contentStatus = eachAttachmentNodeElement.SelectSingleNode("property[@name = 'contentStatus']/text()").InnerText;
				if (!string.Equals(contentStatus, "current", StringComparison.Ordinal))
					continue;

				var id = eachAttachmentNodeElement.SelectSingleNode("id/text()").InnerText;
				var title = eachAttachmentNodeElement.SelectSingleNode("property[@name = 'title']/text()").InnerText;
				if (Uri.TryCreate(title, UriKind.RelativeOrAbsolute, out Uri parsedTitle))
				{
					if (!parsedTitle.IsAbsoluteUri)
						parsedTitle = new Uri(new Uri("file:///"), parsedTitle);

					title = Path.GetFileName(parsedTitle.GetComponents(UriComponents.Path, UriFormat.SafeUnescaped));
				}

				var parentId = eachAttachmentNodeElement.SelectSingleNode("property[@name = 'parent']/id/text()")?.InnerText;
				var originalId = eachAttachmentNodeElement.SelectSingleNode("property[@name = 'originalVersion']/id/text()")?.InnerText;
				var version = int.Parse(eachAttachmentNodeElement.SelectSingleNode("property[@name = 'version']/text()").InnerText);
				var parentPageId = eachAttachmentNodeElement.SelectSingleNode("property[@name = 'containerContent' and @class = 'Page' and @package = 'com.atlassian.confluence.pages']/id/text()").InnerText;

				var parentPage = default(PageNode);
				if (pageNodes.Contains(parentPageId))
					parentPage = pageNodes[parentPageId];

				var node = new AttachmentNode()
				{
					Id = id,
					Title = title,
					Version = version,
					OriginalVersionId = originalId,
					ParentPageId = parentPageId,

					RelatedEntry = zipArchive.GetEntry($"attachments/{parentPageId}/{id}/{version}"),
					AttachmentElement = eachAttachmentNodeElement,
					ParentPage = parentPage,
				};

				parentPage.Attachments.Add(node);
				collection.Add(node);
			}

			foreach (var eachGroup in collection.ToList().GroupBy(x => x.OriginalVersionId))
			{
				if (eachGroup.Key == null)
					continue;
				var olderVersions = eachGroup.OrderByDescending(x => x.Version).Skip(1);
				foreach (var eachOlderVersion in olderVersions)
					collection.Remove(eachOlderVersion.Id);
			}

			return collection;
		}

		public static ItemCollection<BodyContentNode> ExtractBodyContents(XmlDocument xmlDoc, ItemCollection<AttachmentNode> attachments, ItemCollection<PageNode> pages)
		{
			var collection = new ItemCollection<BodyContentNode>();

			foreach (var eachBodyContentNodeElement in ConfluenceNodeExtractor.PopulateBodyContents(xmlDoc))
			{
				var id = eachBodyContentNodeElement.SelectSingleNode("id/text()").InnerText;
				var body = HtmlProcessor.NormalizeBodyContent(eachBodyContentNodeElement.SelectSingleNode("property[@name = 'body']/text()")?.InnerText, attachments, pages);
				//var markdown = PerformConvertToMarkdown(body);
				var content = eachBodyContentNodeElement.SelectSingleNode("property[@name = 'content']/text()")?.InnerText;
				var bodyType = eachBodyContentNodeElement.SelectSingleNode("property[@name = 'bodyType']/text()")?.InnerText;

				var node = new BodyContentNode()
				{
					Id = id,
					Body = body,
					//Markdown = markdown,
					Content = content,
					BodyType = bodyType,
					BodyContentElement = eachBodyContentNodeElement,
				};

				collection.Add(node);
			}

			return collection;
		}

		public static ItemCollection<ContentPropertyNode> ExtractContentProperties(XmlDocument xmlDoc)
		{
			var collection = new ItemCollection<ContentPropertyNode>();

			foreach (var eachContentPropertyNodeElement in ConfluenceNodeExtractor.PopulateContentProperties(xmlDoc))
			{
				var id = eachContentPropertyNodeElement.SelectSingleNode("id/text()").InnerText;
				var propertyName = eachContentPropertyNodeElement.SelectSingleNode("property[@name = 'name']/text()")?.InnerText;
				var stringValue = eachContentPropertyNodeElement.SelectSingleNode("property[@name = 'stringValue']/text()")?.InnerText;
				var longValue = eachContentPropertyNodeElement.SelectSingleNode("property[@name = 'longValue']/text()")?.InnerText;
				var dateValue = eachContentPropertyNodeElement.SelectSingleNode("property[@name = 'dateValue']/text()")?.InnerText;

				var node = new ContentPropertyNode()
				{
					Id = id,
					Name = propertyName,
				};

				if (DateTime.TryParse(dateValue, out DateTime parsedDateTime))
				{
					node.Value = parsedDateTime;
					node.ValueType = ContentPropertyValueType.DateTime;
				}
				else if (long.TryParse(longValue, out long parsedNumeric))
				{
					node.Value = parsedNumeric;
					node.ValueType = ContentPropertyValueType.Long;
				}
				else
				{
					node.Value = stringValue;
					node.ValueType = ContentPropertyValueType.String;
				}

				collection.Add(node);
			}

			return collection;
		}

		// 페이지 트리 구조 추출
		public static ItemCollection<PageNode> ExtractPageStructure(
			XmlDocument xmlDoc,
			ItemCollection<ContentPropertyNode> contentProps)
		{
			var collection = new ItemCollection<PageNode>();

			var rawPageNodes = ConfluenceNodeExtractor.PopulatePages(xmlDoc);
			var olderPageIds = new List<string>();

			foreach (var eachPageElement in rawPageNodes)
			{
				var contentStatus = eachPageElement.SelectSingleNode("property[@name = 'contentStatus']/text()").InnerText;
				if (!string.Equals(contentStatus, "current", StringComparison.Ordinal))
					continue;

				var historicalIdElem = eachPageElement.SelectSingleNode("collection[@name = 'historicalVersions']");

				if (historicalIdElem != null)
				{
					var idList = historicalIdElem.SelectNodes("./element[@class = 'Page']//id/text()").Cast<XmlNode>().Select(x => x.InnerText);
					olderPageIds.AddRange(idList.Skip(1).ToList());
				}

				var id = eachPageElement.SelectSingleNode("id/text()").InnerText;
				var title = eachPageElement.SelectSingleNode("property[@name = 'title']/text()").InnerText;
				var parentId = eachPageElement.SelectSingleNode("property[@name = 'parent']/id/text()")?.InnerText;
				var originalId = eachPageElement.SelectSingleNode("property[@name = 'originalVersion']/id/text()")?.InnerText;
				var version = int.Parse(eachPageElement.SelectSingleNode("property[@name = 'version']/text()").InnerText);

				var node = new PageNode()
				{
					Id = id,
					Title = title,
					ParentId = parentId,
					ContentStatus = contentStatus,
					OriginalVersionId = originalId,
					Version = version,
					PageElement = eachPageElement,
				};

				collection.Add(node);
			}

			foreach (var eachOlderId in olderPageIds.Distinct())
			{
				if (collection.Contains(eachOlderId))
					collection.Remove(eachOlderId);
			}

			/*
					foreach (var eachPageNode in collection)
					{
						if (string.IsNullOrWhiteSpace(eachPageNode.ParentId))
							continue;

						var parentPageNode = collection[eachPageNode.ParentId];
						eachPageNode.ParentPage = parentPageNode;
						parentPageNode.ChildPages.Add(eachPageNode);
					}
			*/

			foreach (var eachGroup in collection.ToList().GroupBy(x => x.OriginalVersionId))
			{
				if (eachGroup.Key == null)
					continue;
				var olderVersions = eachGroup.OrderByDescending(x => x.Version);
				foreach (var eachOlderVersion in olderVersions)
					collection.Remove(eachOlderVersion.Id);
			}

			return collection;
		}

		public static ItemCollection<PageNode> FillPageContents(
			ItemCollection<PageNode> collection,
			ItemCollection<ContentPropertyNode> contentProps,
			ItemCollection<AttachmentNode> attachments,
			ItemCollection<BodyContentNode> bodyContents)
		{
			foreach (var node in collection)
			{
				var eachPageElement = node.PageElement;

				foreach (var eachBodyContentId in eachPageElement.SelectNodes("collection[@name = 'bodyContents']/element/id/text()").Cast<XmlNode>())
				{
					node.BodyContents.Add(bodyContents[eachBodyContentId.InnerText]);
				}

				foreach (var eachContentPropId in eachPageElement.SelectNodes("collection[@name = 'contentProperties']/element/id/text()").Cast<XmlNode>())
				{
					node.ContentProperties.Add(contentProps[eachContentPropId.InnerText]);
				}
			}

			return collection;
		}
	}
}
