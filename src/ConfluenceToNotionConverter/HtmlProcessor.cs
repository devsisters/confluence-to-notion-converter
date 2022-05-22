using ConfluenceToNotionConverter.Models;
using HtmlAgilityPack;
using ImageMagick;

namespace ConfluenceToNotionConverter
{
    internal static class HtmlProcessor
	{
		public static string NormalizeBodyContent(string htmlFragment, ItemCollection<AttachmentNode> attachments, ItemCollection<PageNode> pages)
		{
			// https://stackoverflow.com/questions/25550946/remove-span-tags-but-keep-the-text

			if (string.IsNullOrWhiteSpace(htmlFragment))
				return htmlFragment;

			var document = new HtmlDocument();
			document.LoadHtml(htmlFragment);

			var spanTags = document.DocumentNode.SelectNodes("//span");
			if (spanTags != null)
			{
				var nodes = new Queue<HtmlNode>(spanTags);

				foreach (var node in nodes)
					node.ParentNode.RemoveChild(node, true);
			}

			var divTags = document.DocumentNode.SelectNodes("//div");
			if (divTags != null)
			{
				var nodes = new Queue<HtmlNode>(divTags);

				foreach (var node in nodes)
					node.ParentNode.RemoveChild(node, true);
			}

			var acTaskLists = document.DocumentNode.SelectNodes("//*[name() = 'ac:task-list']");
			if (acTaskLists != null && acTaskLists.Count > 0)
			{
				foreach (var eachAcTaskList in acTaskLists)
					ReplaceAttachedTaskListTag(eachAcTaskList);
			}

			var acLayoutTags = document.DocumentNode.SelectNodes("//*[name() = 'ac:layout']");
			if (acLayoutTags != null && acLayoutTags.Count > 0)
			{
				foreach (var eachAcLayoutTag in acLayoutTags)
					ReplaceAttachedLayoutTag(eachAcLayoutTag);
			}

			var acImageTags = document.DocumentNode.SelectNodes("//*[name() = 'ac:image']");
			if (acImageTags != null && acImageTags.Count > 0)
			{
				foreach (var eachAcImageTag in acImageTags)
					ReplaceAttachedImageTag(eachAcImageTag, attachments);
			}

			var acLinkTags = document.DocumentNode.SelectNodes("//*[name() = 'ac:link']");
			if (acLinkTags != null && acLinkTags.Count > 0)
			{
				foreach (var eachAcLinkTag in acLinkTags)
					ReplaceAttachedLinkTag(eachAcLinkTag, attachments, pages);
			}

			var acMacroTags = document.DocumentNode.SelectNodes("//*[name() = 'ac:macro']");
			if (acMacroTags != null && acMacroTags.Count > 0)
			{
				foreach (var eachAcMacroTag in acMacroTags)
					ReplaceAttachedMacroTag(eachAcMacroTag, attachments);
			}

			var acStructuredMacroTags = document.DocumentNode.SelectNodes("//*[name() = 'ac:structured-macro']");
			if (acStructuredMacroTags != null && acStructuredMacroTags.Count > 0)
			{
				foreach (var eachAcMacroTag in acStructuredMacroTags)
					ReplaceAttachedStructuredMacroTag(eachAcMacroTag, attachments);
			}

			var acEmoticonTags = document.DocumentNode.SelectNodes("//*[name() = 'ac:emoticon']");
			if (acEmoticonTags != null && acEmoticonTags.Count > 0)
			{
				foreach (var eachAcEmoticonTag in acEmoticonTags)
					ReplaceAttachedEmoticonTag(eachAcEmoticonTag);
			}

			var acInlineCommentMarkers = document.DocumentNode.SelectNodes("//*[name() = 'ac:inline-comment-marker']");
			if (acInlineCommentMarkers != null && acInlineCommentMarkers.Count > 0)
			{
				foreach (var eachAcInlineCommentMarker in acInlineCommentMarkers)
					ReplaceAttachedInlineCommentMarkerTag(eachAcInlineCommentMarker);
			}

			return document.DocumentNode.InnerHtml;
		}

		static void ReplaceAttachedImageTag(HtmlNode acImageTagNode, ItemCollection<AttachmentNode> attachments)
		{
			var width = int.TryParse(
				acImageTagNode.Attributes["ac:width"]?.Value,
				out int parsedWidth) ? parsedWidth : default(int?);
			var height = int.TryParse(
				acImageTagNode.Attributes["ac:height"]?.Value,
				out int parsedHeight) ? parsedHeight : default(int?);

			var caption = default(string);
			var acCaptionTag = acImageTagNode.SelectNodes(".//*[name() = 'ac:caption']")?.FirstOrDefault();
			if (acCaptionTag != null)
				caption = acCaptionTag.InnerText;

			var riAttachmentTag = acImageTagNode.SelectNodes(".//*[name() = 'ri:attachment']")?.FirstOrDefault();
			if (riAttachmentTag != null)
			{
				var versionAtSave = int.TryParse(
					riAttachmentTag.Attributes["ri:version-at-save"]?.Value,
					out int parsedVersionAtSave) ? parsedVersionAtSave : default(int?);

				var fileName = riAttachmentTag.Attributes["ri:filename"]?.Value;
				if (fileName != null)
					fileName = fileName.Normalize();

				var att = attachments.Where(x => string.Equals(x.Title, fileName, StringComparison.Ordinal) && x.Version == versionAtSave).FirstOrDefault();
				if (att == null)
					fileName = string.Concat("Missing_", fileName);
				else
					fileName = $"attachments/{att.ParentPageId}/{att.Id}{Path.GetExtension(att.Title)}";

				var newImgTag = acImageTagNode.OwnerDocument.CreateElement("img");

				if (height.HasValue)
				{
					newImgTag.SetAttributeValue("height", Convert.ToString(height));
					newImgTag.SetAttributeValue("data-height", Convert.ToString(height));
				}

				if (width.HasValue)
				{
					newImgTag.SetAttributeValue("width", Convert.ToString(width));
					newImgTag.SetAttributeValue("data-width", Convert.ToString(width));
				}

				if (att != null)
				{
					var contentType = XmlArchiveConverter.GetContentType(Path.GetExtension(att.Title));
					if (att.RelatedEntry != null && XmlArchiveConverter.IsAllowedExtension(att.Title))
					{
						using (var fs = att.RelatedEntry.Open())
						using (var ms = new MemoryStream())
						{
							using (var magick = new MagickImage(fs))
							{
								magick.Strip();
								magick.Quality = 75;
								magick.Write(ms);
							}

							newImgTag.SetAttributeValue("src", $"data:{contentType};base64,{Convert.ToBase64String(ms.ToArray())}");
							att.HasEmbedded = true;
						}
					}
					else
						newImgTag.SetAttributeValue("src", Convert.ToString(fileName));

					newImgTag.SetAttributeValue("alt", Convert.ToString(fileName));
					newImgTag.SetAttributeValue("data-image-src", Convert.ToString(fileName));

					newImgTag.SetAttributeValue("data-linked-resource-id", att.Id);
					newImgTag.SetAttributeValue("data-linked-resource-version", att.Version.ToString());
					newImgTag.SetAttributeValue("data-linked-resource-type", "attachment");
					newImgTag.SetAttributeValue("data-linked-resource-default-alias", att.Title);
					newImgTag.SetAttributeValue("data-linked-resource-content-type", contentType);
					newImgTag.SetAttributeValue("data-linked-resource-container-id", att.ParentPage?.Id);
					newImgTag.SetAttributeValue("data-media-id", Guid.NewGuid().ToString().ToLowerInvariant());
					newImgTag.SetAttributeValue("data-media-type", "file");
				}

				acImageTagNode.ParentNode.InsertAfter(newImgTag, acImageTagNode);

				if (!string.IsNullOrWhiteSpace(caption))
				{
					var captionTag = acImageTagNode.OwnerDocument.CreateElement("span");
					captionTag.InnerHtml = caption;
					newImgTag.ParentNode.InsertAfter(captionTag, newImgTag);
				}

				acImageTagNode.Remove();
			}

			var riUrlTag = acImageTagNode.SelectNodes(".//*[name() = 'ri:url']")?.FirstOrDefault();
			if (riUrlTag != null)
			{
				var url = riUrlTag.Attributes["ri:url"]?.Value;
				var newImgTag = acImageTagNode.OwnerDocument.CreateElement("img");
				newImgTag.SetAttributeValue("src", url);

				if (width.HasValue)
					newImgTag.SetAttributeValue("width", Convert.ToString(width));

				if (height.HasValue)
					newImgTag.SetAttributeValue("height", Convert.ToString(height));

				acImageTagNode.ParentNode.InsertAfter(newImgTag, acImageTagNode);

				if (!string.IsNullOrWhiteSpace(caption))
				{
					var captionTag = acImageTagNode.OwnerDocument.CreateElement("span");
					captionTag.InnerHtml = caption;
					newImgTag.ParentNode.InsertAfter(captionTag, newImgTag);
				}

				acImageTagNode.Remove();
			}
		}

		static void ReplaceAttachedLinkTag(HtmlNode acLinkTagNode, ItemCollection<AttachmentNode> attachments, ItemCollection<PageNode> pages)
		{
			var riAttachmentTag = acLinkTagNode.SelectNodes(".//*[name() = 'ri:attachment']")?.FirstOrDefault();
			if (riAttachmentTag != null)
			{
				var versionAtSave = int.TryParse(
					riAttachmentTag.Attributes["ri:version-at-save"]?.Value,
					out int parsedVersionAtSave) ? parsedVersionAtSave : default(int?);

				var fileName = riAttachmentTag.Attributes["ri:filename"]?.Value;
				if (fileName != null)
					fileName = fileName.Normalize();

				var att = attachments.Where(x => string.Equals(x.Title, fileName, StringComparison.Ordinal) && x.Version == versionAtSave).FirstOrDefault();
				if (att == null)
					fileName = string.Concat("Missing_", fileName);
				else
					fileName = $"attachments/{att.ParentPageId}/{att.Id}{Path.GetExtension(att.Title)}";

				var newAnchorTag = acLinkTagNode.OwnerDocument.CreateElement("a");
				newAnchorTag.SetAttributeValue("href", Convert.ToString(fileName));
				acLinkTagNode.ParentNode.InsertAfter(newAnchorTag, acLinkTagNode);
				acLinkTagNode.Remove();
				return;
			}

			var riPageTag = acLinkTagNode.SelectNodes(".//*[name() = 'ri:page']")?.FirstOrDefault();
			if (riPageTag != null)
			{
				var versionAtSave = int.TryParse(
					riPageTag.Attributes["ri:version-at-save"]?.Value,
					out int parsedVersionAtSave) ? parsedVersionAtSave : default(int?);

				var contentTitle = riPageTag.Attributes["ri:content-title"]?.Value;
				if (contentTitle != null)
					contentTitle = contentTitle.Normalize();

				var page = pages.Where(x => string.Equals(x.Title, contentTitle, StringComparison.Ordinal) && x.Version == versionAtSave).FirstOrDefault();
				var href = string.IsNullOrWhiteSpace(page?.Id) ? "missing" : page?.Id;
				if (page == null)
					contentTitle = string.Concat("Missing_", contentTitle);

				var newAnchorTag = acLinkTagNode.OwnerDocument.CreateElement("a");
				newAnchorTag.SetAttributeValue("href", href + ".html");
				acLinkTagNode.ParentNode.InsertAfter(newAnchorTag, acLinkTagNode);
				acLinkTagNode.Remove();
				return;
			}

			var riUserTag = acLinkTagNode.SelectNodes(".//*[name() = 'ri:user']")?.FirstOrDefault();
			if (riUserTag != null)
			{
				var userKey = riUserTag.Attributes["ri:userkey"]?.Value;

				// To Do: 사용자 이름 치환 필요
				var userName = string.IsNullOrWhiteSpace(userKey) ? "(알 수 없는 사용자)" : userKey;

				var newSpanTag = acLinkTagNode.OwnerDocument.CreateElement("span");
				newSpanTag.InnerHtml = userName;
				acLinkTagNode.ParentNode.InsertAfter(newSpanTag, acLinkTagNode);
				acLinkTagNode.Remove();
				return;
			}

			var riSpaceTag = acLinkTagNode.SelectNodes(".//*[name() = 'ri:space']")?.FirstOrDefault();
			if (riSpaceTag != null)
			{
				var spaceKey = riSpaceTag.Attributes["ri:space-key"]?.Value;

				// To Do: 스페이스 이름 치환 필요
				var spaceName = string.IsNullOrWhiteSpace(spaceKey) ? "(알 수 없는 스페이스)" : spaceKey;

				var newSpanTag = acLinkTagNode.OwnerDocument.CreateElement("span");
				newSpanTag.InnerHtml = spaceName;
				acLinkTagNode.ParentNode.InsertAfter(newSpanTag, acLinkTagNode);
				acLinkTagNode.Remove();
				return;
			}

			// To Do: content-entity에서 content-id, version-at-save 가져오기 필요

			var acPlainTextLinkBodyTag = acLinkTagNode.SelectNodes(".//*[name() = 'ac:plain-text-link-body']")?.FirstOrDefault();
			if (acPlainTextLinkBodyTag != null)
			{
				var fileName = acPlainTextLinkBodyTag.InnerText;

				var newSpanTag = acLinkTagNode.OwnerDocument.CreateElement("span");
				newSpanTag.InnerHtml = $"(To Do: {fileName} 연결 필요)";
				acLinkTagNode.ParentNode.InsertAfter(newSpanTag, acLinkTagNode);
				acLinkTagNode.Remove();
				return;
			}

			var remainingNodes = acLinkTagNode.SelectNodes(".//*");
			if (remainingNodes == null || remainingNodes.Count < 1)
			{
				var parentNode = acLinkTagNode.ParentNode;
				parentNode.RemoveChild(acLinkTagNode, true);
			}
		}

		static void ReplaceAttachedTaskListTag(HtmlNode acTaskListTag)
		{
			var acTasks = acTaskListTag.SelectNodes(".//*[name() = 'ac:task']");
			var taskItems = new List<string>();

			if (acTasks != null && acTasks.Count > 0)
			{
				foreach (var eachAcTask in acTasks)
				{
					var acTaskId = eachAcTask.SelectNodes(".//*[name() = 'ac:task-id']")?.FirstOrDefault();
					var acTaskStatus = eachAcTask.SelectNodes(".//*[name() = 'ac:task-status']")?.FirstOrDefault();
					var acTaskBody = eachAcTask.SelectNodes(".//*[name() = 'ac:task-body']")?.FirstOrDefault();
					taskItems.Add($"<li>#{acTaskId?.InnerText} - {acTaskBody?.InnerText} ({acTaskStatus?.InnerText})</li>");
				}
			}

			var newUlTag = acTaskListTag.OwnerDocument.CreateElement("ul");
			newUlTag.InnerHtml = string.Concat(taskItems);
			acTaskListTag.ParentNode.InsertAfter(newUlTag, acTaskListTag);
			acTaskListTag.Remove();
		}

		static void ReplaceAttachedEmoticonTag(HtmlNode acEmoticonTag)
		{
			var name = acEmoticonTag.Attributes["ac:name"]?.Value;
			var fallback = acEmoticonTag.Attributes["ac:emoji-fallback"]?.Value;
			var @char = fallback;

			if (string.IsNullOrWhiteSpace(fallback))
				@char = "?";

			var newSpanTag = acEmoticonTag.OwnerDocument.CreateElement("span");
			newSpanTag.InnerHtml = @char;
			acEmoticonTag.ParentNode.InsertAfter(newSpanTag, acEmoticonTag);
			acEmoticonTag.Remove();
		}

		static void ReplaceAttachedMacroTag(HtmlNode acMacroTag, ItemCollection<AttachmentNode> attachments)
		{
			var name = acMacroTag.Attributes["ac:name"]?.Value;
			var newTag = default(HtmlNode);

			switch (name)
			{
				case "content-report-table":
					var crContentBlueprintId = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "contentBlueprintId");
					var crBlueprintModuleCompleteKey = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "blueprintModuleCompleteKey");
					var crAnalyticsKey = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "analyticsKey");
					var crBlankDescription = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "blankDescription");
					var crBlankTitle = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "blankTitle");
					var crSpaces = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "spaces");
					var crCreateButtonLabel = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "createButtonLabel");
					var crLabels = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "labels");

					newTag = acMacroTag.OwnerDocument.CreateElement("pre");
					newTag.InnerHtml = @$"(To Do: Content Report Table 요소는 지원되지 않습니다. 아래는 추후 작업을 위한 데이터입니다.)

- contentBlueprintId: {crContentBlueprintId}
- blueprintModuleCompleteKey: {crBlueprintModuleCompleteKey}
- analyticsKey: {crAnalyticsKey}
- blankDescription: {crBlankDescription}
- blankTitle: {crBlankTitle}
- spaces: {crSpaces}
- createButtonLabel: {crCreateButtonLabel}
- labels: {crLabels}
";
					break;
				case "detailssummary":
					var dsAnalyticsKey = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "analyticsKey");
					var dsLabel = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "label");

					newTag = acMacroTag.OwnerDocument.CreateElement("pre");
					newTag.InnerHtml = @$"(To Do: Details Summary 요소는 지원되지 않습니다. 아래는 추후 작업을 위한 데이터입니다.)

- label: {dsLabel}
- analyticsKey: {dsAnalyticsKey}
";
					break;
				case "gliffy":
					var giffyName = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "name");
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = $"(To Do: Gliffy 다이어그램은 직접 추가해야 합니다. 여기에 사용된 Gliffy 다이어그램의 이름은 '{giffyName}' 입니다.)";
					break;
				case "attachments":
					var aUpload = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "upload");

					newTag = acMacroTag.OwnerDocument.CreateElement("pre");
					newTag.InnerHtml = @$"(To Do: Attachments 요소는 지원되지 않습니다. 아래는 추후 작업을 위한 데이터입니다.)

- upload: {aUpload}
";
					break;
				default:
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = $"(Note: 지원되지 않는 위젯입니다. 위젯 종류는 {name} 입니다.)";
					break;
			}

			if (newTag != null)
			{
				acMacroTag.ParentNode.InsertAfter(newTag, acMacroTag);
				acMacroTag.Remove();
			}
		}

		static void ReplaceAttachedStructuredMacroTag(HtmlNode acMacroTag, ItemCollection<AttachmentNode> attachments)
		{
			var name = acMacroTag.Attributes["ac:name"]?.Value;
			var newTag = default(HtmlNode);

			var title = default(string);
			var body = default(string);
			var colour = default(string);
			var fileName = default(string);
			var versionAtSave = default(int?);
			var url = default(string);
			var linkBody = default(string);

			// https://en.wikipedia.org/wiki/Audio_file_format
			var CommonAudioFileExtensions = new string[] { ".3gp", ".aa", ".aac", ".aax", ".act", ".aiff", ".alac", ".amr", ".ape", ".au", ".awb", ".dss", ".dvf", ".flac", ".gsm", ".iklax", ".ivs", ".m4a", ".m4b", ".m4p", ".mmf", ".mp3", ".mpc", ".msv", ".nmf", ".ogg", ".oga", ".mogg", ".opus", ".ra", ".rm", ".raw", ".rf64", ".sln", ".tta", ".voc", ".vox", ".wav", ".wma", ".wv", ".webm", ".8svx", ".cda", };

			// https://en.wikipedia.org/wiki/Video_file_format
			var CommonVideoFileExtensions = new string[] { ".webm", ".mkv", ".flv", ".flv", ".vob", ".ogv", ".ogg", ".drc", ".gif", ".gifv", ".mng", ".avi", ".mts", ".m2ts", ".ts", ".mov", ".qt", ".wmv", ".yuv", ".rm", ".rmvb", ".viv", ".asf", ".amv", ".mp4", ".m4p", ".m4v", ".mpg", ".mp2", ".mpeg", ".mpe", ".mpv", ".mpg", ".mpeg", ".m2v", ".m4v", ".svi", ".3gp", ".3g2", ".mxf", ".roq", ".nsv", ".flv", ".f4v", ".f4p", ".f4a", ".f4b", };

			switch (name)
			{
				case "expand":
				case "panel":
					title = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "title");
					body = HtmlNodeExtractor.ExtractRichTextBody(acMacroTag);
					newTag = acMacroTag.OwnerDocument.CreateElement("details");
					newTag.InnerHtml = $"<summary>{title}</summary>{body}";
					break;
				case "toc":
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = "(To Do: 이곳에 목차를 추가합니다.)";
					break;
				case "info":
					body = HtmlNodeExtractor.ExtractRichTextBody(acMacroTag);
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = $"<span>ℹ<span>{body}";
					break;
				case "warning":
					body = HtmlNodeExtractor.ExtractRichTextBody(acMacroTag);
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = $"<span>⚠️<span>{body}";
					break;
				case "tip":
					body = HtmlNodeExtractor.ExtractRichTextBody(acMacroTag);
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = $"<span>💡️<span>{body}";
					break;
				case "note":
					body = HtmlNodeExtractor.ExtractRichTextBody(acMacroTag);
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = $"<span>📝<span>{body}";
					break;
				case "status":
					title = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "title");
					colour = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "colour");
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = $"<span style=\"color: {colour};\">{title}</span>";
					break;
				case "recently-updated":
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = "(Note: 최근에 업데이트 된 글 목록 표시 위젯은 다른 위키에서는 사용할 수 업습니다.)";
					break;
				case "children":
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = "(Note: 하위 페이지 표시 위젯은 다른 위키에서는 사용할 수 없습니다.)";
					break;
				case "contributors":
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = "(Note: 기여자 목록 표시 위젯은 다른 위키에서는 사용할 수 없습니다.)";
					break;
				case "pagetree":
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = "(Note: 페이지 트리 표시 위젯은 다른 위키에서는 사용할 수 없습니다.)";
					break;
				case "viewpdf":
				case "view-file":
				case "multimedia":
					if (HtmlNodeExtractor.ExtractAttachment(acMacroTag, attachments, out fileName, out versionAtSave, out linkBody))
					{
						if (CommonVideoFileExtensions.Any(x => fileName.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
						{
							newTag = acMacroTag.OwnerDocument.CreateElement("video");
							newTag.SetAttributeValue("src", fileName);
						}
						else if (CommonAudioFileExtensions.Any(x => fileName.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
						{
							newTag = acMacroTag.OwnerDocument.CreateElement("audio");
							newTag.SetAttributeValue("src", fileName);
						}
						else
						{
							newTag = acMacroTag.OwnerDocument.CreateElement("a");
							newTag.SetAttributeValue("href", fileName);
							if (linkBody != null)
								newTag.InnerHtml = linkBody;
							else
								newTag.InnerHtml = fileName;
						}
					}
					break;
				case "widget":
					url = HtmlNodeExtractor.ExtractUrl(acMacroTag);
					newTag = acMacroTag.OwnerDocument.CreateElement("iframe");
					newTag.SetAttributeValue("width", "420");
					newTag.SetAttributeValue("height", "315");
					newTag.SetAttributeValue("src", url);
					break;
				case "gallery":
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = "(To Do: 갤러리는 수동으로 추가해야 합니다.)";
					break;
				case "gliffy":
					var giffyName = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "name");
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = $"(To Do: Gliffy 다이어그램은 직접 추가해야 합니다. 여기에 사용된 Gliffy 다이어그램의 이름은 '{giffyName}' 입니다.)";
					break;
				case "roadmap":
					var roadmapMapLinks = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "maplinks");
					var roadmapTimeline = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "timeline");
					var roadmapPagelinks = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "pagelinks");
					var roadmapSource = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "source");
					var roadmapTitle = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "title");
					var roadmapHash = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "hash");

					newTag = acMacroTag.OwnerDocument.CreateElement("pre");
					newTag.InnerHtml = @$"(To Do: Roadmap 요소는 지원되지 않습니다. 아래는 추후 작업을 위한 데이터입니다.)

- maplinks: {roadmapMapLinks}
- timeline: {roadmapTimeline}
- pagelinks: {roadmapPagelinks}
- source: {roadmapSource}
- title: {roadmapTitle}
- hash: {roadmapHash}
";
					break;
				case "code":
					body = HtmlNodeExtractor.ExtractPlainTextBody(acMacroTag);
					newTag = acMacroTag.OwnerDocument.CreateElement("pre");
					newTag.InnerHtml = body;
					break;
				case "attachments":
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = "(Note: 첨부 파일이 어떤 것인지 찾지 못했습니다.)";
					break;
				case "excerpt":
					body = HtmlNodeExtractor.ExtractRichTextBody(acMacroTag);
					newTag = acMacroTag.OwnerDocument.CreateElement("blockquote");
					newTag.InnerHtml = body;
					break;
				case "google-drive-sheets":
				case "google-drive-slides":
				case "google-drive-docs":
					url = HtmlNodeExtractor.ExtractParameterTag(acMacroTag, "url");
					newTag = acMacroTag.OwnerDocument.CreateElement("a");
					newTag.SetAttributeValue("href", url);
					newTag.SetAttributeValue("target", "_blank");
					break;
				default:
					newTag = acMacroTag.OwnerDocument.CreateElement("p");
					newTag.InnerHtml = $"(Note: 지원되지 않는 위젯입니다. 위젯 종류는 {name} 입니다.)";
					break;
			}

			if (newTag != null)
			{
				acMacroTag.ParentNode.InsertAfter(newTag, acMacroTag);
				acMacroTag.Remove();
			}
		}

		static void ReplaceAttachedLayoutTag(HtmlNode acLayoutTag)
		{
			var acLayoutCellTags = acLayoutTag.SelectNodes(".//*[name() = 'ac:layout-cell']");
			foreach (var eachAcLayoutCellTag in acLayoutCellTags)
			{
				var newDivTag = acLayoutTag.OwnerDocument.CreateElement("div");
				newDivTag.InnerHtml = eachAcLayoutCellTag.InnerHtml;
				eachAcLayoutCellTag.ParentNode.InsertAfter(newDivTag, eachAcLayoutCellTag);
				eachAcLayoutCellTag.Remove();
			}

			var acLayoutSectionTags = acLayoutTag.SelectNodes(".//*[name() = 'ac:layout-section']");
			foreach (var eachAcLayoutSectionTag in acLayoutSectionTags)
			{
				var newDivTag = acLayoutTag.OwnerDocument.CreateElement("p");
				newDivTag.InnerHtml = $"(To Do: 레이아웃 구성은 수동으로 다시 지정해야 합니다 - {eachAcLayoutSectionTag.Attributes["ac:type"]?.Value})";
				eachAcLayoutSectionTag.ParentNode.InsertAfter(newDivTag, eachAcLayoutSectionTag);
				eachAcLayoutSectionTag.ParentNode.RemoveChild(eachAcLayoutSectionTag, true);
			}

			var parentNode = acLayoutTag.ParentNode;
			parentNode.RemoveChild(acLayoutTag, true);
		}

		static void ReplaceAttachedInlineCommentMarkerTag(HtmlNode acInlineCommentMarkerTag)
		{
			var parentNode = acInlineCommentMarkerTag.ParentNode;
			parentNode.RemoveChild(acInlineCommentMarkerTag, true);
		}
	}
}
