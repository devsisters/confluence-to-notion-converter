using ConfluenceToNotionConverter.Models;
using HtmlAgilityPack;

namespace ConfluenceToNotionConverter
{
    internal static class HtmlNodeExtractor
	{
		public static string ExtractParameterTag(HtmlNode acTag, string parameterName)
		{
			var acParameterTags = acTag.SelectNodes(".//*[name() = 'ac:parameter']");

			if (acParameterTags != null)
			{
				foreach (var eachAcParameterTag in acParameterTags)
				{
					var name = eachAcParameterTag.Attributes["ac:name"]?.Value;

					if (string.Equals(name, parameterName, StringComparison.Ordinal))
						return eachAcParameterTag.InnerText;
				}
			}

			return null;
		}

		public static string ExtractPlainTextBody(HtmlNode acTag)
		{
			var acPlainTextBodyTag = acTag.SelectNodes(".//*[name() = 'ac:plain-text-body']");

			if (acPlainTextBodyTag != null)
			{
				foreach (var eachAcPlainTextBodyTag in acPlainTextBodyTag)
				{
					return eachAcPlainTextBodyTag.InnerText;
				}
			}

			return null;
		}

		public static string ExtractRichTextBody(HtmlNode acTag)
		{
			var acRichTextBodyTag = acTag.SelectNodes(".//*[name() = 'ac:rich-text-body']");

			if (acRichTextBodyTag != null)
			{
				foreach (var eachAcRichTextBodyTag in acRichTextBodyTag)
				{
					return eachAcRichTextBodyTag.InnerText;
				}
			}

			return null;
		}

		public static bool ExtractAttachment(HtmlNode acTag, ItemCollection<AttachmentNode> attachments, out string fileName, out int? versionAtSave, out string linkBody)
		{
			fileName = null;
			versionAtSave = null;
			linkBody = null;

			var riAttachmentTag = acTag.SelectNodes(".//*[name() = 'ri:attachment']")?.FirstOrDefault();
			if (riAttachmentTag != null)
			{
				versionAtSave = int.TryParse(
					riAttachmentTag.Attributes["ri:version-at-save"]?.Value,
					out int parsedVersionAtSave) ? parsedVersionAtSave : default(int?);

				fileName = riAttachmentTag.Attributes["ri:filename"]?.Value;
				if (fileName != null)
					fileName = fileName.Normalize();

				var fileNameAttr = fileName;
				var versionAtSaveAttr = versionAtSave;
				var att = attachments.Where(x => string.Equals(x.Title, fileNameAttr, StringComparison.Ordinal) && x.Version == versionAtSaveAttr).FirstOrDefault();
				if (att == null)
					fileName = string.Concat("Missing_", fileName);
				else
					fileName = $"attachments/{att.ParentPageId}/{att.Id}{Path.GetExtension(att.Title)}";

				var linkBodyTag = acTag.SelectNodes(".//*[name() = 'ac:link-body']")?.FirstOrDefault();
				linkBody = linkBodyTag?.InnerHtml;

				if (linkBody == null)
				{
					var plainTextLinkBodyTag = acTag.SelectNodes(".//*[name() = 'ac:plain-text-link-body']")?.FirstOrDefault();
					linkBody = plainTextLinkBodyTag?.InnerHtml;
				}

				return true;
			}

			return false;
		}

		public static string ExtractUrl(HtmlNode acTag)
		{
			var riUrlTag = acTag.SelectNodes(".//*[name() = 'ri:url']")?.FirstOrDefault();

			if (riUrlTag != null)
				return riUrlTag.Attributes["ri:value"]?.Value;

			return null;
		}
	}
}
