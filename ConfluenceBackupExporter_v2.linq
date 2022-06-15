<Query Kind="Statements">
  <NuGetReference>HtmlAgilityPack</NuGetReference>
  <Namespace>HtmlAgilityPack</Namespace>
  <Namespace>System.Collections.ObjectModel</Namespace>
</Query>

// See https://aka.ms/new-console-template for more information
using System.Xml.Serialization;

var sourceDirectory = @"C:\Users\rkttu\Desktop\Confluence-export-space-gb";
var outputDirectory = @"C:\Users\rkttu\Desktop\Confluence-export-space-gb-converted";

var outputEncoding = new UTF8Encoding(false);
var serializer = new XmlSerializer(typeof(HibernateGeneric));
var content = (HibernateGeneric)serializer.Deserialize(File.OpenRead(Path.Combine(sourceDirectory, "entities.xml")));
var confluenceObjects = content.GetConfluenceObjects();

if (!Directory.Exists(outputDirectory))
	Directory.CreateDirectory(outputDirectory);

var spaces = confluenceObjects.GetSpaces();
foreach (var eachSpace in spaces)
{
	var allPages = confluenceObjects.GetPages().ExtractLatestVersionsOnly().ToCollection().Where(x => object.ReferenceEquals(x.space, eachSpace));
	var allAttachments = confluenceObjects.GetAttachments().ExtractLatestVersionsOnly().ToCollection().Where(x => object.ReferenceEquals(x.space, eachSpace));
	var sourceAttachmentDirectory = Path.Combine(sourceDirectory, "attachments");

	// 대상 page 확정
	// 최대 chunk가 2.5GiB이므로 1.2GiB ~2.4GiB로 attachment size가 잡히는 page를 선택
	// 해당 page들을 detach한 후 multiple root page로 취급
	
	var chunks = allPages
		.Select(x => new { page = x, title = x.title, parent_title = x.parent?.title, has_child = x.children.Count > 0, size = (double)x.GetTotalAttachmentSize(sourceAttachmentDirectory, true) / (1024 * 1024 * 1024), depth = x.GetDepth(), })
		.Where(x => 0.3 < x.size)
		.OrderBy(x => x.depth)
		.ThenByDescending(x => x.size)
		//.Dump()
		;
	
	foreach (var eachCandidate in chunks)
		eachCandidate.page.DetachFromParent();

	chunks = allPages
		.Where(x => x.parent == null)
		.Select(x => new { page = x, title = x.title, parent_title = x.parent?.title, has_child = x.children.Count > 0, size = (double)x.GetTotalAttachmentSize(sourceAttachmentDirectory, true) / (1024 * 1024 * 1024), depth = x.GetDepth(), })
		.OrderBy(x => x.depth)
		.ThenByDescending(x => x.size)
		.Dump()
		;
	
	foreach (var eachChunk in chunks)
	{
		var safeTitle = string.Join("_", eachChunk.title.Split(Path.GetInvalidFileNameChars()));
		var eachOutputDirectory = Path.Combine(outputDirectory, eachSpace.key + "_" + safeTitle, eachSpace.key);
		if (!Directory.Exists(eachOutputDirectory))
			Directory.CreateDirectory(eachOutputDirectory);

		var stylesDirectory = Path.Combine(eachOutputDirectory, "styles");
		if (!Directory.Exists(stylesDirectory))
			Directory.CreateDirectory(stylesDirectory);

		File.WriteAllBytes(Path.Combine(stylesDirectory, "site.css"), HtmlRenderers.SiteCssContents);

		var iconsDirectory = Path.Combine(eachOutputDirectory, "images", "icons");
		if (!Directory.Exists(iconsDirectory))
			Directory.CreateDirectory(iconsDirectory);

		File.WriteAllBytes(Path.Combine(iconsDirectory, "bullet_blue.gif"), HtmlRenderers.BulletBlueGifContents);
		File.WriteAllBytes(Path.Combine(iconsDirectory, "wait.gif"), HtmlRenderers.WaitGifContents);

		var emoticonsDirectory = Path.Combine(iconsDirectory, "emoticons");
		if (!Directory.Exists(emoticonsDirectory))
			Directory.CreateDirectory(emoticonsDirectory);

		File.WriteAllBytes(Path.Combine(emoticonsDirectory, "smile.png"), HtmlRenderers.SmilePngContents);

		var contenttypesDirectory = Path.Combine(iconsDirectory, "contenttypes");
		if (!Directory.Exists(contenttypesDirectory))
			Directory.CreateDirectory(contenttypesDirectory);

		File.WriteAllBytes(Path.Combine(contenttypesDirectory, "home_page_16.png"), HtmlRenderers.HomePage16PngContents);
		
		var subPages = eachChunk.page.CollectPages();
		var subPageAttachments = allAttachments.Where(x => subPages.Contains(x.containerContent)).GroupBy(x => x.containerContent);
		var subPageContents = confluenceObjects.GetBodyContents().Where(x => subPages.Contains(x.content as Page)).ToCollection();
		
		File.WriteAllText(Path.Combine(eachOutputDirectory, "index.html"), HtmlRenderers.RenderIndexPage(eachSpace, subPages, chunks.Count() > 1), outputEncoding);

		foreach (var eachPageContents in subPageContents)
		{
			var relatedAttachments = subPageAttachments.Where(x => object.ReferenceEquals(eachPageContents.content, x.Key));
			var renderedContents = HtmlRenderers.RenderPage(eachSpace, (Page)eachPageContents.content, eachPageContents, relatedAttachments.SelectMany(x => x), subPages);
			File.WriteAllText(Path.Combine(eachOutputDirectory, $"{eachPageContents.contentId}.html"), renderedContents, outputEncoding);

			var targetAttachmentDirectory = Path.Combine(eachOutputDirectory, "attachments", eachPageContents.contentId);
			if (!Directory.Exists(targetAttachmentDirectory))
				Directory.CreateDirectory(targetAttachmentDirectory);

			foreach (var eachAttachment in relatedAttachments.SelectMany(x => x))
			{
				var destFileName = $"{eachAttachment.hibernateId}{Path.GetExtension(eachAttachment.safeFileName)}";
				var srcPath = Path.Combine(sourceAttachmentDirectory, eachAttachment.containerContentId, eachAttachment.hibernateId, eachAttachment.version.ToString());
				if (!File.Exists(srcPath))
				{
					$"{srcPath} does not exists.".Dump("Warning");
					continue;
				}
				File.Copy(
					srcPath,
					Path.Combine(targetAttachmentDirectory, destFileName),
					true);
			}
		}
	}
}

/*
var pages = confluenceObjects.GetPages().ExtractLatestVersionsOnly().Where(x => x.spaceId != null);
var pageContents = confluenceObjects.GetBodyContents().Where(x => pages.Contains(x.content as Page));
var attachments = confluenceObjects.GetAttachments().ExtractLatestVersionsOnly().Where(x => x.spaceId != null);
var pageAttachments = attachments.Where(x => pages.Contains(x.containerContent)).GroupBy(x => x.containerContent);
var customContentEntityObjects = confluenceObjects.GetCustomContentEntityObjects().ExtractLatestVersionsOnly().Where(x => x.spaceId != null);
var outgoingLinks = confluenceObjects.GetOutgoingLinks().Where(x => pages.Contains(x.sourceContent)).GroupBy(x => x.sourceContent);
var referralLinks = confluenceObjects.GetReferralLinks().Where(x => pages.Contains(x.sourceContent)).GroupBy(x => x.sourceContent);
*/

/* ContentMeasurer */

public static class ContentMeasurer
{
	public static IEnumerable<Page> CollectPages(this Page targetPage)
	{
		var list = new List<Page>();
		list.Add(targetPage);
		
		foreach (var eachChild in targetPage.children)
			list.AddRange(CollectPages(eachChild));
			
		return list;
	}
	
	public static int GetDepth(this Page targetPage)
	{
		var count = 0;
		var parentPage = targetPage?.parent;
		do
		{
			count++;
			parentPage = parentPage?.parent;
		}
		while (parentPage != null);
		return count;
	}
	
	public static long GetTotalAttachmentSize(this Page targetPage, string sourceAttachmentsDirectory, bool includeChildPages = false)
	{
		var acc = 0L;
		foreach (var eachAttachment in targetPage.attachments)
		{
			var ext = Path.GetExtension(eachAttachment.title);
			var targetPath = Path.Combine(sourceAttachmentsDirectory, eachAttachment.containerContentId, eachAttachment.hibernateId, eachAttachment.version.ToString());
			var fileInfo = new FileInfo(targetPath);

			if (fileInfo.Exists)
				acc += fileInfo.Length;
		}

		if (includeChildPages)
		{
			foreach (var eachChildPage in targetPage.children)
				acc += GetTotalAttachmentSize(eachChildPage, sourceAttachmentsDirectory, includeChildPages);
		}
		
		return acc;
	}
}

/* HtmlExport */

public static class HtmlProcessor
{
	public static string NormalizeBodyContent(string htmlFragment, IEnumerable<Attachment> attachments, IEnumerable<Page> pages)
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

	static void ReplaceAttachedImageTag(HtmlNode acImageTagNode, IEnumerable<Attachment> attachments)
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
			var fileName = riAttachmentTag.Attributes["ri:filename"]?.Value;
			if (fileName != null)
				fileName = fileName.Normalize();

			var att = attachments.Where(x => string.Equals(x.title, fileName, StringComparison.Ordinal)).FirstOrDefault();
			if (att == null)
				fileName = string.Concat("Missing_", fileName);
			else
				fileName = $"attachments/{att.containerContentId}/{att.hibernateId}{Path.GetExtension(att.title)}";

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
				var contentType = att.contentProperties.Where(x => string.Equals("MEDIA_TYPE", x.name, StringComparison.Ordinal)).Select(x => x.stringValue).FirstOrDefault();
				newImgTag.SetAttributeValue("src", Convert.ToString(fileName));

				newImgTag.SetAttributeValue("alt", Convert.ToString(fileName));
				newImgTag.SetAttributeValue("class", "confluence-embedded-image");
				newImgTag.SetAttributeValue("loading", "lazy");
				newImgTag.SetAttributeValue("data-image-src", Convert.ToString(fileName));

				newImgTag.SetAttributeValue("data-linked-resource-id", att.hibernateId);
				newImgTag.SetAttributeValue("data-linked-resource-version", att.version.ToString());
				newImgTag.SetAttributeValue("data-linked-resource-type", "attachment");
				newImgTag.SetAttributeValue("data-linked-resource-default-alias", att.title);
				newImgTag.SetAttributeValue("data-linked-resource-content-type", contentType);
				newImgTag.SetAttributeValue("data-linked-resource-container-id", att.containerContentId);
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

	static void ReplaceAttachedLinkTag(HtmlNode acLinkTagNode, IEnumerable<Attachment> attachments, IEnumerable<Page> pages)
	{
		var riAttachmentTag = acLinkTagNode.SelectNodes(".//*[name() = 'ri:attachment']")?.FirstOrDefault();
		if (riAttachmentTag != null)
		{
			var fileName = riAttachmentTag.Attributes["ri:filename"]?.Value;
			if (fileName != null)
				fileName = fileName.Normalize();

			var att = attachments.Where(x => string.Equals(x.title, fileName, StringComparison.Ordinal)).FirstOrDefault();
			if (att == null)
				fileName = string.Concat("Missing_", fileName);
			else
				fileName = $"attachments/{att.containerContentId}/{att.hibernateId}{Path.GetExtension(att.title)}";

			var newAnchorTag = acLinkTagNode.OwnerDocument.CreateElement("a");
			newAnchorTag.SetAttributeValue("href", Convert.ToString(fileName));
			acLinkTagNode.ParentNode.InsertAfter(newAnchorTag, acLinkTagNode);
			acLinkTagNode.Remove();
			return;
		}

		var riPageTag = acLinkTagNode.SelectNodes(".//*[name() = 'ri:page']")?.FirstOrDefault();
		if (riPageTag != null)
		{
			var contentTitle = riPageTag.Attributes["ri:content-title"]?.Value;
			if (contentTitle != null)
				contentTitle = contentTitle.Normalize();

			var page = pages.Where(x => string.Equals(x.title, contentTitle, StringComparison.Ordinal)).FirstOrDefault();
			var href = string.IsNullOrWhiteSpace(page?.hibernateId) ? "missing" : page?.hibernateId;
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

	static void ReplaceAttachedMacroTag(HtmlNode acMacroTag, IEnumerable<Attachment> attachments)
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

	static void ReplaceAttachedStructuredMacroTag(HtmlNode acMacroTag, IEnumerable<Attachment> attachments)
	{
		var name = acMacroTag.Attributes["ac:name"]?.Value;
		var newTag = default(HtmlNode);

		var title = default(string);
		var body = default(string);
		var colour = default(string);
		var fileName = default(string);
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
				if (HtmlNodeExtractor.ExtractAttachment(acMacroTag, attachments, out fileName, out linkBody))
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

public static class HtmlNodeExtractor
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

	public static bool ExtractAttachment(HtmlNode acTag, IEnumerable<Attachment> attachments, out string fileName, out string linkBody)
	{
		fileName = null;
		linkBody = null;

		var riAttachmentTag = acTag.SelectNodes(".//*[name() = 'ri:attachment']")?.FirstOrDefault();
		if (riAttachmentTag != null)
		{
			fileName = riAttachmentTag.Attributes["ri:filename"]?.Value;
			if (fileName != null)
				fileName = fileName.Normalize();

			var fileNameAttr = fileName;
			var att = attachments.Where(x => string.Equals(x.title, fileNameAttr, StringComparison.Ordinal)).FirstOrDefault();
			if (att == null)
				fileName = string.Concat("Missing_", fileName);
			else
				fileName = $"attachments/{att.containerContentId}/{att.hibernateId}{Path.GetExtension(att.title)}";

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

public static class HtmlRenderers
{
	public static readonly byte[] SiteCssContents = Convert.FromBase64String(@"CgpAaW1wb3J0ICcuL2ltcG9ydHMvZ2xvYmFsJzsKCi8qKgogKiBSRVNFVAogKi8KaHRtbCwgYm9keSwgcCwgZGl2LCBoMSwgaDIsIGgzLCBoNCwgaDUsIGg2LCBpbWcsIHByZSwgZm9ybSwgZmllbGRzZXQgewogICAgbWFyZ2luOiAwOwogICAgcGFkZGluZzogMDsKfQp1bCwgb2wsIGRsIHsKICAgIG1hcmdpbjogMDsKfQppbWcsIGZpZWxkc2V0IHsKICAgIGJvcmRlcjogMDsKfQpALW1vei1kb2N1bWVudCB1cmwtcHJlZml4KCkgewogICAgaW1nIHsKICAgICAgICBmb250LXNpemU6IDA7CiAgICB9CiAgICBpbWc6LW1vei1icm9rZW4gewogICAgICAgIGZvbnQtc2l6ZTogaW5oZXJpdDsKICAgIH0KfQoKLyogaHR0cHM6Ly9naXRodWIuY29tL25lY29sYXMvbm9ybWFsaXplLmNzcyAqLwovKiBDdXN0b21pc2VkIHRvIHJlbW92ZSBzdHlsZXMgZm9yIHVuc3VwcG9ydGVkIGJyb3dzZXJzICovCi8vIEhUTUw1IGRpc3BsYXkgZGVmaW5pdGlvbnMKLy8gPT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT09PT0KLy8gQ29ycmVjdCBgYmxvY2tgIGRpc3BsYXkgbm90IGRlZmluZWQgZm9yIGBkZXRhaWxzYCBvciBgc3VtbWFyeWAgaW4gSUUgOS8xMC8xMSBhbmQgRmlyZWZveC4KLy8gQ29ycmVjdCBgYmxvY2tgIGRpc3BsYXkgbm90IGRlZmluZWQgZm9yIGBtYWluYCBpbiBJRSA5LzEwLzExLgpkZXRhaWxzLAptYWluLApzdW1tYXJ5IHsKICAgIGRpc3BsYXk6IGJsb2NrOwp9CgovLyAxLiBDb3JyZWN0IGBpbmxpbmUtYmxvY2tgIGRpc3BsYXkgbm90IGRlZmluZWQgaW4gSUUgOS4KLy8gMi4gTm9ybWFsaXplIHZlcnRpY2FsIGFsaWdubWVudCBvZiBgcHJvZ3Jlc3NgIGluIENocm9tZSwgRmlyZWZveCwgYW5kIE9wZXJhLgphdWRpbywKY2FudmFzLApwcm9ncmVzcywKdmlkZW8gewogICAgZGlzcGxheTogaW5saW5lLWJsb2NrOyAvLyAxCiAgICB2ZXJ0aWNhbC1hbGlnbjogYmFzZWxpbmU7IC8vIDIKfQoKLy8gUHJldmVudCBtb2Rlcm4gYnJvd3NlcnMgZnJvbSBkaXNwbGF5aW5nIGBhdWRpb2Agd2l0aG91dCBjb250cm9scy4KLy8gUmVtb3ZlIGV4Y2VzcyBoZWlnaHQgaW4gaU9TIDUgZGV2aWNlcy4KYXVkaW86bm90KFtjb250cm9sc10pIHsKICAgIGRpc3BsYXk6IG5vbmU7CiAgICBoZWlnaHQ6IDA7Cn0KCi8vIEFkZHJlc3MgYFtoaWRkZW5dYCBzdHlsaW5nIG5vdCBwcmVzZW50IGluIElFIDgvOS8xMC4KLy8gSGlkZSB0aGUgYHRlbXBsYXRlYCBlbGVtZW50IGluIElFIDgvOS8xMSwgU2FmYXJpLCBhbmQgRmlyZWZveCA8IDIyLgpbaGlkZGVuXSwKdGVtcGxhdGUgewogICAgZGlzcGxheTogbm9uZTsKfQoKLy8gUHJldmVudCBpT1MgZGVmYXVsdGluZyB0byBwdXNoLWJ1dHRvbiB3aGljaCBpZ25vcmVzIG1hbnkgc3R5bGVzIHVubGVzcyBhIGJnIGltYWdlIGlzIHNldAppbnB1dFt0eXBlPSJidXR0b24iXSwKaW5wdXRbdHlwZT0ic3VibWl0Il0sCmlucHV0W3R5cGU9InJlc2V0Il0gewogICAgLXdlYmtpdC1hcHBlYXJhbmNlOiBidXR0b247Cn0KCgpAaW1wb3J0ICcuL2ltcG9ydHMvZ2xvYmFsJzsKCi8qKgogKiBUWVBPR1JBUEhZIC0gMTRweCBiYXNlIGZvbnQgc2l6ZSwgYWdub3N0aWMgZm9udCBzdGFjawogKi8KYm9keSB7CiAgICBjb2xvcjogQGF1aS10ZXh0LWNvbG9yOwogICAgZm9udC1mYW1pbHk6IEBhdWktZm9udC1mYW1pbHk7CiAgICBmb250LXNpemU6IEBhdWktZm9udC1zaXplLW1lZGl1bTsKICAgIGxpbmUtaGVpZ2h0OiAxLjQyODU3MTQyODU3MTQzOwp9CgovKiBJbnRlcm5hdGlvbmFsIEZvbnQgU3RhY2tzKi8KW2xhbmd8PWVuXSB7CiAgICBmb250LWZhbWlseTogQGF1aS1mb250LWZhbWlseTsKfQoKW2xhbmd8PWphXSB7CiAgICBmb250LWZhbWlseTogQGF1aS1mb250LWZhbWlseS1qYTsKfQoKLyogRGVmYXVsdCBtYXJnaW5zICovCnAsCnVsLApvbCwKZGwsCmgxLApoMiwKaDMsCmg0LApoNSwKaDYsCmJsb2NrcXVvdGUsCnByZSwKZm9ybS5hdWksCnRhYmxlLmF1aSwKLmF1aS10YWJzLAouYXVpLXBhbmVsLAouYXVpLWdyb3VwIHsKICAgIG1hcmdpbjogQGF1aS1ncmlkIDAgMCAwOwp9CgovKiBObyB0b3AgbWFyZ2luIHRvIGludGVyZmVyZSB3aXRoIGJveCBwYWRkaW5nICovCnA6Zmlyc3QtY2hpbGQsCnVsOmZpcnN0LWNoaWxkLApvbDpmaXJzdC1jaGlsZCwKZGw6Zmlyc3QtY2hpbGQsCmgxOmZpcnN0LWNoaWxkLApoMjpmaXJzdC1jaGlsZCwKaDM6Zmlyc3QtY2hpbGQsCmg0OmZpcnN0LWNoaWxkLApoNTpmaXJzdC1jaGlsZCwKaDY6Zmlyc3QtY2hpbGQsCmJsb2NrcXVvdGU6Zmlyc3QtY2hpbGQsCnByZTpmaXJzdC1jaGlsZCwKZm9ybS5hdWk6Zmlyc3QtY2hpbGQsCnRhYmxlLmF1aTpmaXJzdC1jaGlsZCwKLmF1aS10YWJzOmZpcnN0LWNoaWxkLAouYXVpLXBhbmVsOmZpcnN0LWNoaWxkLAouYXVpLWdyb3VwOmZpcnN0LWNoaWxkIHsKICAgIG1hcmdpbi10b3A6IDA7Cn0KCi8qIEhlYWRpbmdzOiBkZXNpcmVkIGxpbmUgaGVpZ2h0IGluIHB4IC8gZm9udCBzaXplID0gdW5pdGxlc3MgbGluZSBoZWlnaHQgKi8KaDEsCi5hdWktcGFnZS1oZWFkZXItaGVybyAuYXVpLXBhZ2UtaGVhZGVyLW1haW4gaDEsIC8qIC5hdWktcGFnZS1oZWFkZXItaGVybyBhbmQgLmF1aS1wYWdlLWhlYWRlci1tYXJrZXRpbmcgaGF2ZSBiZWVuIERFUFJFQ0FURUQgaW4gNS43ICovCi5hdWktcGFnZS1oZWFkZXItaGVybyAuYXVpLXBhZ2UtaGVhZGVyLW1haW4gaDIsCi5hdWktcGFnZS1oZWFkZXItbWFya2V0aW5nIC5hdWktcGFnZS1oZWFkZXItbWFpbiBoMSwKLmF1aS1wYWdlLWhlYWRlci1tYXJrZXRpbmcgLmF1aS1wYWdlLWhlYWRlci1tYWluIGgyIHsKICAgIGNvbG9yOiBAYXVpLWhlYWRpbmcteHhsYXJnZS10ZXh0LWNvbG9yOwogICAgZm9udC1zaXplOiBAYXVpLWhlYWRpbmcteHhsYXJnZS1mb250LXNpemU7CiAgICBmb250LXdlaWdodDogQGF1aS1oZWFkaW5nLXh4bGFyZ2UtZm9udC13ZWlnaHQ7CiAgICBsaW5lLWhlaWdodDogQGF1aS1oZWFkaW5nLXh4bGFyZ2UtbGluZS1oZWlnaHQ7CiAgICB0ZXh0LXRyYW5zZm9ybTogQGF1aS1oZWFkaW5nLXh4bGFyZ2UtdGV4dC10cmFuc2Zvcm07CiAgICBtYXJnaW46IEBhdWktaGVhZGluZy14eGxhcmdlLW1hcmdpbi10b3AgMCAwIDA7Cn0KaDIgewogICAgY29sb3I6IEBhdWktaGVhZGluZy14bGFyZ2UtdGV4dC1jb2xvcjsKICAgIGZvbnQtc2l6ZTogQGF1aS1oZWFkaW5nLXhsYXJnZS1mb250LXNpemU7CiAgICBmb250LXdlaWdodDogQGF1aS1oZWFkaW5nLXhsYXJnZS1mb250LXdlaWdodDsKICAgIGxpbmUtaGVpZ2h0OiBAYXVpLWhlYWRpbmcteGxhcmdlLWxpbmUtaGVpZ2h0OwogICAgdGV4dC10cmFuc2Zvcm06IEBhdWktaGVhZGluZy14bGFyZ2UtdGV4dC10cmFuc2Zvcm07CiAgICBtYXJnaW46IEBhdWktaGVhZGluZy14bGFyZ2UtbWFyZ2luLXRvcCAwIDAgMDsKfQpoMywKLmF1aS1wYWdlLWhlYWRlci1oZXJvIC5hdWktcGFnZS1oZWFkZXItbWFpbiBwLCAvKiAuYXVpLXBhZ2UtaGVhZGVyLWhlcm8gYW5kIC5hdWktcGFnZS1oZWFkZXItbWFya2V0aW5nIGhhdmUgYmVlbiBERVBSRUNBVEVEIGluIDUuNyAqLwouYXVpLXBhZ2UtaGVhZGVyLW1hcmtldGluZyAuYXVpLXBhZ2UtaGVhZGVyLW1haW4gcCB7CiAgICBjb2xvcjogQGF1aS1oZWFkaW5nLWxhcmdlLXRleHQtY29sb3I7CiAgICBmb250LXNpemU6IEBhdWktaGVhZGluZy1sYXJnZS1mb250LXNpemU7CiAgICBmb250LXdlaWdodDogQGF1aS1oZWFkaW5nLWxhcmdlLWZvbnQtd2VpZ2h0OwogICAgbGluZS1oZWlnaHQ6IEBhdWktaGVhZGluZy1sYXJnZS1saW5lLWhlaWdodDsKICAgIHRleHQtdHJhbnNmb3JtOiBAYXVpLWhlYWRpbmctbGFyZ2UtdGV4dC10cmFuc2Zvcm07CiAgICBtYXJnaW46IEBhdWktaGVhZGluZy1sYXJnZS1tYXJnaW4tdG9wIDAgMCAwOwp9Cmg0IHsKICAgIGNvbG9yOiBAYXVpLWhlYWRpbmctbWVkaXVtLXRleHQtY29sb3I7CiAgICBmb250LXNpemU6IEBhdWktaGVhZGluZy1tZWRpdW0tZm9udC1zaXplOwogICAgZm9udC13ZWlnaHQ6IEBhdWktaGVhZGluZy1tZWRpdW0tZm9udC13ZWlnaHQ7CiAgICBsaW5lLWhlaWdodDogQGF1aS1oZWFkaW5nLW1lZGl1bS1saW5lLWhlaWdodDsKICAgIHRleHQtdHJhbnNmb3JtOiBAYXVpLWhlYWRpbmctbWVkaXVtLXRleHQtdHJhbnNmb3JtOwogICAgbWFyZ2luOiBAYXVpLWhlYWRpbmctbWVkaXVtLW1hcmdpbi10b3AgMCAwIDA7Cn0KaDUgewogICAgY29sb3I6IEBhdWktaGVhZGluZy1zbWFsbC10ZXh0LWNvbG9yOwogICAgZm9udC1zaXplOiBAYXVpLWhlYWRpbmctc21hbGwtZm9udC1zaXplOwogICAgZm9udC13ZWlnaHQ6IEBhdWktaGVhZGluZy1zbWFsbC1mb250LXdlaWdodDsKICAgIGxpbmUtaGVpZ2h0OiBAYXVpLWhlYWRpbmctc21hbGwtbGluZS1oZWlnaHQ7CiAgICB0ZXh0LXRyYW5zZm9ybTogQGF1aS1oZWFkaW5nLXNtYWxsLXRleHQtdHJhbnNmb3JtOwogICAgbWFyZ2luOiBAYXVpLWhlYWRpbmctc21hbGwtbWFyZ2luLXRvcCAwIDAgMDsKfQpoNiB7CiAgICBjb2xvcjogQGF1aS1oZWFkaW5nLXhzbWFsbC10ZXh0LWNvbG9yOwogICAgZm9udC1zaXplOiBAYXVpLWhlYWRpbmcteHNtYWxsLWZvbnQtc2l6ZTsKICAgIGZvbnQtd2VpZ2h0OiBAYXVpLWhlYWRpbmcteHNtYWxsLWZvbnQtd2VpZ2h0OwogICAgbGluZS1oZWlnaHQ6IEBhdWktaGVhZGluZy14c21hbGwtbGluZS1oZWlnaHQ7CiAgICB0ZXh0LXRyYW5zZm9ybTogQGF1aS1oZWFkaW5nLXhzbWFsbC10ZXh0LXRyYW5zZm9ybTsKICAgIG1hcmdpbjogQGF1aS1oZWFkaW5nLXhzbWFsbC1tYXJnaW4tdG9wIDAgMCAwOwp9CmgxOmZpcnN0LWNoaWxkLApoMjpmaXJzdC1jaGlsZCwKaDM6Zmlyc3QtY2hpbGQsCmg0OmZpcnN0LWNoaWxkLApoNTpmaXJzdC1jaGlsZCwKaDY6Zmlyc3QtY2hpbGQgewogICAgbWFyZ2luLXRvcDogMDsKfQovKiBOaWNlIHN0eWxlcyBmb3IgdXNpbmcgc3ViaGVhZGluZ3MgKi8KaDEgKyBoMiwKaDIgKyBoMywKaDMgKyBoNCwKaDQgKyBoNSwKaDUgKyBoNiB7CiAgICBtYXJnaW4tdG9wOiBAYXVpLWdyaWQ7Cn0KLyogSW5jcmVhc2UgdGhlIG1hcmdpbnMgb24gYWxsIGhlYWRpbmdzIHdoZW4gdXNlZCBpbiB0aGUgZ3JvdXAvaXRlbSBwYXR0ZXJuIC4uLiAqLwouYXVpLWdyb3VwID4gLmF1aS1pdGVtID4gaDE6Zmlyc3QtY2hpbGQsCi5hdWktZ3JvdXAgPiAuYXVpLWl0ZW0gPiBoMjpmaXJzdC1jaGlsZCwKLmF1aS1ncm91cCA+IC5hdWktaXRlbSA+IGgzOmZpcnN0LWNoaWxkLAouYXVpLWdyb3VwID4gLmF1aS1pdGVtID4gaDQ6Zmlyc3QtY2hpbGQsCi5hdWktZ3JvdXAgPiAuYXVpLWl0ZW0gPiBoNTpmaXJzdC1jaGlsZCwKLmF1aS1ncm91cCA+IC5hdWktaXRlbSA+IGg2OmZpcnN0LWNoaWxkIHsKICAgIG1hcmdpbi10b3A6IChAYXVpLWdyaWQgKiAyKTsKfQovKiAuLi4gdW5sZXNzIHRoZXkncmUgdGhlIGZpcnN0LWNoaWxkICovCi5hdWktZ3JvdXA6Zmlyc3QtY2hpbGQgPiAuYXVpLWl0ZW0gPiBoMTpmaXJzdC1jaGlsZCwKLmF1aS1ncm91cDpmaXJzdC1jaGlsZCA+IC5hdWktaXRlbSA+IGgyOmZpcnN0LWNoaWxkLAouYXVpLWdyb3VwOmZpcnN0LWNoaWxkID4gLmF1aS1pdGVtID4gaDM6Zmlyc3QtY2hpbGQsCi5hdWktZ3JvdXA6Zmlyc3QtY2hpbGQgPiAuYXVpLWl0ZW0gPiBoNDpmaXJzdC1jaGlsZCwKLmF1aS1ncm91cDpmaXJzdC1jaGlsZCA+IC5hdWktaXRlbSA+IGg1OmZpcnN0LWNoaWxkLAouYXVpLWdyb3VwOmZpcnN0LWNoaWxkID4gLmF1aS1pdGVtID4gaDY6Zmlyc3QtY2hpbGQgewogICAgbWFyZ2luLXRvcDogMDsKfQoKLyogT3RoZXIgdHlwb2dyYXBoaWNhbCBlbGVtZW50cyAqLwpzbWFsbCB7CiAgICBjb2xvcjogQGF1aS1zbWFsbC10ZXh0LWNvbG9yOwogICAgZm9udC1zaXplOiBAYXVpLWZvbnQtc2l6ZS1zbWFsbDsKICAgIGxpbmUtaGVpZ2h0OiAxLjMzMzMzMzMzMzMzMzMzOwp9CmNvZGUsCmtiZCB7CiAgICBmb250LWZhbWlseTogbW9ub3NwYWNlOwp9CnZhciwKYWRkcmVzcywKZGZuLApjaXRlIHsKICAgIGZvbnQtc3R5bGU6IGl0YWxpYzsKfQpjaXRlOmJlZm9yZSB7CiAgICBjb250ZW50OiAiXDIwMTQgXDIwMDkiOwp9CmJsb2NrcXVvdGUgewogICAgYm9yZGVyLWxlZnQ6IEBhdWktYm9yZGVyLXdpZHRoIEBhdWktYm9yZGVyLXN0eWxlIEBhdWktYm9yZGVyLWNvbG9yOwogICAgY29sb3I6IEBhdWktYmxvY2txdW90ZS10ZXh0LWNvbG9yOwogICAgbWFyZ2luLWxlZnQ6IChAYXVpLWdyaWQgKiAyIC0gMSk7CiAgICBwYWRkaW5nOiBAYXVpLWdyaWQgKEBhdWktZ3JpZCAqIDIpOwp9CmJsb2NrcXVvdGUgPiBjaXRlIHsKICAgIGRpc3BsYXk6IGJsb2NrOwogICAgbWFyZ2luLXRvcDogQGF1aS1ncmlkOwp9CnEgewogICAgY29sb3I6IEBhdWktcXVvdGUtdGV4dC1jb2xvcjsKfQpxOmJlZm9yZSB7CiAgICBjb250ZW50OiBvcGVuLXF1b3RlOwp9CnE6YWZ0ZXIgewogICAgY29udGVudDogY2xvc2UtcXVvdGU7Cn0KYWJiciB7CiAgICBib3JkZXItYm90dG9tOiAxcHggQGF1aS1hYmJyLWJvcmRlci1jb2xvciBkb3R0ZWQ7CiAgICBjdXJzb3I6IGhlbHA7Cn0KCgpAaW1wb3J0ICcuL2ltcG9ydHMvZ2xvYmFsJzsKCi8qKgogKiBQQUdFIExBWU9VVAogKi8KLmF1aS1oZWFkZXIsCiNmb290ZXIgewogICAgY2xlYXI6IGJvdGg7CiAgICBmbG9hdDogbGVmdDsKICAgIHdpZHRoOiAxMDAlOwp9CgojY29udGVudCB7CiAgICBib3gtc2l6aW5nOiBib3JkZXItYm94OwogICAgY2xlYXI6IGJvdGg7CiAgICBwb3NpdGlvbjogcmVsYXRpdmU7Cn0KCiNjb250ZW50OmJlZm9yZSB7CiAgICBjb250ZW50OiAiIjsKICAgIGNsZWFyOiBib3RoOwogICAgZGlzcGxheTogdGFibGU7Cn0KCiNmb290ZXIgLmZvb3Rlci1ib2R5IGEgewogICAgY29sb3I6IEBhdWktZm9vdGVyLWJvZHktbGluay10ZXh0LWNvbG9yOwp9CgojZm9vdGVyIC5mb290ZXItYm9keSA+IHVsLAojZm9vdGVyIC5mb290ZXItYm9keSA+IHAgewogICAgbWFyZ2luOiBAYXVpLWdyaWQgMCAwIDA7Cn0KCiNmb290ZXIgLmZvb3Rlci1ib2R5ID4gdWw6Zmlyc3QtY2hpbGQsCiNmb290ZXIgLmZvb3Rlci1ib2R5ID4gcDpmaXJzdC1jaGlsZCB7CiAgICBtYXJnaW46IDA7Cn0KCiNmb290ZXIgLmZvb3Rlci1ib2R5ID4gdWwgewogICAgZGlzcGxheTogYmxvY2s7CiAgICBmb250LXNpemU6IDA7CiAgICBsaXN0LXN0eWxlOiBub25lOwogICAgcGFkZGluZzogMDsKfQoKI2Zvb3RlciAuZm9vdGVyLWJvZHkgPiB1bCA+IGxpIHsKICAgIGRpc3BsYXk6IGlubGluZS1ibG9jazsKICAgIGZvbnQtc2l6ZTogQGF1aS1mb250LXNpemUtc21hbGw7CiAgICBsaW5lLWhlaWdodDogMS42NjY2NjY2NjY2NjY2NzsKICAgIHBhZGRpbmc6IDA7CiAgICB3aGl0ZS1zcGFjZTogbm93cmFwOwp9CgojZm9vdGVyIC5mb290ZXItYm9keSA+IHVsID4gbGkgKyBsaSB7CiAgICBtYXJnaW4tbGVmdDogQGF1aS1ncmlkOwp9CgojZm9vdGVyIC5mb290ZXItYm9keSA+IHVsID4gbGk6YWZ0ZXIgewogICAgY29udGVudDogIlxiNyI7IC8qIG1pZCBkb3QgKi8KICAgIG1hcmdpbi1sZWZ0OiBAYXVpLWdyaWQ7CiAgICBzcGVhazogbm9uZTsKfQojZm9vdGVyIC5mb290ZXItYm9keSA+IHVsID4gbGk6bGFzdC1jaGlsZDphZnRlciB7CiAgICBkaXNwbGF5OiBub25lOwp9CgoKLyoqCiAqIEdST1VQL0lURU0KICovCgouYXVpLWdyb3VwIHsKICAgIGRpc3BsYXk6IHRhYmxlOwogICAgYm94LXNpemluZzogYm9yZGVyLWJveDsKICAgIGJvcmRlci1zcGFjaW5nOiAwOwogICAgdGFibGUtbGF5b3V0OiBmaXhlZDsKICAgIHdpZHRoOiAxMDAlOwp9CgouYXVpLWdyb3VwID4gLmF1aS1pdGVtIHsKICAgIGJveC1zaXppbmc6IGJvcmRlci1ib3g7CiAgICBkaXNwbGF5OiB0YWJsZS1jZWxsOwogICAgbWFyZ2luOiAwOwogICAgdmVydGljYWwtYWxpZ246IHRvcDsKfQoKLmF1aS1ncm91cCA+IC5hdWktaXRlbSArIC5hdWktaXRlbSB7CiAgICBwYWRkaW5nLWxlZnQ6IChAYXVpLWdyaWQgKiAyKTsKfQoKLyogZGVmZW5zaXZlIGhlYWRlciBhbGxvd2FuY2UgKi8KLmF1aS1sYXlvdXQgLmF1aS1ncm91cCA+IGhlYWRlciB7CiAgICBkaXNwbGF5OiB0YWJsZS1jYXB0aW9uOwp9CgovKiAuYXVpLWdyb3VwLXNwbGl0OiB0d28gaXRlbXM7IGFsaWdubWVudCBpcyBsZWZ0LCB0aGVuIHJpZ2h0IChzcGxpdHMgdGhlIGxheW91dCkuICovCi5hdWktZ3JvdXAuYXVpLWdyb3VwLXNwbGl0ID4gLmF1aS1pdGVtIHsKICAgIHRleHQtYWxpZ246IHJpZ2h0Owp9Ci5hdWktZ3JvdXAuYXVpLWdyb3VwLXNwbGl0ID4gLmF1aS1pdGVtOmZpcnN0LWNoaWxkIHsKICAgIHRleHQtYWxpZ246IGxlZnQ7Cn0KCi8qIC5hdWktZ3JvdXAtdHJpbzogdGhyZWUgaXRlbXM7IGFsaWdubWVudCBpcyBsZWZ0LCBjZW50ZXIsIHJpZ2h0ICovCi5hdWktZ3JvdXAuYXVpLWdyb3VwLXRyaW8gPiAuYXVpLWl0ZW0gewogICAgdGV4dC1hbGlnbjogbGVmdDsKfQouYXVpLWdyb3VwLmF1aS1ncm91cC10cmlvID4gLmF1aS1pdGVtICsgLmF1aS1pdGVtIHsKICAgIHRleHQtYWxpZ246IGNlbnRlcjsKfQouYXVpLWdyb3VwLmF1aS1ncm91cC10cmlvID4gLmF1aS1pdGVtICsgLmF1aS1pdGVtICsgLmF1aS1pdGVtIHsKICAgIHRleHQtYWxpZ246IHJpZ2h0Owp9CgovKioKICogREVGQVVMVCBUSEVNRSBTUEFDSU5HCiAqLwoKI2NvbnRlbnQgewogICAgbWFyZ2luOiAwOwogICAgcGFkZGluZzogMDsKfQoKLyoqCiAqIFBBR0UgREVTSUdOCiAqLwpib2R5IHsKICAgIGJhY2tncm91bmQ6IEBhdWktYmFja2dyb3VuZC1jb2xvcjsKICAgIGNvbG9yOiBAYXVpLXRleHQtY29sb3I7Cn0KCmEgewogICAgY29sb3I6IEBhdWktbGluay1jb2xvcjsKICAgIHRleHQtZGVjb3JhdGlvbjogQGF1aS1saW5rLWRlY29yYXRpb247Cn0KYTpmb2N1cywKYTpob3ZlciwKYTphY3RpdmUgewogICAgdGV4dC1kZWNvcmF0aW9uOiBAYXVpLWxpbmstZGVjb3JhdGlvbi1hY3RpdmU7Cn0KCiNmb290ZXIgLmZvb3Rlci1ib2R5IHsKICAgIGNvbG9yOiBAYXVpLWZvb3Rlci1ib2R5LXRleHQtY29sb3I7CiAgICBmb250LXNpemU6IEBhdWktZm9udC1zaXplLXNtYWxsOwogICAgbGluZS1oZWlnaHQ6IDEuNjY2NjY2NjY2NjY2Njc7CiAgICBtYXJnaW46IChAYXVpLWdyaWQgKiAyKSAwOwogICAgcGFkZGluZzogMCBAYXVpLWdyaWQgKEBhdWktZ3JpZCAqIDIgKyAxKSBAYXVpLWdyaWQ7CiAgICBtaW4taGVpZ2h0OiA0NHB4OyAvKiBtYXJnaW4gKyBoZWlnaHQgb2YgaW1hZ2UsIG1lYW5zIGZvb3RlciBpcyBqdXN0IGFzIGhpZ2ggaWYgbm8gZm9vdGVyIGxpbmsgcHJlc2VudCAqLwogICAgdGV4dC1hbGlnbjogY2VudGVyOwp9CgoKLyoqCiAqIENPTlRFTlQgUEFORUwKICovCiNjb250ZW50ID4gLmF1aS1wYW5lbCB7CiAgICBiYWNrZ3JvdW5kOiBAYXVpLXBhbmVsLWJnLWNvbG9yOwogICAgbWFyZ2luOiAoQGF1aS1ncmlkICogMikgMCAwIDA7CiAgICBwYWRkaW5nOiAoQGF1aS1ncmlkICogMik7CiAgICBib3JkZXItY29sb3I6IEBhdWktYm9yZGVyLWNvbG9yOwogICAgYm9yZGVyLXN0eWxlOiBAYXVpLWJvcmRlci1zdHlsZTsKICAgIGJvcmRlci13aWR0aDogQGF1aS1ib3JkZXItd2lkdGggMDsKfQoKI2NvbnRlbnQgPiAuYXVpLXBhZ2UtaGVhZGVyIHsKICAgIHBhZGRpbmc6IChAYXVpLWdyaWQgKiAyKTsKfQojY29udGVudCA+IC5hdWktcGFnZS1oZWFkZXIgKyAuYXVpLXBhbmVsIHsKICAgIG1hcmdpbi10b3A6IDA7Cn0KCiNjb250ZW50ID4gLmF1aS1wYWdlLWhlYWRlcjpmaXJzdC1jaGlsZCB7CiAgICBtYXJnaW4tdG9wOiAwOwp9Ci5hdWktcGFuZWwgKyAuYXVpLXBhbmVsIHsKICAgIG1hcmdpbi10b3A6IChAYXVpLWdyaWQgKiAyKTsKfQoKLyohIEFVSSBQYWdlIFBhbmVsICovCi5hdWktcGFnZS1wYW5lbCB7CiAgICBiYWNrZ3JvdW5kOiBAYXVpLXBhbmVsLWJnLWNvbG9yOwogICAgYm9yZGVyOiBAYXVpLWJvcmRlci13aWR0aCBAYXVpLWJvcmRlci10eXBlIEBhdWktYm9yZGVyLWNvbG9yOwogICAgYm9yZGVyLWxlZnQtd2lkdGg6IDA7CiAgICBib3JkZXItcmlnaHQtd2lkdGg6IDA7CiAgICBib3gtc2l6aW5nOiBib3JkZXItYm94OwogICAgY2xlYXI6IGJvdGg7CiAgICBkaXNwbGF5OiBibG9jazsKICAgIG1hcmdpbjogKEBhdWktZ3JpZCAqIDIpIDAgMCAwOwogICAgcGFkZGluZzogMDsKICAgIHBvc2l0aW9uOiByZWxhdGl2ZTsKICAgIHdpZHRoOiAxMDAlOwp9Ci5hdWktcGFnZS1wYW5lbC1pbm5lciB7CiAgICBib3JkZXItc3BhY2luZzogMDsKICAgIGRpc3BsYXk6IHRhYmxlOwogICAgdGFibGUtbGF5b3V0OiBmaXhlZDsKICAgIHdpZHRoOiAxMDAlOwp9Ci5hdWktcGFnZS1wYW5lbC1uYXYsCi5hdWktcGFnZS1wYW5lbC1jb250ZW50LAouYXVpLXBhZ2UtcGFuZWwtaXRlbSwKLmF1aS1wYWdlLXBhbmVsLXNpZGViYXIgewogICAgYm94LXNpemluZzogYm9yZGVyLWJveDsKICAgIGRpc3BsYXk6IHRhYmxlLWNlbGw7CiAgICBwYWRkaW5nOiAoQGF1aS1ncmlkICogMik7CiAgICB2ZXJ0aWNhbC1hbGlnbjogdG9wOwp9Ci5hdWktcGFnZS1wYW5lbC1uYXYgewogICAgYm9yZGVyLXJpZ2h0OiBAYXVpLWJvcmRlci13aWR0aCBAYXVpLWJvcmRlci10eXBlIEBhdWktYm9yZGVyLWNvbG9yOwogICAgd2lkdGg6IEBhdWktcGFnZS1uYXYtd2lkdGg7Cn0KLmF1aS1wYWdlLXBhbmVsLXNpZGViYXIgewogICAgd2lkdGg6IEBhdWktcGFnZS1zaWRlYmFyLXdpZHRoOwp9Ci5hdWktcGFnZS1wYW5lbC1pdGVtIHsKICAgIHBhZGRpbmc6IDA7Cn0KLmF1aS1wYWdlLXBhbmVsLW5hdiB+IC5hdWktcGFnZS1wYW5lbC1zaWRlYmFyIHsKICAgIHdpZHRoOiAoQGF1aS1wYWdlLXNpZGViYXItd2lkdGggLSA1KTsKfQovKiBQYWdlIFBhbmVsIEludGVyb3BzICovCi5hdWktcGFnZS1oZWFkZXIgKyAuYXVpLXBhZ2UtcGFuZWwsCi5hdWktbmF2YmFyICsgLmF1aS1wYWdlLXBhbmVsIHsKICAgIG1hcmdpbi10b3A6IDA7Cn0KLmF1aS1uYXZiYXIgKyAuYXVpLXBhZ2UtcGFuZWwgewogICAgYm9yZGVyLXRvcDogbm9uZTsKfQouYXVpLXBhZ2UtcGFuZWwtbmF2ID4gLmF1aS1uYXYtdmVydGljYWwgewogICAgbWFyZ2luLWxlZnQ6IC0oQGF1aS1ncmlkKTsKICAgIG1hcmdpbi1yaWdodDogLShAYXVpLWdyaWQpOwp9CgovKioKICogUGFnZSB2YXJpYXRpb25zCiAqLwouYXVpLXBhZ2UtZm9jdXNlZCAuYXVpLXBhZ2UtaGVhZGVyLAouYXVpLXBhZ2UtZm9jdXNlZCAuYXVpLXBhZ2UtcGFuZWwsCi5hdWktcGFnZS1mb2N1c2VkICNmb290ZXIgLmZvb3Rlci1ib2R5LAouYXVpLXBhZ2Utbm90aWZpY2F0aW9uIC5hdWktcGFnZS1oZWFkZXIsCi5hdWktcGFnZS1ub3RpZmljYXRpb24gLmF1aS1wYWdlLXBhbmVsLAouYXVpLXBhZ2Utbm90aWZpY2F0aW9uICNmb290ZXIgLmZvb3Rlci1ib2R5LAouYXVpLXBhZ2UtZml4ZWQgLmF1aS1oZWFkZXItaW5uZXIsCi5hdWktcGFnZS1maXhlZCAuYXVpLXBhZ2UtaGVhZGVyLWlubmVyLAouYXVpLXBhZ2UtZml4ZWQgLmF1aS1uYXZncm91cC1ob3Jpem9udGFsIC5hdWktbmF2Z3JvdXAtaW5uZXIsCi5hdWktcGFnZS1maXhlZCAuYXVpLXBhZ2UtcGFuZWwtaW5uZXIsCi5hdWktcGFnZS1maXhlZCAjZm9vdGVyIC5mb290ZXItYm9keSwKLmF1aS1wYWdlLWh5YnJpZCAuYXVpLXBhZ2UtaGVhZGVyLAouYXVpLXBhZ2UtaHlicmlkIC5hdWktbmF2Z3JvdXAtaG9yaXpvbnRhbCAuYXVpLW5hdmdyb3VwLWlubmVyLAouYXVpLXBhZ2UtaHlicmlkIC5hdWktcGFnZS1wYW5lbC1pbm5lciwKLmF1aS1wYWdlLWh5YnJpZCAjZm9vdGVyIC5mb290ZXItYm9keSB7CiAgICBtYXJnaW4tbGVmdDogYXV0bzsKICAgIG1hcmdpbi1yaWdodDogYXV0bzsKICAgIHdpZHRoOiBAYXVpLXBhZ2Utd2lkdGgteGxhcmdlOwp9CgovKiBleHRyYSB3aWR0aCBzbyBsZWZ0IGVkZ2Ugb2YgaG92ZXJhYmxlIGNvbnRlbnQgYWxpZ25zIHdpdGggbGVmdCBlZGdlIG9mIGNvbnRlbnQgd2hpbGUgaW5hY3RpdmUuIE9uIGhvdmVyLCB0aGUgaG92ZXIgYWZmb3JkYW5jZSBkb2VzIGV4dGVuZCBvdXRzaWRlIHRoZSBhbGlnbm1lbnQgYnV0IHRoaXMgbG9va3MgYmV0dGVyIHRoYW4gdGhlIG90aGVyIHdheSBhcm91bmQuICovCi5hdWktcGFnZS1maXhlZCAuYXVpLWhlYWRlci1pbm5lciwKLmF1aS1wYWdlLWZpeGVkIC5hdWktbmF2Z3JvdXAtaG9yaXpvbnRhbCAuYXVpLW5hdmdyb3VwLWlubmVyLAouYXVpLXBhZ2UtaHlicmlkIC5hdWktbmF2Z3JvdXAtaG9yaXpvbnRhbCAuYXVpLW5hdmdyb3VwLWlubmVyIHsKICAgIHdpZHRoOiAoQGF1aS1wYWdlLXdpZHRoLXhsYXJnZSArIChAYXVpLWdyaWQgKiAyKSk7Cn0KCi5hdWktcGFnZS1mb2N1c2VkLAouYXVpLXBhZ2Utc2l6ZSB7CiAgICAmLXNtYWxsIHsKICAgICAgICAuYXVpLXBhZ2UtaGVhZGVyLAogICAgICAgIC5hdWktcGFnZS1wYW5lbCwKICAgICAgICAjZm9vdGVyIC5mb290ZXItYm9keSB7CiAgICAgICAgICAgIHdpZHRoOiBAYXVpLXBhZ2Utd2lkdGgtc21hbGw7CiAgICAgICAgfQogICAgfQogICAgJi1tZWRpdW0gewogICAgICAgIC5hdWktcGFnZS1oZWFkZXIsCiAgICAgICAgLmF1aS1wYWdlLXBhbmVsLAogICAgICAgICNmb290ZXIgLmZvb3Rlci1ib2R5IHsKICAgICAgICAgICAgd2lkdGg6IEBhdWktcGFnZS13aWR0aC1tZWRpdW07CiAgICAgICAgfQogICAgfQogICAgJi1sYXJnZSB7CiAgICAgICAgLmF1aS1wYWdlLWhlYWRlciwKICAgICAgICAuYXVpLXBhZ2UtcGFuZWwsCiAgICAgICAgI2Zvb3RlciAuZm9vdGVyLWJvZHkgewogICAgICAgICAgICB3aWR0aDogQGF1aS1wYWdlLXdpZHRoLWxhcmdlOwogICAgICAgIH0KICAgIH0KICAgICYteGxhcmdlIHsKICAgICAgICAuYXVpLXBhZ2UtaGVhZGVyLAogICAgICAgIC5hdWktcGFnZS1wYW5lbCwKICAgICAgICAjZm9vdGVyIC5mb290ZXItYm9keSB7CiAgICAgICAgICAgIHdpZHRoOiBAYXVpLXBhZ2Utd2lkdGgteGxhcmdlOwogICAgICAgIH0KICAgIH0KfQoKLmF1aS1wYWdlLWZvY3VzZWQsCi5hdWktcGFnZS1ub3RpZmljYXRpb24gewogICAgLmF1aS1wYWdlLXBhbmVsIHsKICAgICAgICBib3JkZXItcmFkaXVzOiBAYXVpLWJvcmRlci1yYWRpdXMtbWVkaXVtOwogICAgICAgIGJvcmRlci13aWR0aDogQGF1aS1ib3JkZXItd2lkdGg7CiAgICB9Cn0KCi5hdWktcGFnZS1maXhlZCAuYXVpLXBhZ2UtcGFuZWwtaW5uZXIsCi5hdWktcGFnZS1maXhlZCAjY29udGVudCA+IC5hdWktcGFnZS1oZWFkZXIgewogICAgcGFkZGluZy1sZWZ0OiAwOwogICAgcGFkZGluZy1yaWdodDogMDsKfQoKLmF1aS1wYWdlLWZpeGVkIC5hdWktcGFnZS1wYW5lbC1uYXY6Zmlyc3QtY2hpbGQsCi5hdWktcGFnZS1maXhlZCAuYXVpLXBhZ2UtcGFuZWwtY29udGVudDpmaXJzdC1jaGlsZCwKLmF1aS1wYWdlLWZpeGVkIC5hdWktcGFnZS1wYW5lbC1pdGVtOmZpcnN0LWNoaWxkLAouYXVpLXBhZ2UtZml4ZWQgLmF1aS1wYWdlLXBhbmVsLXNpZGViYXI6Zmlyc3QtY2hpbGQsCi5hdWktcGFnZS1oeWJyaWQgLmF1aS1wYWdlLXBhbmVsLW5hdjpmaXJzdC1jaGlsZCwKLmF1aS1wYWdlLWh5YnJpZCAuYXVpLXBhZ2UtcGFuZWwtY29udGVudDpmaXJzdC1jaGlsZCwKLmF1aS1wYWdlLWh5YnJpZCAuYXVpLXBhZ2UtcGFuZWwtaXRlbTpmaXJzdC1jaGlsZCwKLmF1aS1wYWdlLWh5YnJpZCAuYXVpLXBhZ2UtcGFuZWwtc2lkZWJhcjpmaXJzdC1jaGlsZCB7CiAgICBwYWRkaW5nLWxlZnQ6IDA7Cn0KLmF1aS1wYWdlLWZpeGVkIC5hdWktcGFnZS1wYW5lbC1uYXY6bGFzdC1jaGlsZCwKLmF1aS1wYWdlLWZpeGVkIC5hdWktcGFnZS1wYW5lbC1jb250ZW50Omxhc3QtY2hpbGQsCi5hdWktcGFnZS1maXhlZCAuYXVpLXBhZ2UtcGFuZWwtaXRlbTpsYXN0LWNoaWxkLAouYXVpLXBhZ2UtZml4ZWQgLmF1aS1wYWdlLXBhbmVsLXNpZGViYXI6bGFzdC1jaGlsZCwKLmF1aS1wYWdlLWh5YnJpZCAuYXVpLXBhZ2UtcGFuZWwtbmF2Omxhc3QtY2hpbGQsCi5hdWktcGFnZS1oeWJyaWQgLmF1aS1wYWdlLXBhbmVsLWNvbnRlbnQ6bGFzdC1jaGlsZCwKLmF1aS1wYWdlLWh5YnJpZCAuYXVpLXBhZ2UtcGFuZWwtaXRlbTpsYXN0LWNoaWxkLAouYXVpLXBhZ2UtaHlicmlkIC5hdWktcGFnZS1wYW5lbC1zaWRlYmFyOmxhc3QtY2hpbGQgewogICAgcGFkZGluZy1yaWdodDogMDsKfQoKLyogcmVzZXQgdG8gMTAwJSBpbnNpZGUgcGFnZSBwYW5lbCAqLwouYXVpLXBhZ2UtcGFuZWwgLmF1aS1wYWdlLWhlYWRlciB7CiAgICB3aWR0aDogYXV0bzsKfQouYXVpLXBhZ2UtcGFuZWwgLmF1aS1wYWdlLWhlYWRlci1pbm5lciB7CiAgICB3aWR0aDogMTAwJTsKfQoKCi8qKgogKiBUQUJTIEFTIEZJUlNUIENISUxEIElOIENPTlRFTlQKICogRXhwbGljaXRseSBzZXRzIGJnIHRvIHdoaXRlLCBjaGFuZ2VzIGhvcml6b250YWwgaG92ZXJzIHRvIHdvcmsgb24gZ3JleS4KICogUmVtZW1iZXIgdGhlc2UgZXh0ZW5kIHRoZSBzdGFuZGFyZCBjb21wb25lbnQgc3R5bGVzLgogKi8KCiNjb250ZW50ID4gLmF1aS10YWJzIHsKICAgIG1hcmdpbjogKEBhdWktZ3JpZCAqIDIpOwogICAgYmFja2dyb3VuZDogdHJhbnNwYXJlbnQ7Cn0KCiNjb250ZW50ID4gLmF1aS10YWJzID4gLnRhYnMtcGFuZSB7CiAgICBwYWRkaW5nOiAoQGF1aS1ncmlkICogMik7Cn0KCiNjb250ZW50ID4gLmF1aS10YWJzLmhvcml6b250YWwtdGFicyA+IC50YWJzLXBhbmUgewogICAgYm9yZGVyOiBAYXVpLWJvcmRlci13aWR0aCBAYXVpLWJvcmRlci10eXBlIEBhdWktYm9yZGVyLWNvbG9yOwogICAgYm9yZGVyLXJhZGl1czogQGF1aS1ib3JkZXItcmFkaXVzLXNtYWxsOwogICAgYmFja2dyb3VuZDogQGF1aS1wYW5lbC1iZy1jb2xvcjsKfQoKI2NvbnRlbnQgPiAuYXVpLXRhYnMuaG9yaXpvbnRhbC10YWJzID4gLnRhYnMtbWVudSB7CiAgICBkaXNwbGF5OiB0YWJsZTsgLyogc3RvcHMgYSBnYXAgYXBwZWFyaW5nICovCn0KCi8qKgogKiBBVUkgRm9ybXMgaW5zaWRlIG9mIGEgZm9jdXNlZCBwYWdlCiAqLwoKLmF1aS1wYWdlLWZvY3VzZWQgLmF1aS1wYWdlLXBhbmVsLWNvbnRlbnQgPiBoMjpmaXJzdC1jaGlsZCwKLmF1aS1wYWdlLW5vdGlmaWNhdGlvbiAuYXVpLXBhZ2UtcGFuZWwtY29udGVudCA+IGgxOmZpcnN0LWNoaWxkIHsKICAgIGJvcmRlci1ib3R0b206IEBhdWktYm9yZGVyLXdpZHRoIEBhdWktYm9yZGVyLXR5cGUgQGF1aS1ib3JkZXItY29sb3I7CiAgICBtYXJnaW4tYm90dG9tOiAoQGF1aS1ncmlkICogMik7CiAgICBwYWRkaW5nLWJvdHRvbTogKEBhdWktZ3JpZCAqIDIpOwp9CgouYXVpLXBhZ2Utbm90aWZpY2F0aW9uIHsKICAgIC5hdWktcGFnZS1wYW5lbCB7CiAgICAgICAgbWFyZ2luLXRvcDogQGF1aS1wYWdlLW5vdGlmaWNhdGlvbi1wYW5lbC1tYXJnaW4tdG9wOwogICAgfQoKICAgIC5hdWktcGFnZS1wYW5lbC1jb250ZW50IHsKICAgICAgICBjb2xvcjogQGF1aS1jb2xvci1tZWRpdW0tZ3JheTsKICAgICAgICBwYWRkaW5nOiBAYXVpLXBhZ2Utbm90aWZpY2F0aW9uLWNvbnRlbnQtcGFkZGluZzsKICAgICAgICB0ZXh0LWFsaWduOiBjZW50ZXI7CgogICAgICAgIC5hdWktcGFnZS1ub3RpZmljYXRpb24tZGVzY3JpcHRpb24gewogICAgICAgICAgICBmb250LXNpemU6IEBhdWktZm9udC1zaXplLXhsYXJnZTsKICAgICAgICB9CgogICAgICAgIGZvcm0uYXVpIC50ZXh0IHsKICAgICAgICAgICAgbWFyZ2luLXJpZ2h0OiBAYXVpLXBhZ2Utbm90aWZpY2F0aW9uLXBhbmVsLWNvbnRlbnQtZm9ybS10ZXh0LW1hcmdpbi1yaWdodDsKICAgICAgICB9CiAgICB9CgogICAgJi1kZXRhaWxzIHsKICAgICAgICBtYXJnaW46IDAgYXV0bzsKICAgICAgICBtYXgtd2lkdGg6IEBhdWktcGFnZS1ub3RpZmljYXRpb24tZGV0YWlscy1taW4td2lkdGg7CiAgICAgICAgd2lkdGg6IEBhdWktcGFnZS13aWR0aC14bGFyZ2U7CgogICAgICAgICYtaGVhZGVyIHsKICAgICAgICAgICAgY29sb3I6IEBhdWktY29sb3ItbWVkaXVtLWdyYXk7CiAgICAgICAgICAgIG1hcmdpbjogQGF1aS1wYWdlLW5vdGlmaWNhdGlvbi1kZXRhaWxzLWhlYWRlci10b3AtbWFyZ2luIGF1dG8gMDsKICAgICAgICAgICAgcG9zaXRpb246IHJlbGF0aXZlOwogICAgICAgICAgICB0ZXh0LWFsaWduOiBjZW50ZXI7CgogICAgICAgICAgICAmLWV4cGFuZGVyIHsKICAgICAgICAgICAgICAgICY6OmJlZm9yZSB7CiAgICAgICAgICAgICAgICAgICAgYm9yZGVyLXRvcDogMXB4IHNvbGlkIEBhdWktYm9yZGVyLWNvbG9yOwogICAgICAgICAgICAgICAgICAgIGNvbnRlbnQ6ICcnOwogICAgICAgICAgICAgICAgICAgIGRpc3BsYXk6IGJsb2NrOwogICAgICAgICAgICAgICAgICAgIGxlZnQ6IDA7CiAgICAgICAgICAgICAgICAgICAgcG9zaXRpb246IGFic29sdXRlOwogICAgICAgICAgICAgICAgICAgIHJpZ2h0OiAwOwogICAgICAgICAgICAgICAgICAgIHRvcDogNTAlOwogICAgICAgICAgICAgICAgfQoKICAgICAgICAgICAgICAgIC5hdWktZXhwYW5kZXItdHJpZ2dlciB7CiAgICAgICAgICAgICAgICAgICAgYmFja2dyb3VuZC1jb2xvcjogQGF1aS1idXR0b24tZGVmYXVsdC1iZy1jb2xvcjsKICAgICAgICAgICAgICAgICAgICBkaXNwbGF5OiBpbmxpbmUtYmxvY2s7CiAgICAgICAgICAgICAgICAgICAgcGFkZGluZzogQGF1aS1wYWdlLW5vdGlmaWNhdGlvbi1kZXRhaWxzLWhlYWRlci1leHBhbmRlci10cmlnZ2VyLXBhZGRpbmc7CiAgICAgICAgICAgICAgICAgICAgcG9zaXRpb246IHJlbGF0aXZlOwogICAgICAgICAgICAgICAgfQogICAgICAgICAgICB9CiAgICAgICAgfQogICAgfQp9CgouYXVpLXBhZ2UtZm9jdXNlZCAuYXVpLXBhZ2UtcGFuZWwtY29udGVudCA+IGZvcm0uYXVpIC5idXR0b25zLWNvbnRhaW5lciB7CiAgICBib3JkZXItdG9wOiBAYXVpLWJvcmRlci13aWR0aCBAYXVpLWJvcmRlci10eXBlIEBhdWktYm9yZGVyLWNvbG9yOwogICAgbWFyZ2luLXRvcDogKEBhdWktZ3JpZCAqIDIpOwogICAgcGFkZGluZy10b3A6IChAYXVpLWdyaWQgKiAyKTsKfQoKLy8gcmVzcG9uc2l2ZSBsYXlvdXQgbWl4aW5zCi5hdWktcmVzcG9uc2l2ZS1sYXlvdXQtZnVsbC13aWR0aCgpIHsKICAgIC5hdWktcGFnZS1oZWFkZXIsCiAgICAuYXVpLXBhZ2UtcGFuZWwgewogICAgICAgICNhdWkuYm94LXNpemluZyhib3JkZXItYm94KTsKICAgICAgICB3aWR0aDogMTAwJTsKICAgIH0KCiAgICAuYXVpLXBhZ2UtcGFuZWwgewogICAgICAgIG1hcmdpbi10b3A6IDA7CiAgICB9Cn0KCi5hdWktcmVzcG9uc2l2ZS1sYXlvdXQtcmVtb3ZlLWJvcmRlcnMoKSB7CiAgICAuYXVpLXBhZ2UtcGFuZWwgewogICAgICAgICNhdWkuYm9yZGVyLXJhZGl1cygwKTsKICAgICAgICBib3JkZXItbGVmdDogMDsKICAgICAgICBib3JkZXItcmlnaHQ6IDA7CiAgICB9Cn0KCiNhdWkucmVzcG9uc2l2ZS1zbWFsbCh7CiAgICAuYXVpLWdyb3VwIHsKICAgICAgICA+IC5hdWktaXRlbSB7CiAgICAgICAgZGlzcGxheTogYmxvY2s7CiAgICAgICAgd2lkdGg6IGF1dG87CgogICAgICAgICAgICArIC5hdWktaXRlbSB7CiAgICAgICAgICAgICAgICBwYWRkaW5nLWxlZnQ6IDA7CiAgICAgICAgICAgICAgICBwYWRkaW5nLXRvcDogQGF1aS1ncmlkOwogICAgICAgICAgICB9CiAgICB9CgogICAgJi5hdWktZ3JvdXAtc3BsaXQsCiAgICAmLmF1aS1ncm91cC10cmlvIHsKICAgICAgICAgICAgPiAuYXVpLWl0ZW0sID4gLmF1aS1pdGVtICsgLmF1aS1pdGVtLAogICAgICAgICAgICA+IC5hdWktaXRlbSArIC5hdWktaXRlbSArIC5hdWktaXRlbSB7CiAgICAgICAgICAgICAgICB0ZXh0LWFsaWduOiBsZWZ0OwogICAgICAgICAgICB9CiAgICAgICAgfQogICAgfQoKICAgIC5hdWktcGFnZS1maXhlZCwKICAgIC5hdWktcGFnZS1oeWJyaWQgewogICAgICAgICNjb250ZW50ID4gLmF1aS1wYWdlLWhlYWRlciwKICAgICAgICAuYXVpLXBhZ2UtcGFuZWwtaW5uZXIsCiAgICAgICAgLmF1aS1oZWFkZXItaW5uZXIsCiAgICAgICAgLmF1aS1uYXZncm91cC1ob3Jpem9udGFsIC5hdWktbmF2Z3JvdXAtaW5uZXIsCiAgICAgICAgI2Zvb3RlciAuZm9vdGVyLWJvZHkgewogICAgICAgICAgICAjYXVpLmJveC1zaXppbmcoYm9yZGVyLWJveCk7CiAgICAgICAgICAgIHdpZHRoOiAxMDAlOwogICAgICAgIH0KICAgIH0KCiAgICAuYXVpLXBhZ2UtaGVhZGVyLWlubmVyIHsKICAgICAgICBkaXNwbGF5OiBibG9jazsKICAgICAgICB3aWR0aDogMTAwJTsKICAgIH0KCiAgICAuYXVpLXBhZ2UtaGVhZGVyLWFjdGlvbnMgewogICAgICAgIGRpc3BsYXk6IGJsb2NrOwogICAgICAgIHdpZHRoOiBhdXRvOwogICAgICAgIHRleHQtYWxpZ246IGxlZnQ7CiAgICAgICAgbWFyZ2luLXRvcDogKEBhdWktZ3JpZCAgKiAyKTsKICAgICAgICBwYWRkaW5nLWxlZnQ6IDA7CiAgICAgICAgcGFkZGluZy1yaWdodDogKEBhdWktZ3JpZCAqIDIpCiAgICB9Cn0pOwoKI2F1aS5yZXNwb25zaXZlLW1lZGl1bSh7CiAgICAuYXVpLXBhZ2UtaHlicmlkIHsKICAgICAgICAuYXVpLXBhZ2UtaGVhZGVyLAogICAgICAgIC5hdWktcGFnZS1wYW5lbC1pbm5lciwKICAgICAgICAuYXVpLXBhZ2UtZml4ZWQgLmF1aS1oZWFkZXItaW5uZXIsCiAgICAgICAgLmF1aS1uYXZncm91cC1ob3Jpem9udGFsIC5hdWktbmF2Z3JvdXAtaW5uZXIgewogICAgICAgICAgICAjYXVpLmJveC1zaXppbmcoYm9yZGVyLWJveCk7CiAgICAgICAgICAgIHdpZHRoOiAxMDAlOwogICAgICAgIH0KICAgIH0KfSk7CgojYXVpLnJlc3BvbnNpdmUtbGFyZ2UoewogICAgLmF1aS1wYWdlLWZpeGVkLAogICAgLmF1aS1wYWdlLWh5YnJpZCB7CiAgICAgICAgI2NvbnRlbnQgPiAuYXVpLXBhZ2UtaGVhZGVyLAogICAgICAgIC5hdWktcGFnZS1wYW5lbC1pbm5lciB7CiAgICAgICAgICAgIHBhZGRpbmctbGVmdDogKEBhdWktZ3JpZCAqIDIpOwogICAgICAgICAgICBwYWRkaW5nLXJpZ2h0OiAoQGF1aS1ncmlkICogMik7CiAgICAgICAgfQogICAgfQoKICAgIC5hdWktcGFnZS1wYW5lbC1jb250ZW50LAogICAgLmF1aS1wYWdlLXBhbmVsLXNpZGViYXIgewogICAgICAgIGRpc3BsYXk6IGJsb2NrOwogICAgICAgIHBhZGRpbmctbGVmdDogMDsKICAgICAgICBwYWRkaW5nLXJpZ2h0OiAwOwogICAgICAgIHdpZHRoOiBhdXRvOwogICAgfQoKICAgIC5hdWktcGFnZS1maXhlZCAuYXVpLWhlYWRlci1pbm5lciwKICAgIC5hdWktcGFnZS1maXhlZCAuYXVpLXBhZ2UtaGVhZGVyLWlubmVyLAogICAgLmF1aS1wYWdlLWZpeGVkIC5hdWktcGFnZS1wYW5lbC1pbm5lciwKICAgIC5hdWktcGFnZS1maXhlZCAjZm9vdGVyIC5mb290ZXItYm9keSB7CiAgICAgICAgYm94LXNpemluZzogYm9yZGVyLWJveDsKICAgICAgICB3aWR0aDoxMDAlOwogICAgfQp9KTsKCi8vIEdlbmVyYWwgcmVzcG9uc2l2ZSBsYXlvdXQgYnJlYWtwb2ludHMKaHRtbC5hdWktcmVzcG9uc2l2ZSB7CgogICAgLy8gc28gdGhhdCB0ZXh0IGluIHRoZSBmb290ZXIgd3JhcHMgY29ycmVjdGx5CiAgICAjZm9vdGVyIC5mb290ZXItYm9keSA+IHVsID4gbGkgewogICAgICAgIHdoaXRlLXNwYWNlOiBub3JtYWw7CiAgICB9CgogICAgLy8gRm9jdXNlZCBwYWdlIHJlc3BvbnNpdmUgbGF5b3V0IGJyZWFrcG9pbnRzCiAgICBAbWVkaWEgc2NyZWVuIGFuZCAobWF4LXdpZHRoOiA0MDBweCkgewogICAgICAgIC5hdWktcGFnZS1mb2N1c2VkLXNtYWxsIHsKICAgICAgICAgICAgLmF1aS1yZXNwb25zaXZlLWxheW91dC1mdWxsLXdpZHRoKCk7CiAgICAgICAgICAgIC5hdWktcmVzcG9uc2l2ZS1sYXlvdXQtcmVtb3ZlLWJvcmRlcnMoKTsKICAgICAgICB9CiAgICB9CgogICAgQG1lZGlhIHNjcmVlbiBhbmQgKG1heC13aWR0aDogNjAwcHgpIHsKICAgICAgICAuYXVpLXBhZ2UtZm9jdXNlZC1tZWRpdW0gewogICAgICAgICAgICAuYXVpLXJlc3BvbnNpdmUtbGF5b3V0LWZ1bGwtd2lkdGgoKTsKICAgICAgICAgICAgLmF1aS1yZXNwb25zaXZlLWxheW91dC1yZW1vdmUtYm9yZGVycygpOwogICAgICAgIH0KICAgIH0KCiAgICBAbWVkaWEgc2NyZWVuIGFuZCAobWF4LXdpZHRoOiA4MDBweCkgewogICAgICAgIC5hdWktcGFnZS1mb2N1c2VkLWxhcmdlIHsKICAgICAgICAgICAgLmF1aS1yZXNwb25zaXZlLWxheW91dC1mdWxsLXdpZHRoKCk7CiAgICAgICAgICAgIC5hdWktcmVzcG9uc2l2ZS1sYXlvdXQtcmVtb3ZlLWJvcmRlcnMoKTsKICAgICAgICB9CiAgICB9CgogICAgQG1lZGlhIHNjcmVlbiBhbmQgKG1heC13aWR0aDogOTgwcHgpIHsKICAgICAgICAuYXVpLXBhZ2UtZm9jdXNlZC14bGFyZ2UgewogICAgICAgICAgICAuYXVpLXJlc3BvbnNpdmUtbGF5b3V0LWZ1bGwtd2lkdGgoKTsKICAgICAgICAgICAgLmF1aS1yZXNwb25zaXZlLWxheW91dC1yZW1vdmUtYm9yZGVycygpOwogICAgICAgIH0KICAgIH0KfQoKI2Zvb3RlciAuZm9vdGVyLWJvZHksCiNmb290ZXItbG9nbyBhIHsKICAgIGJhY2tncm91bmQ6IHVybChkYXRhOmltYWdlL3N2Zyt4bWw7YmFzZTY0LFBITjJaeUIzYVdSMGFEMGlNVEUwSWlCb1pXbG5hSFE5SWpJMElpQjJhV1YzUW05NFBTSXdJREFnTVRFMElESTBJaUI0Yld4dWN6MGlhSFIwY0RvdkwzZDNkeTUzTXk1dmNtY3ZNakF3TUM5emRtY2lQanhuSUdacGJHdzlJaU0zTURjd056QWlQanhuUGp4d1lYUm9JR1E5SWsweU55NHlNRFl1TWpZMVl5MHVNVEV5TFM0eE5UUXRMakk1TWkwdU1qUTNMUzQwT0MwdU1qUTNMUzR3T1RjZ01DMHVNVGt5TGpBeU5TMHVNamMyTGpBM01pMHpMalV4TnlBeExqazNOaTAzTGpVNU5DQXpMakF5TFRFeExqYzVJRE11TURJeUxUUXVNVGsySURBdE9DNHlOekl0TVM0d05EWXRNVEV1TnprdE15NHdNaTB1TURnekxTNHdOUzB1TVRjNExTNHdOelF0TGpJM05TMHVNRGMwTFM0eE9EZ2dNQzB1TXpZNExqQTVNeTB1TkRndU1qUTNMUzR4TmpZdU1qSTNMUzR4Tnk0Mk1EZ3VNVEV5TGpnek5TQXhMamMySURFdU16UTRJRE11TnpBeklESXVOQ0ExTGpjM0lETXVNVElnTWk0eE5EUXVOelEySURRdU16ZzJJREV1TVRJMUlEWXVOall6SURFdU1USTJJREl1TWpjNElEQWdOQzQxTWkwdU16Z2dOaTQyTmpNdE1TNHhNallnTWk0d05qZ3RMamN5SURRdU1ERXRNUzQzTnpJZ05TNDNOek10TXk0eE1pNHlPQzB1TWpJM0xqSTNOaTB1TmpBNExqRXhMUzQ0TXpVaUx6NDhjR0YwYUNCa1BTSk5NVFF1TmpZeklEY3VNek0yWXk0NUlEQWdNUzQwTWkwdU1EVTFJREV1TmpBMExTNHdOVFV1TWpJeUlEQWdMalF6TkM0eE9EZ3VORE0wTGpRMUlEQWdMakEzTlMwdU1ERTJMakV6TFM0d016UXVNVGM0TFM0eE1pNHpNamN0TGpZMk15QXhMalV3TkMweExqWTNOQ0F5TGpVeE5DMHVNVEUwTGpFeE15MHVNalF5TGpFek5TMHVNek11TVRNMWFDMHVNREF5WXkwdU1EZzRJREF0TGpJeE55MHVNREl5TFM0ek15MHVNVE0xTFRFdU1ERXlMVEV1TURFdE1TNDFOVFl0TWk0eE9EY3RNUzQyTnpVdE1pNDFNVFF0TGpBeE9DMHVNRFE0TFM0d016VXRMakV3TXkwdU1ETTFMUzR4T0NBd0xTNHlOaTR5TVRJdExqUTBOeTQwTXpRdExqUTBOeTR4T0RNZ01DQXVOekExTGpBMU5pQXhMall3TXk0d05UWm9MakF3TmlJdlBqeHdZWFJvSUdROUlrMHlNeTR4TmpRZ05TNDRPVFJqTFM0d09EUXRMakEyTmkwdU1UZzBMUzR4TFM0eU9EWXRMakV0TGpBMU55QXdMUzR4TVRRdU1ERXRMakUzTGpBek1pMHhMakF4T0M0MExUSXVNRFV6TGpjd05DMHlMams1T0M0NU15MHVNVGt5TGpBME5pMHVNelE0TGpFNU15MHVOREV6TGpNM0xTNDJPRGdnTVM0NU16VXRNaTQwTkRZZ015NDNOemN0TkM0eU9TQTFMamN3T0MwdU1EYzBMakEzTWkwdU1UYzRMakUyTnkwdU16UTJMakUyTnkwdU1UWTRJREF0TGpJM0xTNHdPUzB1TXpRMkxTNHhOall0TVM0NE5EUXRNUzQ1TXkwekxqWXRNeTQzTmpjdE5DNHlPUzAxTGpjd01pMHVNRFkwTFM0eE56WXRMakl5TFM0ek1qTXRMalF4TXkwdU16Y3RMamswTkMwdU1qSTFMVEV1T1RneUxTNDFOQzB6TFM0NU5DMHVNRFUwTFM0d01pMHVNVEV0TGpBekxTNHhOamd0TGpBekxTNHhNRElnTUMwdU1pNHdNekl0TGpJNE5TNHdPVGd0TGpFekxqRXdNaTB1TVRrNExqSTJPQzB1TVRnekxqUXlOeTR4TlRZZ01TNDJNUzQzTnpZZ015NHhPRE1nTVM0NE9UUWdOQzQ0TXlBeExqQTFJREV1TlRVZ01pNDBNaklnTWk0NU9EWWdNeTQzTkRjZ05DNHpOek1nTWk0ME5ETWdNaTQxTlRjZ05DNDNOU0EwTGprM01pQTBMamsxTWlBM0xqYzNNaTR3TVRndU1qVXlMakl5TXk0ME5EZ3VORGN1TkRRNGFESXVPRGRqTGpFeklEQWdMakkxTWkwdU1EVTFMak0wTWkwdU1UVXVNRGczTFM0d09UY3VNVE0wTFM0eU1qWXVNVEk0TFM0ek5UZ3RMakE1TFRFdU9EY3lMUzQzTnpjdE15NDNORGN0TWk0d09UZ3ROUzQzTXkwdU1qYzNMUzQwTVRjdExqVTNOQzB1T0RJeUxTNDRPRFF0TVM0eU1UZ3RMakV5TmkwdU1UWXRMakEzTkMwdU16WXVNREl6TFM0ME5qSnNMakk0TmkwdU16QXlZekV1TXpJMkxURXVNemczSURJdU5qazJMVEl1T0RJeUlETXVOelE0TFRRdU16Y3lJREV1TVRFNExURXVOalE0SURFdU56TTNMVE11TWpFeUlERXVPRGt6TFRRdU9ESXVNREUyTFM0eE5qTXRMakExTmkwdU16TTFMUzR4T0RNdExqUXpOQ0l2UGp4d1lYUm9JR1E5SWsweE1TNDFPRElnTVRjdU5qazRZeTB1TURZNExTNHdOekl0TGpFMk9DMHVNVFEzTFM0ek16VXRMakUwTnkwdU1qRTBJREF0TGpNMExqRTFNeTB1TXprdU1qSTRMVEV1TWpBMklERXVPRGcxTFRFdU9ETWdNeTQyTmpRdE1TNDVNVFlnTlM0ME5USXRMakF3TlM0eE16SXVNRFF5TGpJMk15NHhNeTR6Tmk0d09TNHdPVFV1TWpFMUxqRTFMak0wTkM0eE5XZ3lMamcyTldNdU1qUTFJREFnTGpRMU15MHVNVGs0TGpRM0xTNDBOUzR3TmpNdExqZzJNaTR6TWpVdE1TNDNNall1T0MweUxqWXpNeTR4TmpVdExqTXhOQzR3TXpjdExqWXhMUzR3TlRndExqY3pOUzB1TkRjeUxTNDJNeTB4TGpBMUxURXVNekF5TFRFdU9URXRNaTR5TWpRaUx6NDhMMmMrUEdjK1BIQmhkR2dnWkQwaVRUTTBMakEzSURZdU5EVTNZeTB1TURVM0xTNHhPRFl0TGpJeU5TMHVNekV6TFM0ME1UVXRMak14TTJndE5DNHdOamhqTFM0eE9TQXdMUzR6TlRndU1USTNMUzQwTVRVdU16RXpUREkwTGpBMElESXpMakUyWXkwdU1EUXVNVE0yTFM0d01UZ3VNamcwTGpBMk5TNDBMakE0TWk0eE1UUXVNakV5TGpFNE1pNHpOUzR4T0RKb01pNDRZeTR4T1RRZ01DQXVNelkwTFM0eE15NDBNaTB1TXpKc015NDFNek10TVRJdU1qZzFjeTR3T1RJdExqTXdOeTQwTVRJdExqTXdOMk11TXpJMklEQWdMalF3TlM0ek1pNDBNRFV1TXpKc01TNDJOQ0ExTGpneU5FZ3pNUzQwTm1NdExqRTVOU0F3TFM0ek5qY3VNVE16TFM0ME1pNHpNalpzTFM0Mk16SWdNaTR6TURkakxTNHdNemN1TVRNMUxTNHdNUzR5T0M0d056TXVNemt1TURneUxqRXhNaTR5TVM0eE56Z3VNelEzTGpFM09HZ3pMamMxYkM0NU9TQXpMakkxTTJNdU1EVTFMakU0Tmk0eU1qUXVNekUwTGpReE5TNHpNVFJvTWk0NFl5NHhOQ0F3SUM0eU55MHVNRFk0TGpNMU15MHVNVGd6TGpBNE1pMHVNVEUyTGpFd05TMHVNalkwTGpBMk5DMHVORXd6TkM0d055QTJMalExTnlJdlBqeHdZWFJvSUdROUlrMDBOaTR5TnlBeU1TNHhOamhqTFM0d01pMHVNVEkxTFM0d09EY3RMakl6TmkwdU1Ua3RMak13TmkwdU1UQXlMUzR3TnkwdU1qSTNMUzR3T1RJdExqTTBOeTB1TURZeUxTNDBOVFF1TVRFMkxTNDRPUzR4T0MweExqSXlOaTR4T0MwdU5qTTNJREF0TGpreUxTNHlPRE10TGpreUxTNDVNbll0Tmk0MGFESXVOV011TWpRZ01DQXVORE0xTFM0eUxqUXpOUzB1TkRRMmRpMHhMams1Tm1Nd0xTNHlORGN0TGpFNU5TMHVORFEzTFM0ME16WXRMalEwTjJndE1pNDFWamN1Tmpsak1DMHVNVE10TGpBMU5TMHVNalV6TFM0eE5TMHVNek00TFM0d09UVXRMakE0TlMwdU1qSXlMUzR4TWpRdExqTTBOaTB1TVRBMmJDMHlMalUzTkM0ek5qaGpMUzR5TVRZdU1ETXRMak0zTmk0eU1pMHVNemMyTGpRME0zWXlMamN4TkdndE1TNDBNREpqTFM0eU5DQXdMUzQwTXpZdU1pMHVORE0yTGpRME9IWXhMams1Tm1Nd0lDNHlORGN1TVRrMUxqUTBOeTQwTXpZdU5EUTNhREV1TkRBeWRqWXVPREUwWXpBZ01pNHpOQ0F4TGpFNU9DQXpMalV5TmlBekxqVTJJRE11TlRJMkxqWTNOQ0F3SURFdU9ERTJMUzR4TmlBeUxqVTJNeTB1TkRJMUxqRTVPQzB1TURjdU16SXRMakkzTnk0eU9EZ3RMalE1YkMwdU1qZ3RNUzQ1TVRjaUx6NDhjR0YwYUNCa1BTSk5OVEV1TURNMklEVXVPREU0U0RRNExqUTJZeTB1TWpRZ01DMHVORE0xTGpJdExqUXpOUzQwTkRoMk1UY3VNREk0WXpBZ0xqSTBPQzR4T1RZdU5EUTRMalF6Tmk0ME5EaG9NaTQxTnpaakxqSTBJREFnTGpRek5TMHVNaTQwTXpVdExqUTBPRlkyTGpJMk5tTXdMUzR5TkRndExqRTVOQzB1TkRRNExTNDBNelF0TGpRME9DSXZQanh3WVhSb0lHUTlJazAxT1M0ek9ETWdNVGd1T1RnMmRqRXVOVFUyWXkwdU5ESTNMakl6TXkweExqSXVOVFk0TFRJdU1UTXVOVFk0TFM0NE1ETWdNQzB4TGpBME55MHVNalV0TVM0d05EY3RNUzR3TnpVZ01DMHVPQzR4TlMweExqQTFJREV1TURrM0xURXVNRFZvTWk0d09IcHRMVEV1TnpJNExUZ3VORGMwWXkweExqTXdNeUF3TFRJdU9Ea3VNakV0TkM0d05DNDFNemd0TGpJeE5TNHdOaTB1TXpVdU1qYzNMUzR6TVRRdU5UQXliQzR6TURJZ01TNDVNVGhqTGpBeUxqRXlMakE0TkM0eU1qWXVNVGd1TWprMkxqQTVOeTR3Tnk0eU1UY3VNRGsxTGpNek5DNHdOeklnTVM0d055MHVNakUwSURJdU1UYzFMUzR6TXpJZ015NHhNUzB1TXpNeUlERXVPRFlnTUNBeUxqRTFOeTQwTURNZ01pNHhOVGNnTVM0Mk9UVjJNUzR4TWpob0xUSXVOelpqTFRJdU5qTTNJREF0TXk0M05qTWdNUzR4TVRZdE15NDNOak1nTXk0M016TWdNQ0F5TGpVd05DQXhMakl6TkNBekxqazBJRE11TXpnMUlETXVPVFFnTVM0eU5EVWdNQ0F5TGpRNUxTNHpOVFFnTXk0MU16Z3RNUzR3TUROc0xqRXhMalF4TldNdU1EVXlMakU1TlM0eU1qUXVNek11TkRJdU16Tm9NaTR3T0dNdU1qUWdNQ0F1TkRNMkxTNHlMalF6TmkwdU5EUTRkaTA0TGpBMFl6QXRNeTQwTlRjdE1TNDBNRE10TkM0M05ESXROUzR4TnpVdE5DNDNOREo2SWk4K1BIQmhkR2dnWkQwaVRUWTVMakk0SURFMUxqY3hOR010TVM0MU9UZ3RMalF5TXkweExqVTVPQzB1TkRRdE1TNDFPVGd0TVM0ek5DQXdMUzQyTURRdU1EWXRMamc1TkNBeExqTTNOUzB1T0RrMExqa3dNaUF3SURJdU1qTXlMakUyTWlBekxqQTROaTR6TVRJdU1URTRMakF5TGpJek9DMHVNREV1TXpNMExTNHdPREl1TURrMUxTNHdOek11TVRVNExTNHhPRE11TVRjeUxTNHpNRFJzTGpJME1pMHlMakF4TkdNdU1ESTNMUzR5TWpjdExqRXhOaTB1TkRRdExqTXpOQzB1TkRrdExqazRPQzB1TWpNM0xUSXVNell5TFM0ek9TMHpMalV0TGpNNUxUTXVPVFlnTUMwMExqYzNJREV1TlRNMExUUXVOemNnTXk0NE16WWdNQ0F5TGpVek1pNDBOVGNnTXk0ek9ETWdNeTQwT1NBMExqRTBJRElnTGpRNU5TQXlJQzQ0TmlBeUlERXVORGNnTUNBdU56azRMUzR3TmpnZ01TNHdOelF0TVM0ek56WWdNUzR3TnpRdE1TNHdOVE1nTUMweUxqSTNMUzR4TmpndE15NHpORE10TGpRMk1pMHVNVEl0TGpBek1pMHVNalEwTFM0d01USXRMak0wTnk0d05UVXRMakV3TXk0d05qWXRMakUzTXk0eE56UXRMakU1TlM0eU9UZHNMUzR6TkNBeExqa3hZeTB1TURRdU1qSXlMakE0Tmk0ME16Z3VNamswTGpVd055QXhMakUzTGpNNE9DQXlMamcwTnk0Mk5pQTBMakE0TGpZMklETXVPRE0xSURBZ05DNDJNaTB4TGpZME55QTBMall5TFRRdU1USWdNQzB5TGpjekxTNDBOekl0TXk0eU16Y3RNeTQ0T1RJdE5DNHhOallpTHo0OGNHRjBhQ0JrUFNKTk56a3VNakkzSURFMUxqY3hOR010TVM0MU9UY3RMalF5TXkweExqVTVOeTB1TkRRdE1TNDFPVGN0TVM0ek5DQXdMUzQyTURRdU1EVTRMUzQ0T1RRZ01TNHpOelF0TGpnNU5DNDVNRElnTUNBeUxqSXpNaTR4TmpJZ015NHdPRGN1TXpFeUxqRXhPQzR3TWk0eU5DMHVNREV1TXpNMExTNHdPREl1TURrMkxTNHdOek11TVRVNExTNHhPRE11TVRjekxTNHpNRFJzTGpJME1pMHlMakF4TkdNdU1ESTNMUzR5TWpjdExqRXhOeTB1TkRRdExqTXpOQzB1TkRrdExqazRPQzB1TWpNM0xUSXVNell6TFM0ek9TMHpMalV3TWkwdU16a3RNeTQ1TmlBd0xUUXVOemNnTVM0MU16UXROQzQzTnlBekxqZ3pOaUF3SURJdU5UTXlMalExT0NBekxqTTRNeUF6TGpRNU15QTBMakUwSURFdU9UazNMalE1TlNBeExqazVOeTQ0TmlBeExqazVOeUF4TGpRM0lEQWdMamM1T0MwdU1EWTJJREV1TURjMExURXVNemMwSURFdU1EYzBMVEV1TURVeklEQXRNaTR5TnpJdExqRTJPQzB6TGpNME5DMHVORFl5TFM0eE1UY3RMakF6TWkwdU1qUXpMUzR3TVRJdExqTTBOUzR3TlRVdExqRXdNaTR3TmpZdExqRTNNeTR4TnpRdExqRTVOUzR5T1Rkc0xTNHpOQ0F4TGpreFl5MHVNRFF1TWpJeUxqQTROaTQwTXpndU1qazBMalV3TnlBeExqRTJPQzR6T0RnZ01pNDRORGN1TmpZZ05DNHdPQzQyTmlBekxqZ3pOQ0F3SURRdU5qSXRNUzQyTkRjZ05DNDJNaTAwTGpFeUlEQXRNaTQzTXkwdU5EY3pMVE11TWpNM0xUTXVPRGt6TFRRdU1UWTJJaTgrUEhCaGRHZ2daRDBpVFRnM0xqVTJJRFV1T0RFNGFDMHlMalUzTm1NdExqSTBJREF0TGpRek5pNHlMUzQwTXpZdU5EUTNWamd1Tm1Nd0lDNHlORGd1TVRrMkxqUTBPQzQwTXpZdU5EUTRhREl1TlRjMVl5NHlOQ0F3SUM0ME16VXRMakl1TkRNMUxTNDBORGRXTmk0eU5qWmpNQzB1TWpRM0xTNHhPVFl0TGpRME55MHVORE0yTFM0ME5EY2lMejQ4Y0dGMGFDQmtQU0pOT0RjdU5UWWdNVEF1Tnpkb0xUSXVOVGMyWXkwdU1qUWdNQzB1TkRNMkxqSXRMalF6Tmk0ME5EaDJNVEl1TURjMll6QWdMakkwT0M0eE9UWXVORFE0TGpRek5pNDBORGhvTWk0MU56VmpMakkwSURBZ0xqUXpOUzB1TWk0ME16VXRMalEwT0ZZeE1TNHlNVGhqTUMwdU1qUTNMUzR4T1RZdExqUTBOeTB1TkRNMkxTNDBORGNpTHo0OGNHRjBhQ0JrUFNKTk9UVXVPVGNnTVRndU9UZzJkakV1TlRVMll5MHVOREkyTGpJek15MHhMakl1TlRZNExUSXVNVE11TlRZNExTNDRNRElnTUMweExqQTBOeTB1TWpVdE1TNHdORGN0TVM0d056VWdNQzB1T0M0eE5USXRNUzR3TlNBeExqQTVPQzB4TGpBMWFESXVNRGg2YlMweExqY3lOeTA0TGpRM05HTXRNUzR6TURNZ01DMHlMamc1TGpJeExUUXVNRFF1TlRNNExTNHlNVFV1TURZdExqTTFMakkzTnkwdU16RTFMalV3TW13dU16QXlJREV1T1RFNFl5NHdNaTR4TWk0d09EUXVNakkyTGpFNExqSTVOaTR3T1RjdU1EY3VNakUzTGpBNU5TNHpNek11TURjeUlERXVNRGN0TGpJeE5DQXlMakUzTmkwdU16TXlJRE11TVRFeUxTNHpNeklnTVM0NE5UZ2dNQ0F5TGpFMU5TNDBNRE1nTWk0eE5UVWdNUzQyT1RWMk1TNHhNamhvTFRJdU56WmpMVEl1TmpNM0lEQXRNeTQzTmpJZ01TNHhNVFl0TXk0M05qSWdNeTQzTXpNZ01DQXlMalV3TkNBeExqSXpOQ0F6TGprMElETXVNemcxSURNdU9UUWdNUzR5TkRRZ01DQXlMalE1TFM0ek5UUWdNeTQxTXpndE1TNHdNRE5zTGpFeExqUXhOV011TURVekxqRTVOUzR5TWpVdU16TXVOREl1TXpOb01pNHdPR011TWpRZ01DQXVORE0zTFM0eUxqUXpOeTB1TkRRNGRpMDRMakEwWXpBdE15NDBOVGN0TVM0ME1ETXROQzQzTkRJdE5TNHhOelF0TkM0M05ESjZJaTgrUEhCaGRHZ2daRDBpVFRFd09DNDVNemdnTVRBdU5URXlZeTB4TGpNMk5DQXdMVE11TWpJekxqUTRNeTAwTGpjNU15QXhMakkwYkMwdU1UZzRMUzQyTm1NdExqQTFOQzB1TVRrdExqSXlOUzB1TXpJdExqUXhPQzB1TXpKb0xURXVPVEE0WXkwdU1qUWdNQzB1TkRNMkxqSXRMalF6Tmk0ME5EWjJNVEl1TURjMll6QWdMakkwT0M0eE9UWXVORFE0TGpRek5pNDBORGhvTWk0MU56VmpMakkwSURBZ0xqUXpOaTB1TWk0ME16WXRMalEwT0ZZeE5DNDVNMk11T1RJM0xTNDBPRFFnTWk0eU15MHVPVGcwSURNdU1ERXRMams0TkM0MU9DQXdJQzQzT1RZdU1qSXVOemsyTGpneE5uWTRMalV6TW1Nd0lDNHlORGd1TVRrMUxqUTBPQzQwTXpVdU5EUTRhREl1TlRjMVl5NHlOQ0F3SUM0ME16VXRMakl1TkRNMUxTNDBORGgyTFRndU9EWTRZekF0TWk0MU9UY3RMams1TkMwekxqa3hOQzB5TGprMU55MHpMamt4TkNJdlBqd3ZaejQ4TDJjK1BDOXpkbWMrKSBjZW50ZXIgYm90dG9tIG5vLXJlcGVhdDsKICAgIGJhY2tncm91bmQtc2l6ZTogMTE0cHggMjRweAp9CgojZm9vdGVyLWxvZ28gewogICAgYmFja2dyb3VuZDogI2Y1ZjVmNTsgLyogc2NyZWVuIG91dCB0aGUgYmFja2dyb3VuZCBpbWFnZSBvbiB0aGUgZm9vdGVyIHdoZW4gdGhlIGxvZ28gaXMgcHJlc2VudCwgc28gaXQgZG9lc24ndCBtZXNzIHVwIHRyYW5zcGFyZW50IGFyZWFzICovCiAgICBwb3NpdGlvbjogcmVsYXRpdmU7CiAgICBib3R0b206IC0yMXB4OyAvKiBtYXJnaW4gcGx1cyAxIHB4IHRvIHR3ZWFrIGZvciBpbWFnZSAqLwp9CgojZm9vdGVyLWxvZ28gYSB7CiAgICBkaXNwbGF5OiBibG9jazsKICAgIGhlaWdodDogMjRweDsgLyogbWF0Y2ggaW1hZ2UgaGVpZ2h0ICovCiAgICBtYXJnaW46IDAgYXV0bzsKICAgIHRleHQtYWxpZ246IGxlZnQ7CiAgICB0ZXh0LWluZGVudDogLTk5OTllbTsKICAgIHdpZHRoOiAxMTRweDsgLyogbWF0Y2ggaW1hZ2Ugd2lkdGggKi8KfQojZm9vdGVyLWxvZ28gYTpmb2N1cywKI2Zvb3Rlci1sb2dvIGE6aG92ZXIsCiNmb290ZXItbG9nbyBhOmFjdGl2ZSB7CiAgICBiYWNrZ3JvdW5kOiB1cmwoZGF0YTppbWFnZS9zdmcreG1sO2Jhc2U2NCxQSE4yWnlCM2FXUjBhRDBpTVRFMElpQm9aV2xuYUhROUlqSTBJaUIyYVdWM1FtOTRQU0l3SURBZ01URTBJREkwSWlCNGJXeHVjejBpYUhSMGNEb3ZMM2QzZHk1M015NXZjbWN2TWpBd01DOXpkbWNpUGp4blBqeG5JR1pwYkd3OUlpTTFRa0UxUTBVaVBqeHdZWFJvSUdROUlrMHlOeTR5TURZdU1qWTFZeTB1TVRFeUxTNHhOVFF0TGpJNU1pMHVNalEzTFM0ME9DMHVNalEzTFM0d09UY2dNQzB1TVRreUxqQXlOUzB1TWpjMkxqQTNNaTB6TGpVeE55QXhMamszTmkwM0xqVTVOQ0F6TGpBeUxURXhMamM1SURNdU1ESXlMVFF1TVRrMklEQXRPQzR5TnpJdE1TNHdORFl0TVRFdU56a3RNeTR3TWkwdU1EZ3pMUzR3TlMwdU1UYzRMUzR3TnpRdExqSTNOUzB1TURjMExTNHhPRGdnTUMwdU16WTRMakE1TXkwdU5EZ3VNalEzTFM0eE5qWXVNakkzTFM0eE55NDJNRGd1TVRFeUxqZ3pOU0F4TGpjMklERXVNelE0SURNdU56QXpJREl1TkNBMUxqYzNJRE11TVRJZ01pNHhORFF1TnpRMklEUXVNemcySURFdU1USTFJRFl1TmpZeklERXVNVEkySURJdU1qYzRJREFnTkM0MU1pMHVNemdnTmk0Mk5qTXRNUzR4TWpZZ01pNHdOamd0TGpjeUlEUXVNREV0TVM0M056SWdOUzQzTnpNdE15NHhNaTR5T0MwdU1qSTNMakkzTmkwdU5qQTRMakV4TFM0NE16VWlMejQ4Y0dGMGFDQmtQU0pOTVRRdU5qWXpJRGN1TXpNMll5NDVJREFnTVM0ME1pMHVNRFUxSURFdU5qQTBMUzR3TlRVdU1qSXlJREFnTGpRek5DNHhPRGd1TkRNMExqUTFJREFnTGpBM05TMHVNREUyTGpFekxTNHdNelF1TVRjNExTNHhNaTR6TWpjdExqWTJNeUF4TGpVd05DMHhMalkzTkNBeUxqVXhOQzB1TVRFMExqRXhNeTB1TWpReUxqRXpOUzB1TXpNdU1UTTFhQzB1TURBeVl5MHVNRGc0SURBdExqSXhOeTB1TURJeUxTNHpNeTB1TVRNMUxURXVNREV5TFRFdU1ERXRNUzQxTlRZdE1pNHhPRGN0TVM0Mk56VXRNaTQxTVRRdExqQXhPQzB1TURRNExTNHdNelV0TGpFd015MHVNRE0xTFM0eE9DQXdMUzR5Tmk0eU1USXRMalEwTnk0ME16UXRMalEwTnk0eE9ETWdNQ0F1TnpBMUxqQTFOaUF4TGpZd015NHdOVFpvTGpBd05pSXZQanh3WVhSb0lHUTlJazB5TXk0eE5qUWdOUzQ0T1RSakxTNHdPRFF0TGpBMk5pMHVNVGcwTFM0eExTNHlPRFl0TGpFdExqQTFOeUF3TFM0eE1UUXVNREV0TGpFM0xqQXpNaTB4TGpBeE9DNDBMVEl1TURVekxqY3dOQzB5TGprNU9DNDVNeTB1TVRreUxqQTBOaTB1TXpRNExqRTVNeTB1TkRFekxqTTNMUzQyT0RnZ01TNDVNelV0TWk0ME5EWWdNeTQzTnpjdE5DNHlPU0ExTGpjd09DMHVNRGMwTGpBM01pMHVNVGM0TGpFMk55MHVNelEyTGpFMk55MHVNVFk0SURBdExqSTNMUzR3T1MwdU16UTJMUzR4TmpZdE1TNDRORFF0TVM0NU15MHpMall0TXk0M05qY3ROQzR5T1MwMUxqY3dNaTB1TURZMExTNHhOell0TGpJeUxTNHpNak10TGpReE15MHVNemN0TGprME5DMHVNakkxTFRFdU9UZ3lMUzQxTkMwekxTNDVOQzB1TURVMExTNHdNaTB1TVRFdExqQXpMUzR4TmpndExqQXpMUzR4TURJZ01DMHVNaTR3TXpJdExqSTROUzR3T1RndExqRXpMakV3TWkwdU1UazRMakkyT0MwdU1UZ3pMalF5Tnk0eE5UWWdNUzQyTVM0M056WWdNeTR4T0RNZ01TNDRPVFFnTkM0NE15QXhMakExSURFdU5UVWdNaTQwTWpJZ01pNDVPRFlnTXk0M05EY2dOQzR6TnpNZ01pNDBORE1nTWk0MU5UY2dOQzQzTlNBMExqazNNaUEwTGprMU1pQTNMamMzTWk0d01UZ3VNalV5TGpJeU15NDBORGd1TkRjdU5EUTRhREl1T0RkakxqRXpJREFnTGpJMU1pMHVNRFUxTGpNME1pMHVNVFV1TURnM0xTNHdPVGN1TVRNMExTNHlNall1TVRJNExTNHpOVGd0TGpBNUxURXVPRGN5TFM0M056Y3RNeTQzTkRjdE1pNHdPVGd0TlM0M015MHVNamMzTFM0ME1UY3RMalUzTkMwdU9ESXlMUzQ0T0RRdE1TNHlNVGd0TGpFeU5pMHVNVFl0TGpBM05DMHVNell1TURJekxTNDBOakpzTGpJNE5pMHVNekF5WXpFdU16STJMVEV1TXpnM0lESXVOamsyTFRJdU9ESXlJRE11TnpRNExUUXVNemN5SURFdU1URTRMVEV1TmpRNElERXVOek0zTFRNdU1qRXlJREV1T0RrekxUUXVPREl1TURFMkxTNHhOak10TGpBMU5pMHVNek0xTFM0eE9ETXRMalF6TkNJdlBqeHdZWFJvSUdROUlrMHhNUzQxT0RJZ01UY3VOams0WXkwdU1EWTRMUzR3TnpJdExqRTJPQzB1TVRRM0xTNHpNelV0TGpFME55MHVNakUwSURBdExqTTBMakUxTXkwdU16a3VNakk0TFRFdU1qQTJJREV1T0RnMUxURXVPRE1nTXk0Mk5qUXRNUzQ1TVRZZ05TNDBOVEl0TGpBd05TNHhNekl1TURReUxqSTJNeTR4TXk0ek5pNHdPUzR3T1RVdU1qRTFMakUxTGpNME5DNHhOV2d5TGpnMk5XTXVNalExSURBZ0xqUTFNeTB1TVRrNExqUTNMUzQwTlM0d05qTXRMamcyTWk0ek1qVXRNUzQzTWpZdU9DMHlMall6TXk0eE5qVXRMak14TkM0d016Y3RMall4TFM0d05UZ3RMamN6TlMwdU5EY3lMUzQyTXkweExqQTFMVEV1TXpBeUxURXVPVEV0TWk0eU1qUWlMejQ4TDJjK1BHY2dabWxzYkQwaUl6STNORGszTUNJK1BIQmhkR2dnWkQwaVRUTTBMakEzSURZdU5EVTNZeTB1TURVM0xTNHhPRFl0TGpJeU5TMHVNekV6TFM0ME1UVXRMak14TTJndE5DNHdOamhqTFM0eE9TQXdMUzR6TlRndU1USTNMUzQwTVRVdU16RXpUREkwTGpBMElESXpMakUyWXkwdU1EUXVNVE0yTFM0d01UZ3VNamcwTGpBMk5TNDBMakE0TWk0eE1UUXVNakV5TGpFNE1pNHpOUzR4T0RKb01pNDRZeTR4T1RRZ01DQXVNelkwTFM0eE15NDBNaTB1TXpKc015NDFNek10TVRJdU1qZzFjeTR3T1RJdExqTXdOeTQwTVRJdExqTXdOMk11TXpJMklEQWdMalF3TlM0ek1pNDBNRFV1TXpKc01TNDJOQ0ExTGpneU5FZ3pNUzQwTm1NdExqRTVOU0F3TFM0ek5qY3VNVE16TFM0ME1pNHpNalpzTFM0Mk16SWdNaTR6TURkakxTNHdNemN1TVRNMUxTNHdNUzR5T0M0d056TXVNemt1TURneUxqRXhNaTR5TVM0eE56Z3VNelEzTGpFM09HZ3pMamMxYkM0NU9TQXpMakkxTTJNdU1EVTFMakU0Tmk0eU1qUXVNekUwTGpReE5TNHpNVFJvTWk0NFl5NHhOQ0F3SUM0eU55MHVNRFk0TGpNMU15MHVNVGd6TGpBNE1pMHVNVEUyTGpFd05TMHVNalkwTGpBMk5DMHVORXd6TkM0d055QTJMalExTnlJdlBqeHdZWFJvSUdROUlrMDBOaTR5TnlBeU1TNHhOamhqTFM0d01pMHVNVEkxTFM0d09EY3RMakl6TmkwdU1Ua3RMak13TmkwdU1UQXlMUzR3TnkwdU1qSTNMUzR3T1RJdExqTTBOeTB1TURZeUxTNDBOVFF1TVRFMkxTNDRPUzR4T0MweExqSXlOaTR4T0MwdU5qTTNJREF0TGpreUxTNHlPRE10TGpreUxTNDVNbll0Tmk0MGFESXVOV011TWpRZ01DQXVORE0xTFM0eUxqUXpOUzB1TkRRMmRpMHhMams1Tm1Nd0xTNHlORGN0TGpFNU5TMHVORFEzTFM0ME16WXRMalEwTjJndE1pNDFWamN1Tmpsak1DMHVNVE10TGpBMU5TMHVNalV6TFM0eE5TMHVNek00TFM0d09UVXRMakE0TlMwdU1qSXlMUzR4TWpRdExqTTBOaTB1TVRBMmJDMHlMalUzTkM0ek5qaGpMUzR5TVRZdU1ETXRMak0zTmk0eU1pMHVNemMyTGpRME0zWXlMamN4TkdndE1TNDBNREpqTFM0eU5DQXdMUzQwTXpZdU1pMHVORE0yTGpRME9IWXhMams1Tm1Nd0lDNHlORGN1TVRrMUxqUTBOeTQwTXpZdU5EUTNhREV1TkRBeWRqWXVPREUwWXpBZ01pNHpOQ0F4TGpFNU9DQXpMalV5TmlBekxqVTJJRE11TlRJMkxqWTNOQ0F3SURFdU9ERTJMUzR4TmlBeUxqVTJNeTB1TkRJMUxqRTVPQzB1TURjdU16SXRMakkzTnk0eU9EZ3RMalE1YkMwdU1qZ3RNUzQ1TVRjaUx6NDhjR0YwYUNCa1BTSk5OVEV1TURNMklEVXVPREU0U0RRNExqUTJZeTB1TWpRZ01DMHVORE0xTGpJdExqUXpOUzQwTkRoMk1UY3VNREk0WXpBZ0xqSTBPQzR4T1RZdU5EUTRMalF6Tmk0ME5EaG9NaTQxTnpaakxqSTBJREFnTGpRek5TMHVNaTQwTXpVdExqUTBPRlkyTGpJMk5tTXdMUzR5TkRndExqRTVOQzB1TkRRNExTNDBNelF0TGpRME9DSXZQanh3WVhSb0lHUTlJazAxT1M0ek9ETWdNVGd1T1RnMmRqRXVOVFUyWXkwdU5ESTNMakl6TXkweExqSXVOVFk0TFRJdU1UTXVOVFk0TFM0NE1ETWdNQzB4TGpBME55MHVNalV0TVM0d05EY3RNUzR3TnpVZ01DMHVPQzR4TlMweExqQTFJREV1TURrM0xURXVNRFZvTWk0d09IcHRMVEV1TnpJNExUZ3VORGMwWXkweExqTXdNeUF3TFRJdU9Ea3VNakV0TkM0d05DNDFNemd0TGpJeE5TNHdOaTB1TXpVdU1qYzNMUzR6TVRRdU5UQXliQzR6TURJZ01TNDVNVGhqTGpBeUxqRXlMakE0TkM0eU1qWXVNVGd1TWprMkxqQTVOeTR3Tnk0eU1UY3VNRGsxTGpNek5DNHdOeklnTVM0d055MHVNakUwSURJdU1UYzFMUzR6TXpJZ015NHhNUzB1TXpNeUlERXVPRFlnTUNBeUxqRTFOeTQwTURNZ01pNHhOVGNnTVM0Mk9UVjJNUzR4TWpob0xUSXVOelpqTFRJdU5qTTNJREF0TXk0M05qTWdNUzR4TVRZdE15NDNOak1nTXk0M016TWdNQ0F5TGpVd05DQXhMakl6TkNBekxqazBJRE11TXpnMUlETXVPVFFnTVM0eU5EVWdNQ0F5TGpRNUxTNHpOVFFnTXk0MU16Z3RNUzR3TUROc0xqRXhMalF4TldNdU1EVXlMakU1TlM0eU1qUXVNek11TkRJdU16Tm9NaTR3T0dNdU1qUWdNQ0F1TkRNMkxTNHlMalF6TmkwdU5EUTRkaTA0TGpBMFl6QXRNeTQwTlRjdE1TNDBNRE10TkM0M05ESXROUzR4TnpVdE5DNDNOREo2SWk4K1BIQmhkR2dnWkQwaVRUWTVMakk0SURFMUxqY3hOR010TVM0MU9UZ3RMalF5TXkweExqVTVPQzB1TkRRdE1TNDFPVGd0TVM0ek5DQXdMUzQyTURRdU1EWXRMamc1TkNBeExqTTNOUzB1T0RrMExqa3dNaUF3SURJdU1qTXlMakUyTWlBekxqQTROaTR6TVRJdU1URTRMakF5TGpJek9DMHVNREV1TXpNMExTNHdPREl1TURrMUxTNHdOek11TVRVNExTNHhPRE11TVRjeUxTNHpNRFJzTGpJME1pMHlMakF4TkdNdU1ESTNMUzR5TWpjdExqRXhOaTB1TkRRdExqTXpOQzB1TkRrdExqazRPQzB1TWpNM0xUSXVNell5TFM0ek9TMHpMalV0TGpNNUxUTXVPVFlnTUMwMExqYzNJREV1TlRNMExUUXVOemNnTXk0NE16WWdNQ0F5TGpVek1pNDBOVGNnTXk0ek9ETWdNeTQwT1NBMExqRTBJRElnTGpRNU5TQXlJQzQ0TmlBeUlERXVORGNnTUNBdU56azRMUzR3TmpnZ01TNHdOelF0TVM0ek56WWdNUzR3TnpRdE1TNHdOVE1nTUMweUxqSTNMUzR4TmpndE15NHpORE10TGpRMk1pMHVNVEl0TGpBek1pMHVNalEwTFM0d01USXRMak0wTnk0d05UVXRMakV3TXk0d05qWXRMakUzTXk0eE56UXRMakU1TlM0eU9UZHNMUzR6TkNBeExqa3hZeTB1TURRdU1qSXlMakE0Tmk0ME16Z3VNamswTGpVd055QXhMakUzTGpNNE9DQXlMamcwTnk0Mk5pQTBMakE0TGpZMklETXVPRE0xSURBZ05DNDJNaTB4TGpZME55QTBMall5TFRRdU1USWdNQzB5TGpjekxTNDBOekl0TXk0eU16Y3RNeTQ0T1RJdE5DNHhOallpTHo0OGNHRjBhQ0JrUFNKTk56a3VNakkzSURFMUxqY3hOR010TVM0MU9UY3RMalF5TXkweExqVTVOeTB1TkRRdE1TNDFPVGN0TVM0ek5DQXdMUzQyTURRdU1EVTRMUzQ0T1RRZ01TNHpOelF0TGpnNU5DNDVNRElnTUNBeUxqSXpNaTR4TmpJZ015NHdPRGN1TXpFeUxqRXhPQzR3TWk0eU5DMHVNREV1TXpNMExTNHdPREl1TURrMkxTNHdOek11TVRVNExTNHhPRE11TVRjekxTNHpNRFJzTGpJME1pMHlMakF4TkdNdU1ESTNMUzR5TWpjdExqRXhOeTB1TkRRdExqTXpOQzB1TkRrdExqazRPQzB1TWpNM0xUSXVNell6TFM0ek9TMHpMalV3TWkwdU16a3RNeTQ1TmlBd0xUUXVOemNnTVM0MU16UXROQzQzTnlBekxqZ3pOaUF3SURJdU5UTXlMalExT0NBekxqTTRNeUF6TGpRNU15QTBMakUwSURFdU9UazNMalE1TlNBeExqazVOeTQ0TmlBeExqazVOeUF4TGpRM0lEQWdMamM1T0MwdU1EWTJJREV1TURjMExURXVNemMwSURFdU1EYzBMVEV1TURVeklEQXRNaTR5TnpJdExqRTJPQzB6TGpNME5DMHVORFl5TFM0eE1UY3RMakF6TWkwdU1qUXpMUzR3TVRJdExqTTBOUzR3TlRVdExqRXdNaTR3TmpZdExqRTNNeTR4TnpRdExqRTVOUzR5T1Rkc0xTNHpOQ0F4TGpreFl5MHVNRFF1TWpJeUxqQTROaTQwTXpndU1qazBMalV3TnlBeExqRTJPQzR6T0RnZ01pNDRORGN1TmpZZ05DNHdPQzQyTmlBekxqZ3pOQ0F3SURRdU5qSXRNUzQyTkRjZ05DNDJNaTAwTGpFeUlEQXRNaTQzTXkwdU5EY3pMVE11TWpNM0xUTXVPRGt6TFRRdU1UWTJJaTgrUEhCaGRHZ2daRDBpVFRnM0xqVTJJRFV1T0RFNGFDMHlMalUzTm1NdExqSTBJREF0TGpRek5pNHlMUzQwTXpZdU5EUTNWamd1Tm1Nd0lDNHlORGd1TVRrMkxqUTBPQzQwTXpZdU5EUTRhREl1TlRjMVl5NHlOQ0F3SUM0ME16VXRMakl1TkRNMUxTNDBORGRXTmk0eU5qWmpNQzB1TWpRM0xTNHhPVFl0TGpRME55MHVORE0yTFM0ME5EY2lMejQ4Y0dGMGFDQmtQU0pOT0RjdU5UWWdNVEF1Tnpkb0xUSXVOVGMyWXkwdU1qUWdNQzB1TkRNMkxqSXRMalF6Tmk0ME5EaDJNVEl1TURjMll6QWdMakkwT0M0eE9UWXVORFE0TGpRek5pNDBORGhvTWk0MU56VmpMakkwSURBZ0xqUXpOUzB1TWk0ME16VXRMalEwT0ZZeE1TNHlNVGhqTUMwdU1qUTNMUzR4T1RZdExqUTBOeTB1TkRNMkxTNDBORGNpTHo0OGNHRjBhQ0JrUFNKTk9UVXVPVGNnTVRndU9UZzJkakV1TlRVMll5MHVOREkyTGpJek15MHhMakl1TlRZNExUSXVNVE11TlRZNExTNDRNRElnTUMweExqQTBOeTB1TWpVdE1TNHdORGN0TVM0d056VWdNQzB1T0M0eE5USXRNUzR3TlNBeExqQTVPQzB4TGpBMWFESXVNRGg2YlMweExqY3lOeTA0TGpRM05HTXRNUzR6TURNZ01DMHlMamc1TGpJeExUUXVNRFF1TlRNNExTNHlNVFV1TURZdExqTTFMakkzTnkwdU16RTFMalV3TW13dU16QXlJREV1T1RFNFl5NHdNaTR4TWk0d09EUXVNakkyTGpFNExqSTVOaTR3T1RjdU1EY3VNakUzTGpBNU5TNHpNek11TURjeUlERXVNRGN0TGpJeE5DQXlMakUzTmkwdU16TXlJRE11TVRFeUxTNHpNeklnTVM0NE5UZ2dNQ0F5TGpFMU5TNDBNRE1nTWk0eE5UVWdNUzQyT1RWMk1TNHhNamhvTFRJdU56WmpMVEl1TmpNM0lEQXRNeTQzTmpJZ01TNHhNVFl0TXk0M05qSWdNeTQzTXpNZ01DQXlMalV3TkNBeExqSXpOQ0F6TGprMElETXVNemcxSURNdU9UUWdNUzR5TkRRZ01DQXlMalE1TFM0ek5UUWdNeTQxTXpndE1TNHdNRE5zTGpFeExqUXhOV011TURVekxqRTVOUzR5TWpVdU16TXVOREl1TXpOb01pNHdPR011TWpRZ01DQXVORE0zTFM0eUxqUXpOeTB1TkRRNGRpMDRMakEwWXpBdE15NDBOVGN0TVM0ME1ETXROQzQzTkRJdE5TNHhOelF0TkM0M05ESjZJaTgrUEhCaGRHZ2daRDBpVFRFd09DNDVNemdnTVRBdU5URXlZeTB4TGpNMk5DQXdMVE11TWpJekxqUTRNeTAwTGpjNU15QXhMakkwYkMwdU1UZzRMUzQyTm1NdExqQTFOQzB1TVRrdExqSXlOUzB1TXpJdExqUXhPQzB1TXpKb0xURXVPVEE0WXkwdU1qUWdNQzB1TkRNMkxqSXRMalF6Tmk0ME5EWjJNVEl1TURjMll6QWdMakkwT0M0eE9UWXVORFE0TGpRek5pNDBORGhvTWk0MU56VmpMakkwSURBZ0xqUXpOaTB1TWk0ME16WXRMalEwT0ZZeE5DNDVNMk11T1RJM0xTNDBPRFFnTWk0eU15MHVPVGcwSURNdU1ERXRMams0TkM0MU9DQXdJQzQzT1RZdU1qSXVOemsyTGpneE5uWTRMalV6TW1Nd0lDNHlORGd1TVRrMUxqUTBPQzQwTXpVdU5EUTRhREl1TlRjMVl5NHlOQ0F3SUM0ME16VXRMakl1TkRNMUxTNDBORGgyTFRndU9EWTRZekF0TWk0MU9UY3RMams1TkMwekxqa3hOQzB5TGprMU55MHpMamt4TkNJdlBqd3ZaejQ4TDJjK1BDOXpkbWMrKTsKICAgIGJhY2tncm91bmQtc2l6ZTogMTE0cHggMjRweAp9CgoKCkBpbXBvcnQgJy4vaW1wb3J0cy9nbG9iYWwnOwoKLyogQVVJIGF2YXRhciBjb21wb25lbnQgKi8KLmF1aS1hdmF0YXIgewogICAgYm94LXNpemluZzogYm9yZGVyLWJveDsKICAgIGRpc3BsYXk6IGlubGluZS1ibG9jazsKICAgIHZlcnRpY2FsLWFsaWduOiB0ZXh0LWJvdHRvbTsKfQouYXVpLWF2YXRhci1pbm5lciB7CiAgICBkaXNwbGF5OiB0YWJsZS1jZWxsOwogICAgdmVydGljYWwtYWxpZ246IG1pZGRsZTsKfQouYXVpLWF2YXRhciBpbWcgewogICAgYm9yZGVyLXJhZGl1czogM3B4OwogICAgZGlzcGxheTogYmxvY2s7CiAgICBtYXJnaW46IDAgYXV0bzsKICAgIGhlaWdodDogMTAwJTsKICAgIHdpZHRoOiAxMDAlOwp9CgouYXVpLWF2YXRhci14c21hbGwsCi5hdWktYXZhdGFyLXhzbWFsbCAuYXVpLWF2YXRhci1pbm5lciB7CiAgICBoZWlnaHQ6IDE2cHg7CiAgICB3aWR0aDogMTZweDsKfQouYXVpLWF2YXRhci1zbWFsbCwKLmF1aS1hdmF0YXItc21hbGwgLmF1aS1hdmF0YXItaW5uZXIgewogICAgaGVpZ2h0OiAyNHB4OwogICAgd2lkdGg6IDI0cHg7Cn0KLmF1aS1hdmF0YXItbWVkaXVtLAouYXVpLWF2YXRhci1tZWRpdW0gLmF1aS1hdmF0YXItaW5uZXIgewogICAgaGVpZ2h0OiAzMnB4OwogICAgd2lkdGg6IDMycHg7Cn0KLmF1aS1hdmF0YXItbGFyZ2UsCi5hdWktYXZhdGFyLWxhcmdlIC5hdWktYXZhdGFyLWlubmVyIHsKICAgIGhlaWdodDogNDhweDsKICAgIHdpZHRoOiA0OHB4Owp9Ci5hdWktYXZhdGFyLXhsYXJnZSwKLmF1aS1hdmF0YXIteGxhcmdlIC5hdWktYXZhdGFyLWlubmVyIHsKICAgIGhlaWdodDogNjRweDsKICAgIHdpZHRoOiA2NHB4Owp9Ci5hdWktYXZhdGFyLXh4bGFyZ2UsCi5hdWktYXZhdGFyLXh4bGFyZ2UgLmF1aS1hdmF0YXItaW5uZXIgewogICAgaGVpZ2h0OiA5NnB4OwogICAgd2lkdGg6IDk2cHg7Cn0KLmF1aS1hdmF0YXIteHh4bGFyZ2UsCi5hdWktYXZhdGFyLXh4eGxhcmdlIC5hdWktYXZhdGFyLWlubmVyIHsKICAgIGhlaWdodDogMTI4cHg7CiAgICB3aWR0aDogMTI4cHg7Cn0KCi8qIEZvcmNlcyBsYXJnZXIgaW1hZ2VzIHRvIGRvd25zY2FsZSBpbiBJRTExLiAqLwouYXVpLWF2YXRhci14c21hbGwgLmF1aS1hdmF0YXItaW5uZXIgaW1nIHsKICAgIG1heC1oZWlnaHQ6IDE2cHg7CiAgICBtYXgtd2lkdGg6IDE2cHg7Cn0KLmF1aS1hdmF0YXItc21hbGwgLmF1aS1hdmF0YXItaW5uZXIgaW1nIHsKICAgIG1heC1oZWlnaHQ6IDI0cHg7CiAgICBtYXgtd2lkdGg6IDI0cHg7Cn0KLmF1aS1hdmF0YXItbWVkaXVtIC5hdWktYXZhdGFyLWlubmVyIGltZyB7CiAgICBtYXgtaGVpZ2h0OiAzMnB4OwogICAgbWF4LXdpZHRoOiAzMnB4Owp9Ci5hdWktYXZhdGFyLWxhcmdlIC5hdWktYXZhdGFyLWlubmVyIGltZyB7CiAgICBtYXgtaGVpZ2h0OiA0OHB4OwogICAgbWF4LXdpZHRoOiA0OHB4Owp9Ci5hdWktYXZhdGFyLXhsYXJnZSAuYXVpLWF2YXRhci1pbm5lciBpbWcgewogICAgbWF4LWhlaWdodDogNjRweDsKICAgIG1heC13aWR0aDogNjRweDsKfQouYXVpLWF2YXRhci14eGxhcmdlIC5hdWktYXZhdGFyLWlubmVyIGltZyB7CiAgICBtYXgtaGVpZ2h0OiA5NnB4OwogICAgbWF4LXdpZHRoOiA5NnB4Owp9Ci5hdWktYXZhdGFyLXh4eGxhcmdlIC5hdWktYXZhdGFyLWlubmVyIGltZyB7CiAgICBtYXgtaGVpZ2h0OiAxMjhweDsKICAgIG1heC13aWR0aDogMTI4cHg7Cn0KCi5hdWktYXZhdGFyLXh4bGFyZ2UgaW1nLAouYXVpLWF2YXRhci14eHhsYXJnZSBpbWcgewogICAgYm9yZGVyLXJhZGl1czogNXB4Owp9CgovKiBQcm9qZWN0IGF2YXRhcnMgLSBjaXJjdWxhciBhbmQgZGlmZmVyZW50IHNpemVzICovCi5hdWktYXZhdGFyLXByb2plY3QgewogICAgYmFja2dyb3VuZC1jb2xvcjogQGF1aS1hdmF0YXItcHJvamVjdC1iZy1jb2xvcjsKICAgIGJveC1zaGFkb3c6IDAgMCAwIDFweCBAYXVpLWF2YXRhci1wcm9qZWN0LWJvcmRlci1jb2xvcjsKICAgIHBvc2l0aW9uOiByZWxhdGl2ZTsKfQouYXVpLWF2YXRhci1wcm9qZWN0LAouYXVpLWF2YXRhci1wcm9qZWN0IGltZyB7CiAgICBib3JkZXItcmFkaXVzOiAxMDAlOwp9Ci5hdWktYXZhdGFyLXByb2plY3QgaW1nIHsKICAgIGhlaWdodDogYXV0bzsKICAgIG1heC1oZWlnaHQ6IDEwMCU7CiAgICBtYXgtd2lkdGg6IDEwMCU7CiAgICB3aWR0aDogYXV0bzsKfQovKiBUaGUgYmVsb3cgc3R5bGUgYWltcyB0byBtaW5pbWlzZSBhbnkgImhhbG8iIGNhdXNlZCBieSB0aGUgYW50aWFsaWFzaW5nIG9mIHRoZSBpbWFnZSAqLwouYXVpLWF2YXRhci1wcm9qZWN0OmJlZm9yZSB7CiAgICBib3JkZXItcmFkaXVzOiAxMDAlOwogICAgYm9yZGVyOiAxcHggc29saWQgQGF1aS1hdmF0YXItcHJvamVjdC1ib3JkZXItY29sb3I7CiAgICBib3R0b206IC0xcHg7CiAgICBjb250ZW50OiAiIjsKICAgIGxlZnQ6IC0xcHg7CiAgICBwb3NpdGlvbjogYWJzb2x1dGU7CiAgICByaWdodDogLTFweDsKICAgIHRvcDogLTFweDsKfQoKCkBpbXBvcnQgJy4vaW1wb3J0cy9nbG9iYWwnOwoKLyoqCiAqIEFVSSBQYWdlIEhlYWRlcgogKi8KCi5hdWktcGFnZS1oZWFkZXItaW5uZXIgewogICAgYm9yZGVyLXNwYWNpbmc6IDA7CiAgICBib3gtc2l6aW5nOiBib3JkZXItYm94OwogICAgZGlzcGxheTogdGFibGU7CiAgICB0YWJsZS1sYXlvdXQ6IGF1dG87CiAgICB3aWR0aDogMTAwJTsKfQoKLmF1aS1wYWdlLWhlYWRlci1pbWFnZSwKLmF1aS1wYWdlLWhlYWRlci1tYWluLAouYXVpLXBhZ2UtaGVhZGVyLWFjdGlvbnMgewogICAgYm94LXNpemluZzogYm9yZGVyLWJveDsKICAgIGRpc3BsYXk6IHRhYmxlLWNlbGw7CiAgICBtYXJnaW46IDA7CiAgICBwYWRkaW5nOiAwOwogICAgdGV4dC1hbGlnbjogbGVmdDsKICAgIHZlcnRpY2FsLWFsaWduOiB0b3A7Cn0KLyogY29sbGFwc2UgdGhlIGNlbGwgdG8gZml0IGl0cyBjb250ZW50ICovCi5hdWktcGFnZS1oZWFkZXItaW1hZ2UgewogICAgd2hpdGUtc3BhY2U6IG5vd3JhcDsKICAgIHdpZHRoOiAxcHg7Cn0KLmF1aS1wYWdlLWhlYWRlci1tYWluIHsKICAgIHZlcnRpY2FsLWFsaWduOiBtaWRkbGU7Cn0KLmF1aS1wYWdlLWhlYWRlci1pbWFnZSArIC5hdWktcGFnZS1oZWFkZXItbWFpbiB7CiAgICBwYWRkaW5nLWxlZnQ6IEBhdWktZ3JpZDsKfQouYXVpLXBhZ2UtaGVhZGVyLWFjdGlvbnMgewogICAgcGFkZGluZy1sZWZ0OiAoQGF1aS1ncmlkICogMik7CiAgICB0ZXh0LWFsaWduOiByaWdodDsKICAgIHZlcnRpY2FsLWFsaWduOiBtaWRkbGU7Cn0KLmF1aS1wYWdlLWhlYWRlci1tYWluID4gaDEsCi5hdWktcGFnZS1oZWFkZXItbWFpbiA+IGgyLAouYXVpLXBhZ2UtaGVhZGVyLW1haW4gPiBoMywKLmF1aS1wYWdlLWhlYWRlci1tYWluID4gaDQsCi5hdWktcGFnZS1oZWFkZXItbWFpbiA+IGg1LAouYXVpLXBhZ2UtaGVhZGVyLW1haW4gPiBoNiB7CiAgICBtYXJnaW46IDA7Cn0KLmF1aS1wYWdlLWhlYWRlci1hY3Rpb25zID4gLmF1aS1idXR0b25zIHsKICAgIC8qIHNwYWNlcyBvdXQgYnV0dG9uIGdyb3VwcyB3aGVuIHRoZXkgd3JhcCB0byAyIGxpbmVzICovCiAgICBtYXJnaW4tYm90dG9tOiAoQGF1aS1ncmlkIC8gMik7CiAgICBtYXJnaW4tdG9wOiAoQGF1aS1ncmlkIC8gMik7CiAgICB2ZXJ0aWNhbC1hbGlnbjogdG9wOwogICAgd2hpdGUtc3BhY2U6IG5vd3JhcDsKfQovKiBBdmF0YXIgb3ZlcnJpZGVzICovCi5hdWktcGFnZS1oZWFkZXItaW1hZ2UgLmF1aS1hdmF0YXIgewogICAgdmVydGljYWwtYWxpZ246IHRvcDsKfQoKCgoucGFuZWwsCi5hbGVydFBhbmVsLAouaW5mb1BhbmVsIHsKICAgIGNvbG9yOiAjMzMzOwogICAgcGFkZGluZzogMDsKICAgIG1hcmdpbjogMTBweCAwOwogICAgYm9yZGVyOiAxcHggc29saWQgI2RkZDsKICAgIG92ZXJmbG93OiBoaWRkZW47CiAgICBib3JkZXItcmFkaXVzOiAzcHg7Cn0KCi5hbGVydFBhbmVsLCAuaW5mb1BhbmVsLCAucGFuZWxDb250ZW50IHsKICAgIHBhZGRpbmc6IDEwcHg7Cn0KCi5hbGVydFBhbmVsIHsKICAgIGJvcmRlci1jb2xvcjogI2MwMDsKfQoKLmluZm9QYW5lbCB7CiAgICBib3JkZXItY29sb3I6ICM2OWM7Cn0KCi5wYW5lbEhlYWRlciB7CiAgICBwYWRkaW5nOiAxMHB4OwogICAgYm9yZGVyLWJvdHRvbTogMXB4IHNvbGlkICNkZGQ7CiAgICBiYWNrZ3JvdW5kLWNvbG9yOiAjZjdmN2Y3Owp9CgovKiBiYXNpYyBwYW5lbCAoYmFzaWNwYW5lbC52bWQpIHN0eWxlICovCi5iYXNpY1BhbmVsQ29udGFpbmVyIHsKICAgIGJvcmRlci13aWR0aDogMXB4OwogICAgYm9yZGVyLXN0eWxlOiBzb2xpZDsKICAgIG1hcmdpbi10b3A6IDJweDsKICAgIG1hcmdpbi1ib3R0b206IDhweDsKICAgIHdpZHRoOiAxMDAlOwp9CgouYmFzaWNQYW5lbENvbnRhaW5lcjpmaXJzdC1jaGlsZCB7CiAgICBtYXJnaW4tdG9wOiAwOwp9CgouYmFzaWNQYW5lbFRpdGxlIHsKICAgIHBhZGRpbmc6IDEwcHg7CiAgICBtYXJnaW46IDA7CiAgICBiYWNrZ3JvdW5kLWNvbG9yOiAjZjBmMGYwOwogICAgYm9yZGVyLWJvdHRvbTogMXB4IHNvbGlkICNkZGQ7Cn0KCi5iYXNpY1BhbmVsQm9keSB7CiAgICBwYWRkaW5nOiA1cHg7CiAgICBtYXJnaW46IDA7Cn0KCgovKiBQREwgbWFzdGVyLmNzcyAqLwovKiBHZW5lcmljICovCmZpZWxkc2V0IHsKICAgIGJvcmRlcjogbm9uZTsKICAgIG1hcmdpbjogMDsKICAgIHBhZGRpbmc6IDA7Cn0KCi5zbWFsbHRleHQgewogICAgZm9udC1zaXplOiAxMnB4OwogICAgY29sb3I6ICM3MDcwNzA7Cn0KCiN0aXRsZS10ZXh0IHsKICAgIG1hcmdpbjogMDsKICAgIGZvbnQtc2l6ZTogMjhweDsKfQoKI3RpdGxlLXRleHQgYTpob3ZlcnsKICAgIHRleHQtZGVjb3JhdGlvbjogbm9uZTsKfQoKYm9keS5lcnJvci1wYWdlICNtYWluIHsKICAgIHBhZGRpbmctdG9wOiAwOwp9Cgpib2R5LmVycm9yLXBhZ2UgI21haW4taGVhZGVyIHsKICAgIG1hcmdpbjogMCAtMjBweDsKICAgIHBhZGRpbmc6IDIwcHg7CiAgICBib3JkZXItYm90dG9tOiAxcHggc29saWQgI2NjYzsKICAgIGJhY2tncm91bmQtY29sb3I6ICNmNWY1ZjU7Cn0KCiNtYWluLWhlYWRlciwKI3ByZXZpZXctaGVhZGVyIHsKICAgIG1hcmdpbi1ib3R0b206IDIwcHg7Cn0KLmNvbnRlbnQtdHlwZS1wYWdlICNtYWluICNtYWluLWhlYWRlciwKLmNvbnRlbnQtdHlwZS1ibG9ncG9zdCAjbWFpbiAjbWFpbi1oZWFkZXIgewogICAgbWFyZ2luLXRvcDogLTEwcHg7Cn0KCiNwcmV2aWV3LWhlYWRlciAjdGl0bGUtdGV4dCB7CiAgICBjb2xvcjogIzMzMzsKICAgIG1hcmdpbi10b3A6IDEwcHg7Cn0KCmEgaW1nIHsKICAgIGJvcmRlcjogMDsKfQoKLmhpZGRlbiB7CiAgICBkaXNwbGF5OiBub25lOwp9Ci8qIEVuZCBnZW5lcmljICovCgovKiBNYWluIGJvZHkgKi8KCi8qIGlmIHNwYWNlLWlhIGlzIG9uLCB3ZSB3YW50IHdpZHRoIHRvIGJlIGF1dG8qLwouaWEtc3BsaXR0ZXIgLmF1aS1wYWdlLXBhbmVsIHsKICAgIHdpZHRoOiBhdXRvOwp9Ci8qIGVuZCBtYWluIGJvZHkgKi8KCi8qIEhlYWRlciAqLwojcXVpY2stc2VhcmNoLXN1Ym1pdCB7CiAgICBkaXNwbGF5OiBub25lOwp9CgojcXVpY2stc2VhcmNoIHsKICAgIG1hcmdpbjogMDsKfQoKI3F1aWNrLXNlYXJjaC5xdWljay1zZWFyY2gtbG9hZGluZzphZnRlciB7CiAgICBkaXNwbGF5OiBub25lOwp9CgouYXVpLW5hdi1saW5rIHNwYW4gKyBzcGFuIHsKICAgIG1hcmdpbi1sZWZ0OiA1cHg7Cn0KLyogRW5kIGhlYWRlciAqLwoKLyogR2VuZXJpYyBBZG1pbiBzaWRlYmFyIChvciB2ZXJ0aWNhbCBuYXZpZ2F0aW9uKSAqLwouYXVpLW5hdi12ZXJ0aWNhbCBsaSBhOmxpbmssCi5hdWktbmF2LXZlcnRpY2FsIGxpIGE6Zm9jdXMsCi5hdWktbmF2LXZlcnRpY2FsIGxpIGE6dmlzaXRlZCwKLmF1aS1uYXYtdmVydGljYWwgbGkgYTphY3RpdmUgIHsKICAgIGNvbG9yOiAjNjY2Owp9CgovKiBFbmQgZ2VuZXJpYyBhZG1pbiBzaWRlYmFyL3ZlcnRpY2FsIG5hdiovCgovKiBTcGFjZSBBZG1pbiBzaWRlYmFyICovCi5pbi1wYWdlLW1lbnUgewogIHBhZGRpbmc6IDEwcHg7Cn0KCi5pbi1wYWdlLW1lbnUtY29udGVudCB7CiAgICBib3JkZXItbGVmdDogMXB4IHNvbGlkICNDQ0M7CiAgICBwYWRkaW5nOiAwIDAgMjBweCAxMHB4Owp9CgovKiBFbmQgU3BhY2UgQWRtaW4gc2lkZWJhciAqLwoKLypHbG9iYWwgbWVzc2FnZXMgYWJvdmUgdGhlIGhlYWRlciovCiNtZXNzYWdlQ29udGFpbmVyIHsKICAgIGxpc3Qtc3R5bGUtdHlwZTogbm9uZTsKICAgIG1hcmdpbjogMDsKICAgIHBhZGRpbmc6IDA7Cn0KCiNtZXNzYWdlQ29udGFpbmVyIGxpIHsKICAgIGRpc3BsYXk6IGJsb2NrOwp9CgojbWVzc2FnZUNvbnRhaW5lciAuYXVpLW1lc3NhZ2UgewogICAgbWFyZ2luOiAwOwogICAgYm9yZGVyLXJhZGl1czogMDsKfQovKkVuZCBnbG9iYWwgbWVzc2FnZXMgYWJvdmUgdGhlIGhlYWRlciovCgojYnJlYWRjcnVtYnMgbGkuaGlkZGVuLWNydW1iIHsKICAgIGRpc3BsYXk6IG5vbmU7Cn0KCi8qIE1ldGFkYXRhIHN0dWZmIGp1c3QgYmVsb3cgcGFnZSB0aXRsZSovCi5wYWdlLW1ldGFkYXRhIHVsIHsKICAgIG92ZXJmbG93OiBoaWRkZW47CiAgICBtYXJnaW46IDA7CiAgICBwYWRkaW5nOiAwOwp9CgoucGFnZS1tZXRhZGF0YSB1bCBsaSB7CiAgICBsaXN0LXN0eWxlOiBub25lOwogICAgZmxvYXQ6IGxlZnQ7CiAgICBwYWRkaW5nOiAwIDVweCAwIDA7CiAgICBtYXJnaW46IDA7CiAgICBsaW5lLWhlaWdodDogMS41Owp9CgoucGFnZS1tZXRhZGF0YSB1bCBsaTpmaXJzdC1jaGlsZDpiZWZvcmUgeyAvKiBsZWZ0LW1vc3QgbGlzdCBpdGVtIC0tIG5vIGxlZnQgbWlkZG90ICovCiAgICBkaXNwbGF5OiBub25lOwp9CgoucGFnZS1tZXRhZGF0YSB1bCBsaTpiZWZvcmUgewogICAgY29udGVudDogJ+KAoic7CiAgICBjb2xvcjogIzcwNzA3MDsKICAgIHBhZGRpbmc6IDAgNXB4Owp9CgoucGFnZS1tZXRhZGF0YSB1bCBsaSBpbWcgewogICAgdmVydGljYWwtYWxpZ246IHRleHQtYm90dG9tOwp9CgoucGFnZS1tZXRhZGF0YSB1bCBhLnBhZ2UtdGlueXVybCBzcGFuIHsKICAgIGZsb2F0OiBsZWZ0OwogICAgaGVpZ2h0OiAxNnB4OwogICAgd2lkdGg6IDEwcHg7CiAgICB0ZXh0LWluZGVudDogLTk5OTllbTsKfQoKLnBhZ2UtbWV0YWRhdGEgdWwgYS5hY3Rpb24tdmlldy1hdHRhY2htZW50cyBzcGFuLnBhZ2UtbWV0YWRhdGEtYXR0YWNobWVudHMtY291bnQgewogICAgbWFyZ2luLWxlZnQ6IDNweDsKfQovKiBFbmQgbWV0YWRhdGEgc3R1ZmYqLwoKLyogUGFnZSBjb250ZW50ICovCiNtYWluIHsKICAgIG1hcmdpbjogMDsKICAgIHBhZGRpbmc6IDIwcHg7CiAgICBjbGVhcjogYm90aDsKICAgIG1pbi1oZWlnaHQ6IDYwMHB4Owp9CgovKiBDT05GREVWLTQ4MjUyIFBhZGRpbmcgb24gdGhlIGJsb2cgY29sbGVjdG9yIGFuZCBwYWdlIGNvbGxlY3RvciBwYWdlcyBkaWZmZXIgdG8gY29udGVudCBwYWdlcyAqLwoucGFnZXMtY29sbGVjdG9yLW1vZGUgI21haW4sCi52aWV3LWJsb2dwb3N0cy1tb2RlICNtYWluLAouY29udGVudC10eXBlLXBhZ2UgI21haW4sCi5jb250ZW50LXR5cGUtYmxvZ3Bvc3QgI21haW4gewogICAgcGFkZGluZzogMjBweCA0MHB4Owp9CgovKiBVc2VkIGZvciB0aGVtaW5nIGJhY2tncm91bmQgY292ZXIgaW1hZ2VzIG9ubHkgKi8KI21haW4gLmNvbm5lY3QtdGhlbWUtYmFja2dyb3VuZC1jb3ZlciB7CiAgICBkaXNwbGF5OiBub25lOwp9CgovKiBDU1MgZm9yIHRoZW1lZCBwYWdlICovCiNtYWluLnRoZW1lZCB7CiAgICBwYWRkaW5nOiAwOwogICAgYmFja2dyb3VuZDogbm9uZQp9CgojdGl0bGUtaGVhZGluZyB7CiAgICBtYXJnaW46IDA7CiAgICBwYWRkaW5nOiAwOwp9CiN0aXRsZS10ZXh0IGEgewogICAgY29sb3I6ICMzMzM7Cn0KCi5uYXZCYWNrZ3JvdW5kQm94IHsKICAgIHBhZGRpbmc6IDVweDsKICAgIGZvbnQtc2l6ZTogMjJweDsKICAgIGZvbnQtd2VpZ2h0OiBib2xkOwogICAgdGV4dC1kZWNvcmF0aW9uOiBub25lOwp9Cgouc2ltcGxlLWNvbmZpcm1hdGlvbiA+IGZvcm0gPiAuYnV0dG9ucy1jb250YWluZXIgewogICAgcGFkZGluZy1sZWZ0OiAwOwogICAgbWFyZ2luLXRvcDogMjBweDsKfQoKLyogYXVpLXBhZ2UtbGF5b3V0LmNzcyBtYWtlcyB0aGlzIGNsZWFyLCB3aGljaCBzdHVmZnMgdXAKIHRoZSBtdWx0aS1jb2x1bW4gbGF5b3V0IG9mIHNwYWNlIGFkbWluIHNpZGViYXIgKi8KCiNjb250ZW50IHsKICAgIGNsZWFyOiBub25lICFpbXBvcnRhbnQ7Cn0KCiNjb250ZW50OjpiZWZvcmUgewogICAgZGlzcGxheTogbm9uZSAhaW1wb3J0YW50Owp9CgovKiBlbmQgYXVpLXBhZ2UtbGF5b3V0LmNzcyBvdmVycmlkZXMgKi8KCi8qRW5kIHBhZ2UgY29udGVudCovCgovKiBQYWdlIGZvb3RlciAqLwoKLmF1aS1sYXlvdXQgI2Zvb3RlciAuZm9vdGVyLWJvZHkgPiB1bCA+IGxpLnByaW50LW9ubHkgewogICAgZGlzcGxheTogbm9uZTsKfQovKiBFbmQgcGFnZSBmb290ZXIgKi8KCiNjb20tYXRsYXNzaWFuLWNvbmZsdWVuY2UgLmhpZGRlbiB7CiAgICBkaXNwbGF5OiBub25lOwp9CgovKiBQcm9ncmVzcyBQYWdlICovCgovKiBUaGlzIGRvZXNuJ3QgYW5kIHNob3VsZG4ndCBiZSBhIHRhYmxlICovCgojc3RhdHVzIHsKICAgIG1hcmdpbjogMTBweCAwOwp9Cgojc3RhdHVzIHRhYmxlIHsKICAgIG1hcmdpbjogMTBweCAwOwp9CgojdGFza1Byb2dyZXNzQmFyIHsKICAgIGJhY2tncm91bmQ6ICNlYmYyZjk7CiAgICAvKiBUaGlzIGJvcmRlciByYWRpdXMgd2lsbCBub3QKICAgICAgIHdvcmsgd2hpbGUgaXQncyBzdGlsbCBhIHRhYmxlICovCiAgICBib3JkZXItcmFkaXVzOiAzcHg7Cn0KCiN0YXNrR3JlZW5CYXIgewogICAgYmFja2dyb3VuZDogIzNiN2ZjNDsKfQoKI3N0YXR1cyAuc21hbGx0ZXh0IHsKICAgIHRleHQtYWxpZ246IGNlbnRlcjsKICAgIGNvbG9yOiAjNzA3MDcwOwp9CgojY29udGVudCB7CiAgICAvKiBPdmVycmlkZSBhdWktcGFnZS1sYXlvdXQuY3NzICovCiAgICBwb3NpdGlvbjogc3RhdGljICFpbXBvcnRhbnQ7Cn0KCmhyIHsKICAgIGJvcmRlcjogbm9uZTsKICAgIGJvcmRlci1ib3R0b206IDFweCBzb2xpZCAjY2NjOwp9CgojYmxvZ2xpc3QgewogICAgZGlzcGxheTogbm9uZTsKfQoKLyogQ2hpbGQgUGFnZXMgc3R5bGluZyAobWFpbmx5IGZvciB0aGUgYmVuZWZpdCBvZiBkb2N0aGVtZSkgKi8KLmNoaWxkcmVuLXNob3ctaGlkZS5pY29uIHsKICAgIGRpc3BsYXk6IG5vbmU7Owp9CgouY2hpbGQtZGlzcGxheSB7CiAgICBwYWRkaW5nOiAycHggMnB4IDJweCAxMnB4OwogICAgZGlzcGxheTogYmxvY2s7Cn0KCi8qCiAqIFBhZ2UgaGlzdG9yeQogKiBUaGlzIGlzIHRoZSBjc3MgZm9yIHRoZSB3YXJuaW5nIGF0IHRoZSB0b3Agb2YgdGhlIHBhZ2Ugd2hlbiB2aWV3aW5nIHRoZSBwYWdlIGhpc3RvcnkKICovCiNwYWdlLWhpc3Rvcnktd2FybmluZyB7CiAgICBtYXJnaW4tYm90dG9tOiAyMHB4Owp9CgoucGFnZS1oaXN0b3J5LXZpZXcgYTpiZWZvcmUgewogICAgZGlzcGxheTogaW5saW5lLWJsb2NrOwogICAgY29udGVudDogIsK3IjsKICAgIHBhZGRpbmc6IDAgMTBweDsKfQoKLnBhZ2UtaGlzdG9yeS12aWV3IGE6YmVmb3JlOmhvdmVyIHsKICAgIHRleHQtZGVjb3JhdGlvbjogbm9uZTsKfQoKLnBhZ2UtaGlzdG9yeS12aWV3IGE6Zmlyc3QtY2hpbGQ6YmVmb3JlIHsKICAgIGRpc3BsYXk6IG5vbmU7CiAgICBjb250ZW50OiAnJzsKICAgIHBhZGRpbmc6IDA7Cn0KCi52ZXJzaW9uLW5hdmlnYXRpb24tYmxvY2sgewogICAgcGFkZGluZy10b3A6IDEwcHg7Cn0KCi5jdXJyZW50LXZlcnNpb24tbWFyZ2luIHsKICAgIGRpc3BsYXk6IGlubGluZS1ibG9jazsKICAgIHBhZGRpbmctbGVmdDogMTBweDsKfQoKLmN1cnJlbnQtdmVyc2lvbi1tYXJnaW46Zmlyc3QtY2hpbGQgewogICAgcGFkZGluZzogMDsKfQovKiBFbmQgcGFnZSBoaXN0b3J5ICovCgovKiBBbHRlcm5hdGl2ZSBwYWdlcyAqLwouYWx0ZXJuYXRpdmUtcGFnZS1saXN0IHsKICAgIGxpc3Qtc3R5bGU6IG5vbmU7CiAgICBwYWRkaW5nLWxlZnQ6IDMwcHg7CiAgICBsaW5lLWhlaWdodDogMjBweDsKfQoKLmFsdGVybmF0aXZlLXBhZ2UtbGlzdCAuZXhjZXJwdCB7CiAgICBtYXJnaW4tbGVmdDogMjBweDsKfQovKiBFbmQgYWx0ZXJuYXRpdmUgcGFnZXMgKi8KCi8qIFBlb3BsZSBkaXJlY3RvcnkqLwoucGFnZS1zZWN0aW9uLAoucGFnZS1ncm91cCB7CiAgICBkaXNwbGF5OiB0YWJsZTsKICAgIG1hcmdpbjogMDsKICAgIHBhZGRpbmc6IDA7CiAgICB3aWR0aDogMTAwJTsKfQoKLnBhZ2Utc2VjdGlvbiAucGFnZS1ncm91cCB7CiAgICBkaXNwbGF5OiB0YWJsZS1yb3c7CiAgICBtYXJnaW46IDA7CiAgICBwYWRkaW5nOiAwOwogICAgd2lkdGg6IDEwMCU7Cn0KCi5wYWdlLWl0ZW0gewogICAgZGlzcGxheTogdGFibGUtY2VsbDsKICAgIG1hcmdpbjogMDsKICAgIG1pbi13aWR0aDogMjU2cHg7IC8qIGdyaWQgbWluaW11bSAqLwogICAgcGFkZGluZzogMCAwIDAgMTZweDsKICAgIHZlcnRpY2FsLWFsaWduOiB0b3A7Cn0KCi8qUGVvcGxlIGRpcmVjdG9yeSovCmJvZHkucGVvcGxlIC5kYXNoYm9hcmQgewogICAgbWFyZ2luLXRvcDogMDsKfQpib2R5LnBlb3BsZSAuZGFzaGJvYXJkLWdyb3VwID4gZGl2IHsKICAgIG1hcmdpbi10b3A6IDA7Cn0KCmJvZHkucGVvcGxlICNtYWluIHsKICAgIHBhZGRpbmc6IDA7Cn0KCmJvZHkucGVvcGxlICNtYWluLWhlYWRlciB7CiAgICBwYWRkaW5nOiAyN3B4IDAgMCA1MHB4OwogICAgbWFyZ2luLWJvdHRvbTogMDsKICAgIGJhY2tncm91bmQtY29sb3I6ICNmNWY1ZjU7Cn0KCi5wYWdlY29udGVudC5wZW9wbGUtZGlyZWN0b3J5IHsKICAgIHBhZGRpbmctbGVmdDogMzRweDsKfQoKLnBlb3BsZS1kaXJlY3RvcnkgLmRhc2hib2FyZC1zZWN0aW9uIHsKICAgIGRpc3BsYXk6IHRhYmxlOwogICAgd2lkdGg6IDEwMCU7Cn0KCmJvZHkucGVvcGxlIC5kYXNoYm9hcmQtZ3JvdXAgPiAuYXVpLXBhZ2UtcGFuZWwgewogICAgYm9yZGVyLWJvdHRvbTogMDsKfQoKYm9keS5wZW9wbGUgLmRhc2hib2FyZC1ncm91cCAuYXVpLXBhZ2UtcGFuZWwgLmF1aS1wYWdlLXBhbmVsLW5hdiB7CiAgICBoZWlnaHQ6IDUyM3B4OyAvKiA2MDBweCAoI21haW46bWluLWhlaWdodCkgLSAjbWFpbi1oZWFkZXI6aGVpZ2h0ICovCn0KCmJvZHkucGVvcGxlIC5kYXNoYm9hcmQtZ3JvdXAgLmF1aS1uYXZncm91cC1pbm5lciB7CiAgICBtYXJnaW4tbGVmdDogLTEwcHg7Cn0KCmJvZHkucGVvcGxlIC5hY3RpdmUtdGFiID4gYSB7CiAgICBjb2xvcjogIzMzMzsKICAgIGZvbnQtd2VpZ2h0OiBib2xkOwp9CgojcGVvcGxlLXNlYXJjaC10aXRsZS1iYXIgewogICAgb3ZlcmZsb3c6IGhpZGRlbjsKICAgIG1hcmdpbi1ib3R0b206IDEwcHg7Cn0KCiNwZW9wbGUtc2VhcmNoLXRpdGxlLWJhciBoMiB7CiAgICBmbG9hdDogbGVmdDsKfQoKYm9keS5wZW9wbGUgI3Blb3BsZS1zZWFyY2ggewogICAgbWFyZ2luLXRvcDogMDsKICAgIHRleHQtYWxpZ246IHJpZ2h0Owp9CgoucGVvcGxlLWRpcmVjdG9yeSAuYXVpLXRhYnMgPiAudGFicy1tZW51IHsKICAgIHBhZGRpbmc6IDAgMzBweDsKfQoKLnBlb3BsZS1kaXJlY3RvcnkgLmdyZXlib3ggewogICAgcG9zaXRpb246IHJlbGF0aXZlOwogICAgbWFyZ2luOiAwOwp9CgoucGVvcGxlLWRpcmVjdG9yeSAuZ3JleWJveGZpbGxlZCB7CiAgICB3aWR0aDogMTAwJTsKfQoKYm9keS5wZW9wbGUgI3Blb3BsZS1zZWFyY2ggaW5wdXQgewogICAgdmVydGljYWwtYWxpZ246IHRvcDsKfQoKYm9keS5wZW9wbGUgI3Blb3BsZS1zZWFyY2ggLmZpZWxkLWdyb3VwIHsKICAgIGRpc3BsYXk6IGlubGluZS1ibG9jazsKICAgIHdpZHRoOiBhdXRvOwogICAgcGFkZGluZzogMCAxMHB4IDAgMDsKICAgIG1hcmdpbjogMDsKfQoKYm9keS5wZW9wbGUgLmF1aS1tZXNzYWdlLmVycm9yIHsKICAgIG1hcmdpbi1ib3R0b206IDEwcHg7Cn0KCmJvZHkucGVvcGxlIC5ncmV5Ym94ICNwZW9wbGVsaXN0IC5wcm9maWxlLW1hY3JvIHsKICAgIG1hcmdpbjogMTBweCA0MHB4IDIwcHggMDsKfQoKI3Blb3BsZS1zZWFyY2ggLmZpZWxkLWdyb3VwIHsKICAgIHBhZGRpbmctbGVmdDogMDsKfQoKYm9keS5wZW9wbGUgLmJsYW5rLWV4cGVyaWVuY2UtcGVvcGxlIHsKICAgIG1hcmdpbi10b3A6IDUwcHg7Cn0KCi5ibGFuay1leHBlcmllbmNlLXBlb3BsZSB7CiAgICBtaW4taGVpZ2h0OiAxMDBweDsKICAgIGJhY2tncm91bmQ6IHVybCguLi8uLi8uLi9pbWFnZXMvaWNvbnMvcGVvcGxlLWVtcHR5LXBsYWNlaG9sZGVyLnBuZykgbm8tcmVwZWF0IHJpZ2h0IGJvdHRvbTsKfQoKLndhdGNoZXMgLnRhYmxldmlldy1hY3Rpb24taWNvbiB7CiAgICB0ZXh0LWFsaWduOiByaWdodDsKfQoKLyogQmxhbmsgZXhwZXJpZW5jZSAqLwouYmxhbmstZXhwZXJpZW5jZS1jb250YWluZXIgewogICAgYm9yZGVyOiAxcHggc29saWQgI0NDQzsKICAgIGJvcmRlci1yYWRpdXM6IDNweDsKICAgIHdpZHRoOiA2NSU7CiAgICBtYXJnaW46IDEwMHB4IGF1dG87CiAgICBwYWRkaW5nOiAzMHB4Owp9CgouYmxhbmstZXhwZXJpZW5jZS1jb250YWluZXIgcCB7CiAgICBjb2xvcjogIzcwNzA3MDsKICAgIGxpbmUtaGVpZ2h0OiAyNHB4OwogICAgZm9udC1zaXplOiAxNnB4OwogICAgd2lkdGg6NzAlOwogICAgbWFyZ2luLXRvcDowOwogICAgbWFyZ2luLWJvdHRvbToyNnB4Owp9CgouYmxhbmstZXhwZXJpZW5jZS1ibG9nIHsKICAgIGJhY2tncm91bmQ6IHVybCgnLi4vLi4vaW1hZ2VzL2ljb25zL2Jsb2ctZW1wdHktcGxhY2Vob2xkZXIucG5nJykgbm8tcmVwZWF0IHJpZ2h0IGJvdHRvbTsKfQouYmxhbmstZXhwZXJpZW5jZS1wYWdlIHsKICAgIGJhY2tncm91bmQ6IHVybCgnLi4vLi4vaW1hZ2VzL2ljb25zL3BhZ2VzLWVtcHR5LXBsYWNlaG9sZGVyLnBuZycpIG5vLXJlcGVhdCByaWdodCBib3R0b207Cn0KCi8qQmxvZyBSb2xlKi8KCi5ibG9nLXBvc3QtbGlzdGluZyB7CiAgICBwb3NpdGlvbjogcmVsYXRpdmU7CiAgICBwYWRkaW5nOiAzMHB4IDIwcHggMjBweCA2OHB4OwogICAgbWFyZ2luLXRvcDogLTEwcHg7Cn0KCiNsaW5rLWJyb3dzZXItdGFiLWl0ZW1zICsgLmJsb2ctcG9zdC1saXN0aW5nIHsKICAgIHBhZGRpbmctdG9wOiAxMHB4Owp9CgojbGluay1icm93c2VyLXRhYi1pdGVtcyArIC5ibG9nLXBvc3QtbGlzdGluZyAubG9nb0Jsb2NrIHsKICAgIHRvcDogMTBweDsKfQoKLmJsb2ctcG9zdC1saXN0aW5nICsgLmJsb2ctcG9zdC1saXN0aW5nIHsKICAgIGJvcmRlci10b3A6IDFweCBzb2xpZCAjY2NjOwogICAgbWFyZ2luLXRvcDogMDsKfQoKLmxvZ29CbG9jaywKLmJsb2dIZWFkaW5nIHsKICAgIGRpc3BsYXk6IGlubGluZS1ibG9jazsKfQoKLnVzZXJMb2dvLAoudXNlckxvZ28tNDggewogICAgd2lkdGg6IDQ4cHg7CiAgICBoZWlnaHQ6IDQ4cHg7CiAgICBib3JkZXItcmFkaXVzOiAzcHg7Cn0KCi51c2VyTG9nby05NiB7CiAgICB3aWR0aDogOTZweDsKICAgIGhlaWdodDogOTZweDsKICAgIGJvcmRlci1yYWRpdXM6IDNweDsKfQoKLnVzZXJMb2dvLTE0NCB7CiAgICB3aWR0aDogMTQ0cHg7CiAgICBoZWlnaHQ6IDE0NHB4OwogICAgYm9yZGVyLXJhZGl1czogM3B4Owp9CgoudXNlckxvZ29MaW5rIHsKICAgIGRpc3BsYXk6IGlubGluZTsKfQoKLnBhZ2UtbWV0YWRhdGEgewogICAgbGluZS1oZWlnaHQ6IDEuMjUgIWltcG9ydGFudDsKfQoKLmxvZ28taGVhZGluZy1ibG9jayB7CiAgICBtYXJnaW4tYm90dG9tOiAyMHB4Owp9CgoKLmxvZ29CbG9jayB7CiAgICBwb3NpdGlvbjogYWJzb2x1dGU7CiAgICBsZWZ0OiAwcHg7CiAgICB0b3A6IDMwcHg7Cn0KCi51c2VyTG9nb0xpbmsgewogICAgbGluZS1oZWlnaHQ6IDMwcHg7Cn0KCiN0aXRsZS1oZWFkaW5nIC51c2VyTG9nb0xpbmsgewogICAgZmxvYXQ6IGxlZnQ7Cn0KCi5sb2dvLWhlYWRpbmctYmxvY2sgLnVzZXJMb2dvIHsKICAgIHdpZHRoOiA0OHB4OwogICAgYm9yZGVyLXJhZGl1czogM3B4OwogICAgZGlzcGxheTogaW5saW5lLWJsb2NrOwp9CgpzcGFuLmJsb2dIZWFkaW5nIHsKICAgIGRpc3BsYXk6IGJsb2NrOwp9CgouYmxvZ0hlYWRpbmcgLnBhZ2UtbWV0YWRhdGEgewogICAgbWFyZ2luOiAwOwogICAgbGluZS1oZWlnaHQ6IDE2cHggIWltcG9ydGFudDsKICAgIG1hcmdpbi10b3A6IDJweDsKICAgIG1hcmdpbi1yaWdodDogMTBweDsKfQoKYS5ibG9nSGVhZGluZyB7CiAgICBmb250LXNpemU6IDI0cHg7Cn0KCi5ibG9nLXBvc3QtbGlzdGluZyA+IC53aWtpLWNvbnRlbnQgewogICAgcGFkZGluZzogMHB4ICFpbXBvcnRhbnQ7Cn0KCgouYmxvZy1wb3N0LWxpc3RpbmcgLmVuZHNlY3Rpb24gewogICAgY2xlYXI6Ym90aDsKICAgIG1hcmdpbi10b3A6IDIwcHg7Cn0KLyogRW5kIGJsb2cgcm9sZSovCgovKiBCbGFuayBleHBlcmllbmNlICovCi5ibGFuay1leHBlcmllbmNlLWNvbnRhaW5lciB7CiAgICBib3JkZXI6IDFweCBzb2xpZCAjQ0NDOwogICAgYm9yZGVyLXJhZGl1czogM3B4OwogICAgd2lkdGg6IDY1JTsKICAgIG1hcmdpbjogMTAwcHggYXV0bzsKICAgIHBhZGRpbmc6IDMwcHg7Cn0KCi5ibGFuay1leHBlcmllbmNlLWNvbnRhaW5lciBwIHsKICAgIGNvbG9yOiAjNzA3MDcwOwogICAgbGluZS1oZWlnaHQ6IDI0cHg7CiAgICBmb250LXNpemU6IDE2cHg7CiAgICB3aWR0aDo3MCU7CiAgICBtYXJnaW4tdG9wOjA7CiAgICBtYXJnaW4tYm90dG9tOjI2cHg7Cn0KCi5ibGFuay1leHBlcmllbmNlLWJsb2cgewogICAgYmFja2dyb3VuZDogdXJsKCcuLi8uLi9pbWFnZXMvaWNvbnMvYmxvZy1lbXB0eS1wbGFjZWhvbGRlci5wbmcnKSBuby1yZXBlYXQgcmlnaHQgYm90dG9tOwp9CgovKiBhbHBoYWJldCBsaXN0ICovCiNzcXVhcmV0YWIgewogICAgbWFyZ2luLWxlZnQ6IDA7CiAgICBwYWRkaW5nLWxlZnQ6IDA7CiAgICB3aGl0ZS1zcGFjZTogbm93cmFwOwogICAgZm9udC1mYW1pbHk6IFZlcmRhbmEsIEFyaWFsLCBIZWx2ZXRpY2EsIHNhbnMtc2VyaWY7CiAgICBmb250LXNpemU6IDE0cHg7CiAgICBsaW5lLWhlaWdodDogMjBweDsKfQoKI3NxdWFyZXRhYiBsaSB7CiAgICBkaXNwbGF5OiBpbmxpbmU7CiAgICBsaXN0LXN0eWxlLXR5cGU6IG5vbmU7Cn0KCiNzcXVhcmV0YWIgYSB7CiAgICBwYWRkaW5nOiA1cHggN3B4IDNweCA3cHg7CiAgICBib3JkZXItd2lkdGg6IDFweDsKICAgIGJvcmRlci1zdHlsZTogc29saWQ7Cn0KCiNzcXVhcmV0YWIgYTpsaW5rLAojc3F1YXJldGFiIGE6dmlzaXRlZCB7CiAgICBjb2xvcjogI2ZmZjsKICAgIHRleHQtZGVjb3JhdGlvbjogbm9uZTsKfQoKI3NxdWFyZXRhYiBhOmhvdmVyIHsKICAgIHRleHQtZGVjb3JhdGlvbjogbm9uZTsKfQoKLmFkbWluLXNpZGViYXItZ3JvdXAgfiAuYWRtaW4tc2lkZWJhci1ncm91cCB7CgltYXJnaW4tdG9wOiAyMHB4Owp9CgovKiBDT05GREVWLTEzNDgyOiBPdmVycmlkZSAuYXVpLXBhZ2UtcGFuZWwgKi8KI21haW4uYXVpLXBhZ2UtcGFuZWwgewoJYm9yZGVyLXRvcDogMDsKfQoKLnJlY2VudGx5LXVwZGF0ZWQtY29uY2lzZSAudXBkYXRlLWl0ZW0gLnVwZGF0ZS1pdGVtLWRlc2MsCi5yZWNlbnRseS11cGRhdGVkLWNvbmNpc2UgLnVwZGF0ZS1pdGVtIC51cGRhdGUtaXRlbS1jaGFuZ2VzewogICAgZm9udC1zaXplOiAxMnB4OwogICAgbWFyZ2luLWxlZnQ6IDVweDsKICAgIGxpbmUtaGVpZ2h0OiAyMHB4Owp9CgovKiBjb25zaXN0ZW50IHBsYWNlaG9sZGVyIHRleHQgY29sb3IgKi8KOjotd2Via2l0LWlucHV0LXBsYWNlaG9sZGVyIHsK4oCCICBjb2xvcjogIzk5OTsKfQo6LW1vei1wbGFjZWhvbGRlciB7IC8qIEZpcmVmb3ggMTgtICovCuKAgiAgY29sb3I6ICM5OTk74oCCCn0KOjotbW96LXBsYWNlaG9sZGVyIHvigIIgLyogRmlyZWZveCAxOSsgKi8K4oCCICBjb2xvcjogIzk5OTvigIIKfQo6LW1zLWlucHV0LXBsYWNlaG9sZGVyIHvigIIK4oCCICBjb2xvcjogIzk5OTvigIIKfQoKLyogRW5kIFBETCBtYXN0ZXIuY3NzICovCgouZGVmYXVsdC1tYWNyby1zcGlubmVyIHsKICAgIHdpZHRoOiA0MHB4OwogICAgaGVpZ2h0OiA0MHB4OwogICAgcG9zaXRpb246IHJlbGF0aXZlOwp9CgoKLyogcmVtb3ZlIHVud2FudGVkIHRpdGxlIGZyb20gQ29udGVudEJ5TGFiZWwgbWFjcm8gKi8KCi5hdWktaWNvbmZvbnQtcGFnZS1kZWZhdWx0LCAuYXVpLWljb25mb250LXBhZ2UtYmxvZ3Bvc3QgewoKICAgIGRpc3BsYXk6bm9uZTsKCn0KCuKAiwoKLmhpZGRlbiB7CgogICAgZGlzcGxheTpub25lOwoKfQoKCgovKiB3aWtpLWNvbnRlbnQuY3NzICovCgovKioKICogQVVJIE92ZXJyaWRlcwogKiBzZWUgL2luY2x1ZGVzL2Nzcy9hdWktb3ZlcnJpZGVzLmNzcwogKi8KLnJvdW5kZWQtY29ybmVycyAoQHJhZGl1czogNXB4KSB7CiAgLW1vei1ib3JkZXItcmFkaXVzOiBAcmFkaXVzOwogIC13ZWJraXQtYm9yZGVyLXJhZGl1czogQHJhZGl1czsKICBib3JkZXItcmFkaXVzOiBAcmFkaXVzOwp9Cgoud2lraS1jb250ZW50IHsKICAuaGVhZGVyLCAuZm9vdGVyLCAuY2VsbCB7CiAgICBtYXJnaW46IDhweCAwOwogICAgYm94LXNpemluZzogYm9yZGVyLWJveDsKICAgIHdvcmQtd3JhcDogYnJlYWstd29yZDsKICAgIC5yb3VuZGVkLWNvcm5lcnM7CiAgfQoKICAuY29sdW1uTGF5b3V0IHsKICAgIGRpc3BsYXk6IHRhYmxlOwogICAgdGFibGUtbGF5b3V0OiBmaXhlZDsKICAgIHdpZHRoOiAxMDAlOwogICAgKmNsZWFyOiBib3RoOwoKICAgIC5jZWxsIHsKICAgICAgdmVydGljYWwtYWxpZ246IHRvcDsKICAgIH0KICAgIC5jZWxsLmFzaWRlIHsKICAgICAgd2lkdGg6IDI5LjklOwogICAgfQogICAgLmNlbGwuc2lkZWJhcnMgewogICAgICB3aWR0aDogMTkuOSU7CiAgICB9CiAgfQoKICAuY2VsbCB7CiAgICAgZGlzcGxheTogdGFibGUtY2VsbDsKICAgICBwYWRkaW5nOiAwIDEwcHg7CiAgfQoKICAuaW5uZXJDZWxsIHsKICAgIG92ZXJmbG93LXg6IGF1dG87CiAgfQoKICAucGxhY2Vob2xkZXIgewogICAgYmFja2dyb3VuZDogI2Y1ZjVmNTsKICAgIGJvcmRlcjogMXB4IGRvdHRlZCAjY2NjOwogICAgY29sb3I6ICM3MDcwNzA7CiAgICBmb250LXN0eWxlOiBpdGFsaWM7CiAgICBtYXJnaW46IDA7CiAgICBwYWRkaW5nOiAxMHB4OwoKICAgIC5hY3RpdmF0aW9uLWNvbnRlbnQgewogICAgICBkaXNwbGF5Om5vbmU7CiAgICB9CiAgICAuZGlzcGxheS1jb250ZW50IHsKICAgICAgZGlzcGxheTogaW5oZXJpdDsKICAgIH0KICB9CgogIGxpID4gdWwsCiAgbGkgPiBvbCwKICB1bCA+IHVsLAogIG9sID4gb2wgewogICAgbWFyZ2luLXRvcDogMDsKICB9CgogIHVsIHsKICAgIGxpc3Qtc3R5bGUtdHlwZTogZGlzYzsKICB9CgogIG9sLAogIG9sIG9sIG9sIG9sLAogIG9sIG9sIG9sIG9sIG9sIG9sIG9sLAogIG9sIG9sIG9sIG9sIG9sIG9sIG9sIG9sIG9sIG9sIHsKICAgIGxpc3Qtc3R5bGUtdHlwZTogZGVjaW1hbDsKICB9CgogIG9sIG9sLAogIG9sIG9sIG9sIG9sIG9sLAogIG9sIG9sIG9sIG9sIG9sIG9sIG9sIG9sLAogIG9sIG9sIG9sIG9sIG9sIG9sIG9sIG9sIG9sIG9sIG9sIHsKICAgIGxpc3Qtc3R5bGUtdHlwZTogbG93ZXItYWxwaGE7CiAgfQoKICBvbCBvbCBvbCwKICBvbCBvbCBvbCBvbCBvbCBvbCwKICBvbCBvbCBvbCBvbCBvbCBvbCBvbCBvbCBvbCwKICBvbCBvbCBvbCBvbCBvbCBvbCBvbCBvbCBvbCBvbCBvbCBvbCB7CiAgICBsaXN0LXN0eWxlLXR5cGU6IGxvd2VyLXJvbWFuOwogIH0KCiAgLyogdGhlc2Ugc3R5bGVzIGFyZSBjb3BpZWQgZnJvbSBhdWktcGFnZS10eXBvZ3JhcGh5LmNzcyBpbiBBVUkgNS40LiBUaGlzIHdpbGwgcmV0YWluIHRoZSBoZWFkaW5nIHN0eWxlcyBmb3IgdXNlcgogICAqIGdlbmVyYXRlZCBjb250ZW50IHdoZW4gdXBncmFkaW5nIHRvIHVzZSBBVUkgNS43IGFuZCBBREcgMi4wLiBUaGlzIGlzIGJyaXR0bGUgYmVjYXVzZSBub3QgZXZlcnkgcHJvcGVydHkgaXMKICAgKiBleHBsaWNpdGx5IGRlY2xhcmVkIGhlcmUuIElmIEFVSSA1Ljggc2V0cyBoMSB0ZXh0LXRyYW5zZm9ybTogdXBwZXJjYXNlOyBpdCB3aWxsIGJyZWFrIHRoZSBkZWZhdWx0IHN0eWxlcyAqLwogIGgxIHsKICAgIGZvbnQtc2l6ZTogMS43MTRlbTsKICAgIGZvbnQtd2VpZ2h0OiBub3JtYWw7CiAgICBsaW5lLWhlaWdodDogMS4xNjY7CiAgfQogIGgyIHsKICAgIGZvbnQtc2l6ZTogMS40M2VtOwogICAgZm9udC13ZWlnaHQ6IG5vcm1hbDsKICAgIGxpbmUtaGVpZ2h0OiAxLjI7CiAgfQogIGgzIHsKICAgIGZvbnQtc2l6ZTogMS4xNDJlbTsKICAgIGxpbmUtaGVpZ2h0OiAxLjU7CiAgfQogIGg0IHsKICAgIGZvbnQtc2l6ZTogMWVtOwogICAgbGluZS1oZWlnaHQ6IDEuNDI4OwogIH0KICBoNSB7CiAgICBmb250LXNpemU6IDAuODU3ZW07CiAgICBsaW5lLWhlaWdodDogMS4zMzM7CiAgfQogIGg2IHsKICAgIGxpbmUtaGVpZ2h0OiAxLjQ1NDsKICAgIGZvbnQtc2l6ZTogMC43ODVlbTsKICB9CiAgaDE6Zmlyc3QtY2hpbGQsCiAgaDI6Zmlyc3QtY2hpbGQsCiAgaDM6Zmlyc3QtY2hpbGQsCiAgaDQ6Zmlyc3QtY2hpbGQsCiAgaDU6Zmlyc3QtY2hpbGQsCiAgaDY6Zmlyc3QtY2hpbGQgewogICAgbWFyZ2luLXRvcDogMDsKICB9CiAgLyogTmljZSBzdHlsZXMgZm9yIHVzaW5nIHN1YmhlYWRpbmdzICovCiAgaDEgKyBoMiwKICBoMiArIGgzLAogIGgzICsgaDQsCiAgaDQgKyBoNSwKICBoNSArIGg2IHsKICAgIG1hcmdpbi10b3A6IDEwcHg7CiAgfQogIC8qIEVuZCBzdHlsZXMgY29waWVkIGZyb20gYXVpLXBhZ2UtdHlwb2dyYXBoeS5jc3MgaW4gQVVJIDUuNCAqLwoKICBoMSArIGgxLAogIGgyICsgaDIsCiAgaDMgKyBoMywKICBoNCArIGg0LAogIGg1ICsgaDUsCiAgaDYgKyBoNiB7CiAgICBtYXJnaW4tdG9wOiAxMHB4OwogIH0KICAvKiBFbmQgQ29uZmx1ZW5jZSBzcGVjaWZpYyB0eXBvZ3JhcGh5IG92ZXJyaWRlcyBvbiB0b3Agb2YgQVVJIDUuNCB0eXBvZ3JhcGh5ICovCgogIC5jb25mbHVlbmNlLWNvbnRlbnQtaW1hZ2UtYm9yZGVyIHsKICAgIGJvcmRlcjogMXB4IHNvbGlkIGJsYWNrOwogIH0KCiAgZGl2LmVycm9yID4gc3Bhbi5lcnJvciB7CiAgICBjb2xvcjogIzMzMzsKICAgIHBhZGRpbmc6IDZweCAxMHB4OwogICAgcG9zaXRpb246IHJlbGF0aXZlOwogICAgYmFja2dyb3VuZDogI2ZmZmRmNjsKICAgIGJvcmRlcjogMXB4IHNvbGlkICNmZmVhYWU7CiAgICAucm91bmRlZC1jb3JuZXJzOwogIH0KCi8qIENPTkZERVYtNjEzNyBXZWxjb21lIHRvIHRoZSB3b3JsZCBvZiBicm93c2VyIGhhY2tzCiBXZSdyZSB0YXJnZXRpbmcgSUU4IGNvbXBhdGliaWxpdHkgbW9kZSB3aGljaCBtZWFucyBJRTcKIGhhY2tzLiBUaGlzIHVwZGF0ZXMgdGhlIHBhZ2UtbGF5b3V0IHRvIGJlIGZsb2F0ZWQgcmF0aGVyCiB0aGFuIHRhYmxlIGxheW91dCB3aGljaCBpcyBub3Qgc3VwcG9ydGVkIGluIGNvbXBhdCBtb2RlLgogVGhlc2Ugc3R5bGVzIGFyZSBvbmx5IGFwcGxpZWQgdG8gSUU4IGNvbXBhdCBtb2RlICovCiAgLmNvbHVtbkxheW91dCwKICAuY2VsbCwKICAuaGVhZGVyLAogIC5mb290ZXIgewogICAgKmRpc3BsYXk6IGJsb2NrOwogICAgKmZsb2F0OiBsZWZ0OwogICAgKndpZHRoOiAxMDAlOwogIH0KCiAgLmlubmVyQ2VsbCB7CiAgICAqYm9yZGVyOiAycHggZGFzaGVkICNjY2M7CiAgICAqbWFyZ2luOiA4cHggNHB4OwogICAgKnBhZGRpbmc6IDRweCA4cHg7CiAgfQoKICAvKiBUZXh0IFBsYWNlaG9sZGVycyAqLwogIC50ZXh0LXBsYWNlaG9sZGVyIHsKICAgIGJhY2tncm91bmQ6ICNmNWY1ZjU7CiAgICBjb2xvcjogIzcwNzA3MDsKICAgIGZvbnQtc3R5bGU6IGl0YWxpYzsKICAgIG1pbi13aWR0aDogMTBweDsgLyogU28gdGhhdCBpdCBpcyB2aXNpYmxlIHdoZW4gZW1wdHkgKi8KICAgIGRpc3BsYXk6IGJsb2NrOwogIH0KCiAgLnRleHQtcGxhY2Vob2xkZXIuc2VsZWN0ZWQgewogICAgY29sb3I6ICMzMzMKICB9CgogIC5hdWktbG96ZW5nZSB7CiAgICBwYWRkaW5nOiAzcHggNXB4IDJweCA1cHg7CiAgfQp9CgogIC8qIG5lZWRlZCB0byBtYWtlIHRoZSBoZWFkaW5nIGluIHRoZSBlZGl0b3IgdGhlIHJpZ2h0IGNvbG91ciB3aXRob3V0IG1lc3Npbmcgd2l0aCBmYWJyaWMqLwogICN0aW55bWNlIGg2IHsKICAgIGNvbG9yOiAjNkI3NzhDOwogIH0KICAKLmNvbnRlbnRMYXlvdXQgLmlubmVyQ2VsbCA+ICo6Zmlyc3QtY2hpbGQsCi5jb250ZW50TGF5b3V0MiB7CiAgLmlubmVyQ2VsbCA+ICo6Zmlyc3QtY2hpbGQgewogICAgbWFyZ2luLXRvcDogMDsKICB9Cn0KCi5jb250ZW50TGF5b3V0MiAuY29sdW1uTGF5b3V0IHsKICBtYXJnaW4tYm90dG9tOiA4cHg7Cn0KCgoudmlldyAud2lraS1jb250ZW50IC5jZWxsOmZpcnN0LWNoaWxkLAouY29udGVudC1wcmV2aWV3IC53aWtpLWNvbnRlbnQgLmNlbGw6Zmlyc3QtY2hpbGQgewogIHBhZGRpbmc6IDA7Cn0KCi8qIENPTkYtMjM0OTcgLSBXb3JrIGFyb3VuZCByZW5kZXJpbmcgaXNzdWUgaW4gV2Via2l0IGFuZCBJRTkuIFdvcmtzIGZpbmUgZm9yIElFOCBhbmQgRmlyZWZveC4gKi8KbGlbc3R5bGUqPSd0ZXh0LWFsaWduOiBjZW50ZXInXSwKbGlbc3R5bGUqPSd0ZXh0LWFsaWduOiByaWdodCddIHsKICBsaXN0LXN0eWxlLXBvc2l0aW9uOiBpbnNpZGU7Cn0KCi8qIENPTkZERVYtNzc1NCAtIFdvcmthcm91bmQgZm9yIHNvbWUgdW53YW50ZWQgb25EZW1hbmQgc3R5bGluZy4KICAgUmVtb3ZlIG9uY2UgSlNUREVWLTE3MzAgaXMgZml4ZWQuIFNlZSBDT05GREVWLTc3OTkuCiAgIFdpbGwgaGF2ZSBhIHNpZGUtZWZmZWN0IG9mIGJyZWFraW5nIGFueSBmb290ZXJzIGluIHRoZW1lcyB0aGF0CiAgIGRvIG5vdCB1c2UgdGhlIGRlZmF1bHQgZm9udC1mYW1pbHkgb3IgZm9udC1zaXplLgogICAjbWFpbiBzZWxlY3RvciBpcyB0byBwcmV2ZW50IHRoaXMgcnVsZSBhcHBseWluZyB3aXRoaW4gdGhlIFJURSBpZnJhbWUuCiAgICovCiNtYWluIC53aWtpLWNvbnRlbnQgLmZvb3RlciBwLCAjbWFpbiAud2lraS1jb250ZW50IC5mb290ZXIgYSB7CiAgZm9udC1mYW1pbHk6IGFyaWFsLHNhbnMtc2VyaWY7CiAgZm9udC1zaXplOiAxNHB4Owp9Cgoud2lraS1jb250ZW50IC5jZWxsLAoubWNlQ29udGVudEJvZHkud2lraS1jb250ZW50IC5jZWxsLAoudHdvQ29sdW1ucyAuY2VsbCwKLnRocmVlQ29sdW1ucyAuY2VsbCwKLnR3b0NvbHVtbnMgLmxhcmdlLAoubWNlQ29udGVudEJvZHkud2lraS1jb250ZW50IC5oZWFkZXIsCi5tY2VDb250ZW50Qm9keS53aWtpLWNvbnRlbnQgLmZvb3RlciwKLnRocmVlQ29sdW1ucyAubGFyZ2UgewogICpib3JkZXI6IDA7CiAgKm1hcmdpbjogMDsKICAqcGFkZGluZzogMDsKICAqb3ZlcmZsb3c6IGhpZGRlbjsKfQoKLnR3b0NvbHVtbnMgLmNlbGwgewogICp3aWR0aDogNDkuOSU7Cn0KCi50aHJlZUNvbHVtbnMgLmNlbGwgewogICp3aWR0aDogMzMuMyU7Cn0KCi50d29Db2x1bW5zIC5sYXJnZSB7CiAgKndpZHRoOiA2OS45JTsKfQoKLnRocmVlQ29sdW1ucyAubGFyZ2UgewogICp3aWR0aDogNTkuOSU7Cn0KCmRpdi5hc2lkZSArIGRpdi5sYXJnZSwKZGl2LmxhcmdlICsgZGl2LmFzaWRlLApkaXYubGFyZ2UgKyBkaXYuc2lkZWJhcnMsCi50d29Db2x1bW5zIGRpdi5jZWxsICsgZGl2LmNlbGwsCi50aHJlZUNvbHVtbnMgZGl2LmNlbGwgKyBkaXYuY2VsbCArIGRpdi5jZWxsIHsKICAqZmxvYXQ6IHJpZ2h0Owp9CgovKiBQYWdlIExheW91dHMgMiAqLwovKiBTZWN0aW9uIHR5cGVzOiBzaW5nbGUsIHR3by1lcXVhbCwgdHdvLWxlZnQtc2lkZWJhciwgdHdvLXJpZ2h0LXNpZGViYXIsIHRocmVlLWVxdWFsLCB0aHJlZS13aXRoLXNpZGViYXJzICovCgoudHdvLWVxdWFsIC5ub3JtYWwgewogICp3aWR0aDogNDkuOSU7Cn0KCi50d28tbGVmdC1zaWRlYmFyIC5ub3JtYWwsCi50d28tcmlnaHQtc2lkZWJhciAubm9ybWFsIHsKICAqd2lkdGg6IDY5LjklOwp9CgoudGhyZWUtZXF1YWwgLmNlbGwgewogICp3aWR0aDogMzMuMyU7Cn0KCi50aHJlZS13aXRoLXNpZGViYXJzIC5ub3JtYWwgewogICp3aWR0aDogNTkuOSU7Cn0KCi50d28tZXF1YWwgZGl2LmNlbGwgKyBkaXYuY2VsbCB7CiAgKmZsb2F0OiByaWdodDsKfQoKLnRocmVlLWVxdWFsLCAudGhyZWUtd2l0aC1zaWRlYmFycyB7CiAgZGl2LmNlbGwgKyBkaXYuY2VsbCArIGRpdi5jZWxsIHsKICAgICpmbG9hdDogcmlnaHQ7CiAgfQp9CgovKiBDT05GREVWLTEzODA0OiBUZW1wb3Jhcnkgd29ya2Fyb3VuZCwgcmVtb3ZlIG9uY2UgZml4ZWQgaW4gdGhlIHBsdWdpbiAoV0RBWS0xNjQxKS4gKi8KI213LWNvbnRhaW5lciBkaXYubXctbm8tbm90aWZpY2F0aW9ucyBkaXYuc3ViaGVhZGluZyBwIHsKICBsaW5lLWhlaWdodDogMjRweDsKICBtYXJnaW4tdG9wOiA4cHg7CiAgbWFyZ2luLWJvdHRvbTogOHB4Owp9CgouY29uZmx1ZW5jZVRhYmxlIHsKICAgIGJvcmRlci1jb2xsYXBzZTogY29sbGFwc2U7Cn0KCi5jb25mbHVlbmNlVGgsCi5jb25mbHVlbmNlVGQgewogICAgYm9yZGVyOiAxcHggc29saWQgI2RkZDsKICAgIHBhZGRpbmc6IDdweCAxMHB4OyAvKiBDT05GREVWLTEzNjE4OiBsaW5lLWhlaWdodCB3aWxsIGFkZCB1cCAzIHBpeGVscywgc28gd2UgaGF2ZSBhIDEwcHggdG9wIHBhZGRpbmcgZm9yIHRleHQuIEltYWdlcyB3aWxsIHJlbWFpbiB3aXRoIDdweCB0b3AgbWFyZ2luIHRob3VnaCAoc2VlIHRocmVhZCBpbiBTdGFzaCkgKi8KICAgIHZlcnRpY2FsLWFsaWduOiB0b3A7CiAgICB0ZXh0LWFsaWduOiBsZWZ0OwogICAgbWluLXdpZHRoOiA4cHg7IC8qIENPTkYtMzk5NDM6IHNldCB0YWJsZSBjZWxsIG1pbi13aWR0aCB0byB3aGljaCBjdXJzb3IgY2FuIGJlIGZvY3VzZWQgKi8KfQoKLyogTGlzdHMgaW4gdGFibGVzICovCi5jb25mbHVlbmNlVGFibGUgb2wsCi5jb25mbHVlbmNlVGFibGUgdWwgewogICAgbWFyZ2luLWxlZnQ6IDA7CiAgICBwYWRkaW5nLWxlZnQ6IDIycHg7IC8qIENPTkZERVYtMTI1ODk6IGRlZmF1bHQgbGVmdCBwYWRkaW5nIGlzIGZhciB0b28gd2lkZSAqLwp9CgovKiBhbGwgdGFibGVzIHNob3VsZCBoYXZlIGEgdG9wIG1hcmdpbiBvZiAxMHB4ICovCi5jb25mbHVlbmNlVGFibGUsIC50YWJsZS13cmFwIHsKICAgIG1hcmdpbjogMTBweCAwIDAgMDsKICAgIG92ZXJmbG93LXg6IGF1dG87Cn0KCi8qIGFuIGV4Y2VwdGlvbiB0byBhYm92ZSBydWxlIGZvciB0YWJsZXMgdGhhdCBhcmUgZmlyc3QgY2hpbGQgKi8KLmNvbmZsdWVuY2VUYWJsZTpmaXJzdC1jaGlsZCwgLnRhYmxlLXdyYXA6Zmlyc3QtY2hpbGQgewogICAgbWFyZ2luLXRvcDogMDsKfQoKLyogQmFja2dyb3VuZCBjb2xvcnMgKi8KdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRoLmNvbmZsdWVuY2VUaCwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRoLmNvbmZsdWVuY2VUaCA+IHAsCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0aC5jb25mbHVlbmNlVGguaGlnaGxpZ2h0LWdyZXksCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0aC5jb25mbHVlbmNlVGguaGlnaGxpZ2h0LWdyZXkgPiBwLAp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGQuY29uZmx1ZW5jZVRkLmhpZ2hsaWdodC1ncmV5LAp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGQuY29uZmx1ZW5jZVRkLmhpZ2hsaWdodC1ncmV5ID4gcCB7CiAgICBiYWNrZ3JvdW5kLWNvbG9yOiAjZjBmMGYwOwp9Cgp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoLmhpZ2hsaWdodC1ibHVlLAp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoLmhpZ2hsaWdodC1ibHVlID4gcCwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZC5oaWdobGlnaHQtYmx1ZSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZC5oaWdobGlnaHQtYmx1ZSA+IHAgewogICAgYmFja2dyb3VuZC1jb2xvcjogI2UwZjBmZjsKfQoKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRoLmNvbmZsdWVuY2VUaC5oaWdobGlnaHQtZ3JlZW4sCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0aC5jb25mbHVlbmNlVGguaGlnaGxpZ2h0LWdyZWVuID4gcCwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZC5oaWdobGlnaHQtZ3JlZW4sCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0ZC5jb25mbHVlbmNlVGQuaGlnaGxpZ2h0LWdyZWVuID4gcCB7CiAgICBiYWNrZ3JvdW5kLWNvbG9yOiAjZGRmYWRlOwp9Cgp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoLmhpZ2hsaWdodC1yZWQsCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0aC5jb25mbHVlbmNlVGguaGlnaGxpZ2h0LXJlZCA+IHAsCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0ZC5jb25mbHVlbmNlVGQuaGlnaGxpZ2h0LXJlZCwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZC5oaWdobGlnaHQtcmVkID4gcCB7CiAgICBiYWNrZ3JvdW5kLWNvbG9yOiAjZmZlN2U3Owp9Cgp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoLmhpZ2hsaWdodC15ZWxsb3csCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0aC5jb25mbHVlbmNlVGguaGlnaGxpZ2h0LXllbGxvdyA+IHAsCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0ZC5jb25mbHVlbmNlVGQuaGlnaGxpZ2h0LXllbGxvdywKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZC5oaWdobGlnaHQteWVsbG93ID4gcCB7CiAgICBiYWNrZ3JvdW5kLWNvbG9yOiAjZmZmZmRkOwp9CgovKiBBREczIGNvbG9ycyAqLwp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMwOTFlNDIiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMDkxZTQyIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzA5MUU0MjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMxNzJiNGQiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMTcyYjRkIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzE3MkI0RDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMyNTM4NTgiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMjUzODU4Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzI1Mzg1ODsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMzNDQ1NjMiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMzQ0NTYzIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzM0NDU2MzsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM0MjUyNmUiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjNDI1MjZlIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzQyNTI2RTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM1MDVmNzkiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjNTA1Zjc5Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzUwNUY3OTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM1ZTZjODQiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjNWU2Yzg0Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzVFNkM4NDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM2Yjc3OGMiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjNmI3NzhjIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzZCNzc4QzsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM3YTg2OWEiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjN2E4NjlhIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzdBODY5QTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM4OTkzYTQiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjODk5M2E0Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzg5OTNBNDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM5N2EwYWYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjOTdhMGFmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzk3QTBBRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNhNWFkYmEiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjYTVhZGJhIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0E1QURCQTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNiM2JhYzUiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjYjNiYWM1Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0IzQkFDNTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNjMWM3ZDAiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjYzFjN2QwIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0MxQzdEMDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNkZmUxZTYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZGZlMWU2Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0RGRTFFNjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNlYmVjZjAiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZWJlY2YwIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0VCRUNGMDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmNGY1ZjciXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZjRmNWY3Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0Y0RjVGNzsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmYWZiZmMiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmFmYmZjIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZBRkJGQzsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZmZmZmYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmZmZmZmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGRkZGRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNiZjI2MDAiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjYmYyNjAwIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0JGMjYwMDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNkZTM1MGIiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZGUzNTBiIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0RFMzUwQjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZjU2MzAiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmY1NjMwIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGNTYzMDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZjc0NTIiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmY3NDUyIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGNzQ1MjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZjhmNzMiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmY4ZjczIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGOEY3MzsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZmJkYWQiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmZiZGFkIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGQkRBRDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZmViZTYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmZlYmU2Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGRUJFNjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZjhiMDAiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmY4YjAwIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGOEIwMDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZjk5MWYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmY5OTFmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGOTkxRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZmFiMDAiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmZhYjAwIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGQUIwMDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZmM0MDAiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmZjNDAwIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGQzQwMDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZmUzODAiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmZlMzgwIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGRTM4MDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZmYwYjMiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmZmMGIzIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGRjBCMzsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNmZmZhZTYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZmZmYWU2Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0ZGRkFFNjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMwMDY2NDQiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMDA2NjQ0Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzAwNjY0NDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMwMDg3NWEiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMDA4NzVhIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzAwODc1QTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMzNmIzN2UiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMzZiMzdlIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzM2QjM3RTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM1N2Q5YTMiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjNTdkOWEzIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzU3RDlBMzsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM3OWYyYzAiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjNzlmMmMwIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzc5RjJDMDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNhYmY1ZDEiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjYWJmNWQxIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0FCRjVEMTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNlM2ZjZWYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZTNmY2VmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0UzRkNFRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMwMDhkYTYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMDA4ZGE2Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzAwOERBNjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMwMGEzYmYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMDBhM2JmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzAwQTNCRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMwMGI4ZDkiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMDBiOGQ5Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzAwQjhEOTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMwMGM3ZTYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMDBjN2U2Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzAwQzdFNjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM3OWUyZjIiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjNzllMmYyIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzc5RTJGMjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNiM2Y1ZmYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjYjNmNWZmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0IzRjVGRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNlNmZjZmYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZTZmY2ZmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0U2RkNGRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMwNzQ3YTYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMDc0N2E2Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzA3NDdBNjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMwMDUyY2MiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMDA1MmNjIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzAwNTJDQzsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMwMDY1ZmYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMDA2NWZmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzAwNjVGRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMyNjg0ZmYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMjY4NGZmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzI2ODRGRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM0YzlhZmYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjNGM5YWZmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzRDOUFGRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNiM2Q0ZmYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjYjNkNGZmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0IzRDRGRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNkZWViZmYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZGVlYmZmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0RFRUJGRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM0MDMyOTQiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjNDAzMjk0Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzQwMzI5NDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM1MjQzYWEiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjNTI0M2FhIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzUyNDNBQTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM2NTU0YzAiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjNjU1NGMwIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzY1NTRDMDsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM4Nzc3ZDkiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjODc3N2Q5Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzg3NzdEOTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCM5OThkZDkiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjOTk4ZGQ5Il0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzk5OEREOTsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNjMGI2ZjIiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjYzBiNmYyIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0MwQjZGMjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCNlYWU2ZmYiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjZWFlNmZmIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogI0VBRTZGRjsKfQp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoW2RhdGEtaGlnaGxpZ2h0LWNvbG91cj0iXCMwMDAwMDAiXSwKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZFtkYXRhLWhpZ2hsaWdodC1jb2xvdXI9IlwjMDAwMDAwIl0gewogICAgYmFja2dyb3VuZC1jb2xvcjogIzAwMDAwMDsKfQovKiBBREczIGNvbG9ycyBlbmQgKi8KCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0aC5jb25mbHVlbmNlVGgsCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0aC5jb25mbHVlbmNlVGggPiBwIHsKICAgIGZvbnQtd2VpZ2h0OiBib2xkOwp9Cgp0YWJsZS5jb25mbHVlbmNlVGFibGUgdGguY29uZmx1ZW5jZVRoLm5vaGlnaGxpZ2h0LCAvKiBkZXByZWNhdGVkICovCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0aC5jb25mbHVlbmNlVGgubm9oaWdobGlnaHQgPiBwIHsKICAgIC8qIGRlcHJlY2F0ZWQgKi8KICAgIGZvbnQtd2VpZ2h0OiBub3JtYWw7CiAgICBiYWNrZ3JvdW5kLWNvbG9yOiB0cmFuc3BhcmVudDsKfQoKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLmNvbmZsdWVuY2VUZCBpbWcsCnRhYmxlLmNvbmZsdWVuY2VUYWJsZSB0ZC5jb25mbHVlbmNlVGQgLmNvbmZsdWVuY2UtZW1iZWRkZWQtZmlsZS13cmFwcGVyIGltZywKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRoLmNvbmZsdWVuY2VUaCAuY29uZmx1ZW5jZS1lbWJlZGRlZC1maWxlLXdyYXBwZXIgaW1nIHsKICAgIG1heC13aWR0aDogbm9uZTsKfQoKdGFibGUuY29uZmx1ZW5jZVRhYmxlIHRkLm51bWJlcmluZ0NvbHVtbiB7CiAgICAtd2Via2l0LXRvdWNoLWNhbGxvdXQ6IG5vbmU7CiAgICAtd2Via2l0LXVzZXItc2VsZWN0OiBub25lOwogICAgLWtodG1sLXVzZXItc2VsZWN0OiBub25lOwogICAgLW1vei11c2VyLXNlbGVjdDogbm9uZTsKICAgIC1tcy11c2VyLXNlbGVjdDogbm9uZTsKICAgIHVzZXItc2VsZWN0OiBub25lOwogICAgY3Vyc29yOiBkZWZhdWx0Owp9CgovKgogICAgU3R5bGVzIGZvciBtYWNyb3MgYnVuZGxlZCBpbiBSZW5kZXJlci4KKi8KLnNlYXJjaE1hY3JvIHsKICAgIGZvbnQtc2l6ZTogMTBwdDsKICAgIG1hcmdpbjogMTBweCAwOwp9Cgouc2VhcmNoTWFjcm8gLnJlc3VsdCB7CiAgICBtYXJnaW4tdG9wOiAzcHg7CiAgICBwYWRkaW5nOiAwIDVweCA1cHggNXB4OwogICAgYm9yZGVyLWJvdHRvbTogMXB4IHNvbGlkICNkZGQ7Cn0KCi5zZWFyY2hNYWNybyAucmVzdWx0U3VtbWFyeSB7CiAgICBtYXJnaW4tYm90dG9tOiA3cHg7Cn0KCi5yc3NNYWNybyB7CiAgICBmb250LXNpemU6IDEwcHQ7Cn0KCi5yc3NNYWNybyB0YWJsZSB7CiAgICBtYXJnaW46IDEwcHggMDsKICAgIHdpZHRoOiAxMDAlOwogICAgYm9yZGVyLWNvbGxhcHNlOiBjb2xsYXBzZTsKfQoKLnJzc01hY3JvIHRhYmxlIHRoLAoucnNzTWFjcm8gdGFibGUgdGQgewogICAgYm9yZGVyOiAxcHggc29saWQgI2NjYzsKICAgIHBhZGRpbmc6IDRweDsKfQoKLnJzc01hY3JvIHRhYmxlIHRoIHsKICAgIGJhY2tncm91bmQ6ICNmMGYwZjA7Cn0KCi8qIENvZGUgc3R5bGVzICovCi5jb2RlLCAucHJlZm9ybWF0dGVkIHsKICAgIGJhY2tncm91bmQtY29sb3I6ICNmZmY7CiAgICBvdmVyZmxvdzogYXV0bzsKfQoKLmNvZGUgcHJlLCAucHJlZm9ybWF0dGVkIHByZSB7IC8qIG5lZWRzICdwcmUnIHRvIG92ZXJyaWRlIFRpbnlNQ0Ugc3R5bGUgKi8KICAgIGZvbnQtZmFtaWx5OiJDb3VyaWVyIE5ldyIsIENvdXJpZXIsIG1vbm9zcGFjZTsKICAgIGxpbmUtaGVpZ2h0OiAxLjM7Cn0KCi8qIFRoZSBjb2RlIG1hY3JvIGNsYXNzZXMgYXJlIG92ZXJyaWRkZW4gYnkgdGhlIGZpeGVkIHdpZHRoIHRoZW1lIHNvIGhhdmUgYmVlbgogICBtYWRlIG1vcmUgc3BlY2lmaWMuICovCgoud2lraS1jb250ZW50IC5jb2RlLWtleXdvcmQgewogIGNvbG9yOiAjMDAwMDkxOwogIGJhY2tncm91bmQtY29sb3I6IGluaGVyaXQ7Cn0KCi53aWtpLWNvbnRlbnQgLmNvZGUtb2JqZWN0IHsKICBjb2xvcjogIzkxMDA5MTsKICBiYWNrZ3JvdW5kLWNvbG9yOiBpbmhlcml0Owp9Cgoud2lraS1jb250ZW50IC5jb2RlLXF1b3RlIHsKICBjb2xvcjogIzAwOTEwMDsKICBiYWNrZ3JvdW5kLWNvbG9yOiBpbmhlcml0Owp9Cgoud2lraS1jb250ZW50IC5jb2RlLWNvbW1lbnQgewogIGNvbG9yOiAjODA4MDgwOwogIGJhY2tncm91bmQtY29sb3I6IGluaGVyaXQ7Cn0KCi53aWtpLWNvbnRlbnQgLmNvZGUteG1sIC5jb2RlLWtleXdvcmQgewogIGNvbG9yOiBpbmhlcml0OwogIGZvbnQtd2VpZ2h0OiBib2xkOwp9Cgoud2lraS1jb250ZW50IC5jb2RlLXRhZyB7CiAgY29sb3I6ICMwMDAwOTE7CiAgYmFja2dyb3VuZC1jb2xvcjogaW5oZXJpdDsKfQoKLyogUmVjZW50bHkgVXBkYXRlZCBTdHlsZXMgKi8KLnJlY2VudGx5VXBkYXRlZEl0ZW0gewoJYm9yZGVyLWJvdHRvbTogI2YwZjBmMCAxcHggc29saWQ7Cglib3JkZXItdG9wOiAjZjBmMGYwIDFweCBzb2xpZDsKCW1hcmdpbjogMTBweCAwIDAgMDsKCXBhZGRpbmc6IDA7Cglib3JkZXItc3BhY2luZzogMDsKCXdpZHRoOiAxMDAlOwogICAgdGV4dC1kZWNvcmF0aW9uOiBub25lOwogICAgYm9yZGVyLWNvbGxhcHNlOiBjb2xsYXBzZTsKfQoKLnJlY2VudGx5VXBkYXRlZEl0ZW0gdGQgewogICAgcGFkZGluZzogMTBweDsKICAgIGJvcmRlci1ib3R0b206ICNmMGYwZjAgMXB4IHNvbGlkOwogICAgdmVydGljYWwtYWxpZ246IHRvcDsKfQoKLnJlY2VudGx5VXBkYXRlZEl0ZW0gLmF1dGhvckFuZERhdGUgewoJYmFja2dyb3VuZC1jb2xvcjogI2YwZjBmMDsKCXdpZHRoOiAyNSU7Cn0KCi5yZWNlbnRseVVwZGF0ZWRJdGVtIC5kYXRlIHsKICAgIG1hcmdpbi10b3A6IDRweDsKICAgIGZvbnQtc2l6ZTogOTAlOwogICAgY29sb3I6ICM2NjY7Cn0KCi5yZWNlbnRseVVwZGF0ZWRJdGVtIC5wcm9maWxlUGljIHsKICAgIGZsb2F0OiByaWdodDsKICAgIGJhY2tncm91bmQtY29sb3I6ICNmMGYwZjA7CiAgICBtYXJnaW46IDAgMnB4Owp9CgoucmVjZW50bHlVcGRhdGVkSXRlbSAudHdpeGllIHsKICAgIHBhZGRpbmc6IDEwcHggMCAwIDRweDsKfQoKLnJlY2VudGx5VXBkYXRlZEl0ZW0gdGQuaWNvbiB7CiAgICBwYWRkaW5nOiA4cHggMCAwIDFweDsKfQoKLnJlY2VudGx5VXBkYXRlZEl0ZW0gLmRldGFpbHMgewogICAgcGFkZGluZy1sZWZ0OiA3cHg7Cn0KCi5yZWNlbnRseVVwZGF0ZWRJdGVtIC5zdW1tYXJ5LCAucmVjZW50bHlVcGRhdGVkSXRlbSAudGh1bWJuYWlsIHsKICAgIG1hcmdpbi10b3A6IDNweDsKICAgIGNvbG9yOiAjNjY2Owp9CgoubW9yZVJlY2VudGx5VXBkYXRlZEl0ZW1zIHsKICAgIHRleHQtYWxpZ246IHJpZ2h0OwogICAgbWFyZ2luLXRvcDogMTBweDsKICAgIGZvbnQtc2l6ZTogMTBwdDsKfQoKCi8qUERMIGljb25zLmNzcyovCi5pY29u
LAouaWNvbi1jb250YWluZXIgewogICAgZGlzcGxheTogaW5saW5lLWJsb2NrOwogICAgaGVpZ2h0OiAxNnB4OwogICAgbWluLXdpZHRoOiAxNnB4OwogICAgdGV4dC1hbGlnbjogbGVmdDsKICAgIHRleHQtaW5kZW50OiAtOTk5OWVtOwogICAgYmFja2dyb3VuZC1yZXBlYXQ6IG5vLXJlcGVhdDsKICAgIGJhY2tncm91bmQtcG9zaXRpb246IGxlZnQgY2VudGVyOwogICAgZm9udC1zaXplOiAwOwogICAgdmVydGljYWwtYWxpZ246IHRleHQtYm90dG9tOyAvKiBhbGxvd3MgaWNvbiB0byBiZSB2ZXJ0aWNhbGx5IG1pZGRsZSBhbGlnbmVkIHRvIG5lYXJieSB0ZXh0ICovCn0KCi5zZWFyY2gtcmVzdWx0LXRpdGxlIC5pY29uIHsKICAgIHBvc2l0aW9uOiBhYnNvbHV0ZTsKICAgIGxlZnQ6IC0yNnB4OwogICAgdG9wOiA0cHg7Cn0KCi5zZWFyY2gtcmVzdWx0LXRpdGxlIC5pY29uIGltZyB7CiAgICB3aWR0aDogMTZweDsKICAgIGhlaWdodDogMTZweDsKICAgIGxlZnQ6IDA7Cn0KCi5pY29uLWNvbnRhaW5lciA+ICogewogICAgdGV4dC1pbmRlbnQ6IDA7CiAgICBmb250LXNpemU6IDE0cHg7CiAgICBtYXJnaW4tbGVmdDogMjRweDsKfQoKaW1nLmVtb3RpY29uIHsKICAgIHZlcnRpY2FsLWFsaWduOiB0ZXh0LWJvdHRvbTsKfQoKYS5jb250ZW50LXR5cGUtcGFnZSBzcGFuLAphLmNvbnRlbnQtdHlwZS1ibG9ncG9zdCBzcGFuLAphLmNvbnRlbnQtdHlwZS1zcGFjZSBzcGFuLAphLmNvbnRlbnQtdHlwZS1zcGFjZWRlc2Mgc3BhbiwKYS5jb250ZW50LXR5cGUtY29tbWVudCBzcGFuLAphLmNvbnRlbnQtdHlwZS1zdGF0dXMgc3BhbiwKYS5jb250ZW50LXR5cGUtdXNlciBzcGFuLAphLmNvbnRlbnQtdHlwZS11c2VyaW5mbyBzcGFuLAphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LWltYWdlIHNwYW4sCmEuY29udGVudC10eXBlLWF0dGFjaG1lbnQtcGRmIHNwYW4sCmEuY29udGVudC10eXBlLWF0dGFjaG1lbnQtaHRtbCBzcGFuLAphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LXRleHQgc3BhbiwKYS5jb250ZW50LXR5cGUtYXR0YWNobWVudC10ZXh0LWh0bWwgc3BhbiwKYS5jb250ZW50LXR5cGUtYXR0YWNobWVudC10ZXh0LXhtbCBzcGFuLAphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LXhtbCBzcGFuLAphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LXppcCBzcGFuLAphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LWphdmEgc3BhbiwKYS5jb250ZW50LXR5cGUtYXR0YWNobWVudC1jc3Mgc3BhbiwKYS5jb250ZW50LXR5cGUtYXR0YWNobWVudC1qcyBzcGFuLAphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LXVua25vd24gc3BhbiB7CiAgICBiYWNrZ3JvdW5kLXJlcGVhdDogbm8tcmVwZWF0OwogICAgYmFja2dyb3VuZC1wb3NpdGlvbjogbGVmdCBjZW50ZXI7Cn0KCi5pY29uLWVkaXQgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBUUFBQUMxK2pmcUFBQUFWVWxFUVZRb3oyTmdJQWNVcEJXOEsvaGZzS3BBRUpmMGZ5aGNoVjhhQ0FsSUY3ekRMLzIvSUkyTzBvSjRwWUVLUXBGZGppRU5WTkJSVUY1d0JpcHRqQzF3emhRb2dWMkJYUnFvNEYzQmJxQXBMZ3cwQVFDQmhuZkVnZjJWSFFBQUFBQkpSVTVFcmtKZ2dnPT0pOwp9CgouYXVpLWJ1dHRvbiA+IC5pY29uLWVkaXQgewogICAgcGFkZGluZy1yaWdodDogNnB4Owp9CgojbGFiZWxzLXNlY3Rpb24gLmljb24tZWRpdCB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQVNrbEVRVlFvejJOZ29Ba29NTVl2UGJQZ2Y4RTduSXJBMGlENERyODBDQnJqbDU1Sloya1h2TkpBQlIwRjVRVm5jRW9ERmF3cUVBUXF3U1VOVlBBT3FMK2pJQlIzNEFwU0VIRUFvWk5UV0pzWElKb0FBQUFBU1VWT1JLNUNZSUk9KTsKfQoKLmljb24tcmVtb3ZlLWZhdiB7CiAgICBiYWNrZ3JvdW5kOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQWdFbEVRVlFvejQxUlNSSEFJQkREQWhhd2dBVXNZSUYzWk9BSkMxakFBaGJvQWxQS1BkMThsaVN6RjR3TkFRUERib0dBY0pNMUVrR2ZEYTRZM0VsV1JjNVFQVzBKRHI2Skx6eXhwT1c1MHdXbWxvNWJNYlpXa0J0TGhPd240Wk1sZ284YjhLVUMzNTJveDNndVdxalM5c3ZXR3pxSWtvdjZtajlKVHkwRCt4Y1AyejJmLzZjRkZja0FBQUFBU1VWT1JLNUNZSUk9KSBuby1yZXBlYXQgcmlnaHQgdG9wOwp9CgouaWNvbi1hZGQtZmF2IHsKICAgIGJhY2tncm91bmQ6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVFBQUFDMStqZnFBQUFBNGtsRVFWUW96MzFSTVFxRFFCRDBMWDdCdCtRTDlrcVFxNFFnZ25CVlNvdXJiQVFSQWhkSWNaMTEyaFFwaElDUUptSE5EeWFyQ2NZYzZHNnplelBzenM0NXpsOFVlWkU3YXhFanhncGNaZ0lSeW15UklFbHhTbHFBS3o5Q1J6ZmFvdkpuejhya1owa3BoV0NBMEtJdG1CSWc1VW41V1JuV3pRMGF1bEJQR0Fsb1g5eWRLR0hhZUZQbEMrengrSUtmN0VsQy9GYlZHd0dKNTBUcFJyamV6SlJvVitBNkVSb1MwTzdmQmRvTmNaOElGNVpzRVFhTEhxUnB4MDRlV2E2dzdWSW1aUlVobEZFbVFJSWRWNWFIQVNScGI2Z1BudVFWbHAreE5iTE1Wajl0SG0rb0JzS1lhb0dWdHdBQUFBQkpSVTVFcmtKZ2dnPT0pIG5vLXJlcGVhdCByaWdodCB0b3A7Cgp9CgovKmFuaW1hdGVkIGdpZiovCi5pY29uLXdhaXQgewogICAgYmFja2dyb3VuZDogdXJsKC4uLy4uL2ltYWdlcy9pY29ucy93YWl0LmdpZikgbm8tcmVwZWF0IGxlZnQgdG9wOwp9CgouaWNvbi1yZWZyZXNoIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVFBQUFDMStqZnFBQUFBb0VsRVFWUW9GYTNCUVhIRE1CUUZ3RS9CRkVyQkZFVEJGSHhlR0tJZ0NxRmdDcVlRQ3FMd21tbnFtUnlTNmFXN1ZhVnA5V0ExUk1SZHQ5VEZwbGZaUlp5NjdpNm1yM3JTSFRZeGJmVkxOeTMxWklncDFucGhxWXREeEtoUFRCRnhzOVU3NG5Ub2RxMytpYWJyRGxQcUhjMHdSYVErTVVRY2RmRlZMNnhpaWx0ZFRNTlNQelJUN0E2OUxrNHhkZDBwWXEvUzdYV3g2TzRpWWxqclFkUHFMOS9wd0hRQUF5RUZZZ0FBQUFCSlJVNUVya0pnZ2c9PSk7Cn0KCi51aS10cmVlIGxpIGEsCmEuY29udGVudC10eXBlLXBhZ2Ugc3BhbiwKZGl2LmNvbnRlbnQtdHlwZS1wYWdlLApzcGFuLmNvbnRlbnQtdHlwZS1wYWdlLAouaWNvbi1wYWdlIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVFBQUFDMStqZnFBQUFBaUVsRVFWUW96NzJOTVFyRE1BeEZkUmJmcWZjUW1rS21Ra2Vkb25NcGRNazVTc21VUVZmNHV5SHdPOWloTnFaYnFkNm9weWNSRVhra2gxSnBsWW4zazdUam1Ma2lnOEZnWkNqblhqRnVkVmxRM3RCVmpPMmFvV1JjMFZSR29URHhpM0JndnhhT3o0WC9GekxHd281R2VHRzgzejZDNDhJbjlrNWFjYWFqQ2t0eUdIdVVqaVdKdkFHVUlFL2VJaDhzendBQUFBQkpSVTVFcmtKZ2dnPT0pOwogICAgYmFja2dyb3VuZC1yZXBlYXQ6IG5vLXJlcGVhdDsKfQoKYS5jb250ZW50LXR5cGUtYmxvZ3Bvc3Qgc3BhbiwKZGl2LmNvbnRlbnQtdHlwZS1ibG9ncG9zdCwKc3Bhbi5jb250ZW50LXR5cGUtYmxvZ3Bvc3QsCi5pY29uLWJsb2csCi5pY29uLWJsb2dwb3N0IHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVFBQUFDMStqZnFBQUFBdmtsRVFWUW96M1hRTWFyQ1FCU0Y0VmxMOXZUMmNabkdZQld3RWFaeUNkWVBRUWh1WWRvZ1ZoYXpnc0RwVWdTRTMyS2lab3k1cDd3Zlp5N2puSFB1WEFVWmhwOVNjL3B6OHduYWN0TW9Fb2sweXRpV3hIT2Zsam5HdjRvV3ozeE5Na2hIelZxV0lLZG1CYnppUzlDcVZhZEJxeURYYm9oYUFhUmVVUTMySm91R0RWR0Q5dFRraHhZM0hEQ2lvb3p1RjRnaUhXZ1laTFRmWUZTdmhsNnRERklHRDgzQVZjdGZ1SDlBMEk1T2p3TGQxQkEwZ1VzVjVDbGpCRjBxNTU3WkxVNnplRmIzaXdBQUFBQkpSVTVFcmtKZ2dnPT0pOwogICAgYmFja2dyb3VuZC1yZXBlYXQ6IG5vLXJlcGVhdDsKfQoKYS5jb250ZW50LXR5cGUtYXR0YWNobWVudC1pbWFnZSBzcGFuLApkaXYuY29udGVudC10eXBlLWF0dGFjaG1lbnQtaW1hZ2UsCnNwYW4uY29udGVudC10eXBlLWF0dGFjaG1lbnQtaW1hZ2UsCi5pY29uLWZpbGUtaW1hZ2UgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBUUFBQUMxK2pmcUFBQUF1a2xFUVZSNEFZWFJvV3JFUUJBRzRMemZ2Y3lLeURVWHFJbUtPMWxUVmFnNVZjNVVWOVFHYWs3VlhHUlVSTGl2STVhaklRdk44TE1EODQwSTA2UkRPeVgxaXNtaGFhY1IyMjhHakVHYXRCbXRVWGRESkRva096RG9kWWFvS3VEREdXL3hxb0V2YlF3RmFxUGZnTEpqbFQxRlZnb280MEYyQlMrV2lML2dvcmNZSFMyNGx6ekFwODRNWHFPQXF4UXA0T1FHV0J4OUU0bTF5TS8rTDBaZDhCeEVrSDRQZUpaZEFLY2FtR1hUbzA4cXg3cDUzeHpybjNQL0FoRWl3emJ1RmVLQ0FBQUFBRWxGVGtTdVFtQ0MpOwogICAgYmFja2dyb3VuZC1yZXBlYXQ6IG5vLXJlcGVhdDsKfQoKYS5jb250ZW50LXR5cGUtYXR0YWNobWVudC1wZGYgc3BhbiwKZGl2LmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LXBkZiwKc3Bhbi5jb250ZW50LXR5cGUtYXR0YWNobWVudC1wZGYsCi5pY29uLWZpbGUtcGRmIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVFBQUFDMStqZnFBQUFBamtsRVFWUjRBYVhSSVFzQ01SaUg4WDNZcXdmdkdJSndZRExaN2p1WVpNRWl5SkxSTDNDZ1VUQVpMQXNpRXg2SHNLQzNLY1BuclQ5NHcxKzFqUWxDL2t4b0cyWENRS2toRWlWOFN4Z0J6NXBKQ1p6WjBtR1JkM0RqeEo0Vk0rWllydkFKcGl4WXN1TkNTaU1JOXdUR0NkRGhxOENEQXh0NlhBazQrZ2lPRVFMb3YxL29xaGU1c1JKNGpmVnI3aWNRaGNNcllkWmFaUUFBQUFCSlJVNUVya0pnZ2c9PSk7CiAgICBiYWNrZ3JvdW5kLXJlcGVhdDogbm8tcmVwZWF0Owp9CgouZXhwYW5kLWNvbnRyb2wtaWNvbiwKLnVpLXRyZWUgbGkuY2xvc2VkID4gLmNsaWNrLXpvbmUsCiNjaGlsZHJlbi1zZWN0aW9uLmNoaWxkcmVuLWhpZGRlbiBhLmNoaWxkcmVuLXNob3ctaGlkZS5pY29uLAouaWNvbi1zZWN0aW9uLWNsb3NlZCB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQUxFbEVRVlFvejJOZ0dHU2c0RXpCVFB3S09ncitGNlRoVnpLVHNKSzdCZi9KTjRHZ0c0Qys2R0FZaWdBQVNOWVVUR2FWWXdzQUFBQUFTVVZPUks1Q1lJST0pOwp9CgouZXhwYW5kLWNvbnRyb2wtaWNvbi5leHBhbmRlZCwKLnVpLXRyZWUgbGkub3BlbmVkID4gLmNsaWNrLXpvbmUsCiNjaGlsZHJlbi1zZWN0aW9uLmNoaWxkcmVuLXNob3dpbmcgYS5jaGlsZHJlbi1zaG93LWhpZGUuaWNvbiwKLmljb24tc2VjdGlvbi1vcGVuZWQgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBUUFBQUMxK2pmcUFBQUFNVWxFUVZRb3oyTmdHR2FnNEV4QkI1VFZVWEFHbTRLT2d2OEZNNEgwVEFpTlRVa2FVT291RUtmaHRpWU5yL1NRQlFBc1BSUk1Kbm5seWdBQUFBQkpSVTVFcmtKZ2dnPT0pOwp9CgovKjxzdmcgd2lkdGg9IjE4IiBoZWlnaHQ9IjE4IiB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHhtbG5zOnhsaW5rPSJodHRwOi8vd3d3LnczLm9yZy8xOTk5L3hsaW5rIj48ZGVmcz48cGF0aCBkPSJNMi41NTMgNy42MmMwLTIuMzYzIDIuNDQzLTQuMjg1IDUuNDQ1LTQuMjg1IDMuMDAxIDAgNS40NDQgMS45MjIgNS40NDQgNC4yODQgMCAyLjM2My0yLjQ0MyA0LjI4NS01LjQ0NCA0LjI4NS0zLjAwMiAwLTUuNDQ1LTEuOTIyLTUuNDQ1LTQuMjg1Wm0xMS41MzggNi4wNDF2LS4wMDFzLTEuMjE1LTEuNzU4LS41OTYtMi40MjNsLS4wMjguMDE1Yy45NTMtLjk5OCAxLjUyOC0yLjI2IDEuNTI4LTMuNjMzIDAtMy4yMi0zLjEzOS01Ljg0MS02Ljk5Ny01Ljg0MUM0LjEzOSAxLjc3OCAxIDQuMzk4IDEgNy42MTlzMy4xMzkgNS44NDIgNi45OTggNS44NDJhOC4wODggOC4wODggMCAwIDAgMy4wNzYtLjYwM2MuNzguNzk1IDEuNzc4IDEuMjIyIDIuNTE4IDEuMzM2bC4wMDMtLjAwMmMuMDQuMDEzLjA4Mi4wMjYuMTI3LjAyNmEuNC40IDAgMCAwIC4zNjktLjU1N1oiIGlkPSJhIi8+PC9kZWZzPjx1c2UgZmlsbD0iIzNGNEY3MSIgeGxpbms6aHJlZj0iI2EiIGZpbGwtcnVsZT0iZXZlbm9kZCIvPjwvc3ZnPiovCmEuY29udGVudC10eXBlLWNvbW1lbnQgc3BhbiwKZGl2LmNvbnRlbnQtdHlwZS1jb21tZW50LApzcGFuLmNvbnRlbnQtdHlwZS1jb21tZW50LAouaWNvbi1jb21tZW50IHsKICAgIGJhY2tncm91bmQ6IG5vLXJlcGVhdCBsZWZ0IGNlbnRlciB1cmwoZGF0YTppbWFnZS9zdmcreG1sLCUzQ3N2ZyUyMHdpZHRoJTNEJTIyMTYlMjIlMjBoZWlnaHQlM0QlMjIxNiUyMiUyMHhtbG5zJTNEJTIyaHR0cCUzQS8vd3d3LnczLm9yZy8yMDAwL3N2ZyUyMiUyMHhtbG5zJTNBeGxpbmslM0QlMjJodHRwJTNBLy93d3cudzMub3JnLzE5OTkveGxpbmslMjIlM0UlM0NkZWZzJTNFJTNDcGF0aCUyMGQlM0QlMjJNMi41NTMlMjA3LjYyYzAtMi4zNjMlMjAyLjQ0My00LjI4NSUyMDUuNDQ1LTQuMjg1JTIwMy4wMDElMjAwJTIwNS40NDQlMjAxLjkyMiUyMDUuNDQ0JTIwNC4yODQlMjAwJTIwMi4zNjMtMi40NDMlMjA0LjI4NS01LjQ0NCUyMDQuMjg1LTMuMDAyJTIwMC01LjQ0NS0xLjkyMi01LjQ0NS00LjI4NVptMTEuNTM4JTIwNi4wNDF2LS4wMDFzLTEuMjE1LTEuNzU4LS41OTYtMi40MjNsLS4wMjguMDE1Yy45NTMtLjk5OCUyMDEuNTI4LTIuMjYlMjAxLjUyOC0zLjYzMyUyMDAtMy4yMi0zLjEzOS01Ljg0MS02Ljk5Ny01Ljg0MUM0LjEzOSUyMDEuNzc4JTIwMSUyMDQuMzk4JTIwMSUyMDcuNjE5czMuMTM5JTIwNS44NDIlMjA2Ljk5OCUyMDUuODQyYTguMDg4JTIwOC4wODglMjAwJTIwMCUyMDAlMjAzLjA3Ni0uNjAzYy43OC43OTUlMjAxLjc3OCUyMDEuMjIyJTIwMi41MTglMjAxLjMzNmwuMDAzLS4wMDJjLjA0LjAxMy4wODIuMDI2LjEyNy4wMjZhLjQuNCUyMDAlMjAwJTIwMCUyMC4zNjktLjU1N1olMjIlMjBpZCUzRCUyMmElMjIvJTNFJTNDL2RlZnMlM0UlM0N1c2UlMjBmaWxsJTNEJTIyJTIzM0Y0RjcxJTIyJTIweGxpbmslM0FocmVmJTNEJTIyJTIzYSUyMiUyMGZpbGwtcnVsZSUzRCUyMmV2ZW5vZGQlMjIvJTNFJTNDL3N2ZyUzRSk7Owp9Ci8qPHN2ZyB3aWR0aD0iMTgiIGhlaWdodD0iMTgiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyIgeG1sbnM6eGxpbms9Imh0dHA6Ly93d3cudzMub3JnLzE5OTkveGxpbmsiPjxkZWZzPjxwYXRoIGlkPSJhIiBkPSJNMi41NTMgNy42MmMwLTIuMzYzIDIuNDQzLTQuMjg1IDUuNDQ1LTQuMjg1IDMuMDAxIDAgNS40NDQgMS45MjIgNS40NDQgNC4yODQgMCAyLjM2My0yLjQ0MyA0LjI4NS01LjQ0NCA0LjI4NS0zLjAwMiAwLTUuNDQ1LTEuOTIyLTUuNDQ1LTQuMjg1em0xMS41MzggNi4wNDF2LS4wMDFzLTEuMjE1LTEuNzU4LS41OTYtMi40MjNsLS4wMjguMDE1Yy45NTMtLjk5OCAxLjUyOC0yLjI2IDEuNTI4LTMuNjMzIDAtMy4yMi0zLjEzOS01Ljg0MS02Ljk5Ny01Ljg0MUM0LjEzOSAxLjc3OCAxIDQuMzk4IDEgNy42MTlzMy4xMzkgNS44NDIgNi45OTggNS44NDJhOC4wODggOC4wODggMCAwIDAgMy4wNzYtLjYwM2MuNzguNzk1IDEuNzc4IDEuMjIyIDIuNTE4IDEuMzM2bC4wMDMtLjAwMmEuNDEuNDEgMCAwIDAgLjEyNy4wMjYuNC40IDAgMCAwIC4zNjktLjU1N3oiLz48L2RlZnM+PHVzZSB4bGluazpocmVmPSIjYSIgZmlsbD0iIzNGNEY3MSIgZmlsbC1ydWxlPSJldmVub2RkIi8+PHJlY3Qgcng9Ii45IiBoZWlnaHQ9IjQuMTg1IiB3aWR0aD0iMi4wMzEiIHk9IjQuMjcxIiB4PSI2LjgzOCIgZmlsbD0iIzNGNEY3MSIvPjxlbGxpcHNlIHJ5PSIuODA4IiByeD0iLjg2OCIgY3k9IjkuNjk1IiBjeD0iNy44MDIiIGZpbGw9IiMzRjRGNzEiLz48L3N2Zz4qLwouaWNvbi1vcGVuLWNvbW1lbnQgewogICAgYmFja2dyb3VuZDogbm8tcmVwZWF0IGxlZnQgY2VudGVyIHVybChkYXRhOmltYWdlL3N2Zyt4bWwsJTNDc3ZnJTIwd2lkdGglM0QlMjIxNiUyMiUyMGhlaWdodCUzRCUyMjE2JTIyJTIweG1sbnMlM0QlMjJodHRwJTNBLy93d3cudzMub3JnLzIwMDAvc3ZnJTIyJTIweG1sbnMlM0F4bGluayUzRCUyMmh0dHAlM0EvL3d3dy53My5vcmcvMTk5OS94bGluayUyMiUzRSUzQ2RlZnMlM0UlM0NwYXRoJTIwaWQlM0QlMjJhJTIyJTIwZCUzRCUyMk0yLjU1MyUyMDcuNjJjMC0yLjM2MyUyMDIuNDQzLTQuMjg1JTIwNS40NDUtNC4yODUlMjAzLjAwMSUyMDAlMjA1LjQ0NCUyMDEuOTIyJTIwNS40NDQlMjA0LjI4NCUyMDAlMjAyLjM2My0yLjQ0MyUyMDQuMjg1LTUuNDQ0JTIwNC4yODUtMy4wMDIlMjAwLTUuNDQ1LTEuOTIyLTUuNDQ1LTQuMjg1em0xMS41MzglMjA2LjA0MXYtLjAwMXMtMS4yMTUtMS43NTgtLjU5Ni0yLjQyM2wtLjAyOC4wMTVjLjk1My0uOTk4JTIwMS41MjgtMi4yNiUyMDEuNTI4LTMuNjMzJTIwMC0zLjIyLTMuMTM5LTUuODQxLTYuOTk3LTUuODQxQzQuMTM5JTIwMS43NzglMjAxJTIwNC4zOTglMjAxJTIwNy42MTlzMy4xMzklMjA1Ljg0MiUyMDYuOTk4JTIwNS44NDJhOC4wODglMjA4LjA4OCUyMDAlMjAwJTIwMCUyMDMuMDc2LS42MDNjLjc4Ljc5NSUyMDEuNzc4JTIwMS4yMjIlMjAyLjUxOCUyMDEuMzM2bC4wMDMtLjAwMmEuNDEuNDElMjAwJTIwMCUyMDAlMjAuMTI3LjAyNi40LjQlMjAwJTIwMCUyMDAlMjAuMzY5LS41NTd6JTIyLyUzRSUzQy9kZWZzJTNFJTNDdXNlJTIweGxpbmslM0FocmVmJTNEJTIyJTIzYSUyMiUyMGZpbGwlM0QlMjIlMjMzRjRGNzElMjIlMjBmaWxsLXJ1bGUlM0QlMjJldmVub2RkJTIyLyUzRSUzQ3JlY3QlMjByeCUzRCUyMi45JTIyJTIwaGVpZ2h0JTNEJTIyNC4xODUlMjIlMjB3aWR0aCUzRCUyMjIuMDMxJTIyJTIweSUzRCUyMjQuMjcxJTIyJTIweCUzRCUyMjYuODM4JTIyJTIwZmlsbCUzRCUyMiUyMzNGNEY3MSUyMi8lM0UlM0NlbGxpcHNlJTIwcnklM0QlMjIuODA4JTIyJTIwcnglM0QlMjIuODY4JTIyJTIwY3klM0QlMjI5LjY5NSUyMiUyMGN4JTNEJTIyNy44MDIlMjIlMjBmaWxsJTNEJTIyJTIzM0Y0RjcxJTIyLyUzRSUzQy9zdmclM0UpOwp9Ci8qPHN2ZyB3aWR0aD0iMTgiIGhlaWdodD0iMTgiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyIgeG1sbnM6eGxpbms9Imh0dHA6Ly93d3cudzMub3JnLzE5OTkveGxpbmsiPjxkZWZzPjxwYXRoIGQ9Ik0xIDh2Ni4xODJhLjg2Mi44NjIgMCAwIDAgLjg1Ny44ODNoLjg5MlY3LjExN2gtLjg5MkEuODYyLjg2MiAwIDAgMCAxLjAwMSA4Wm0xMy4zNzEtLjg0OGEyLjY1NSAyLjY1NSAwIDAgMC0yLjAzMS0uOTM3aC0yLjMwNWMuMDM1LS4xNi4wNy0uMzE4LjA5Ny0uNDcuNDUtMi41OSAwLTMuNTMyLS40NDItNC4wNTdBMi4xMiAyLjEyIDAgMCAwIDguMDY2LjkzNWEyLjcwMyAyLjcwMyAwIDAgMC0yLjU5NyAyLjQzYy0uMzQ0IDEuNjI0LS4zOTcgMS43NjYtLjg0OCAyLjQwNmwtLjY3Ljk2MmExLjc4NyAxLjc4NyAwIDAgMC0uMzE5IDEuMDA3djUuNTRhMS43NyAxLjc3IDAgMCAwIDEuNzc1IDEuNzY2aDYuMzQxYTIuNjQ0IDIuNjQ0IDAgMCAwIDIuNjIzLTIuMjE0bC41OTItMy41MzJhMi42MzMgMi42MzMgMCAwIDAtLjU5Mi0yLjE0OFptLTEuNzQ5IDUuMzg3YS44ODMuODgzIDAgMCAxLS44NzQuNzM4aC02LjM0VjcuNzRsLjY3LS45NjFhNi43MyA2LjczIDAgMCAwIDEuMTQtMy4xYy4wMzItLjQ3OC4zNzgtLjg3OC44NDgtLjk3OC42NyAwIC41MDMgMS42OTIuMzE3IDIuNzM4YTE3LjUzIDE3LjUzIDAgMCAxLS43NiAyLjU0NWw0LjcxNy0uMDA3YS44ODMuODgzIDAgMCAxIC44NzQgMS4wMjlsLS41OTIgMy41MzNaIiBpZD0iYSIvPjwvZGVmcz48dXNlIGZpbGw9IiMzRjRGNzEiIGZpbGwtcnVsZT0ibm9uemVybyIgeGxpbms6aHJlZj0iI2EiLz48L3N2Zz4qLwouaWNvbi1saWtlIHsKICAgIGJhY2tncm91bmQ6IG5vLXJlcGVhdCBsZWZ0IGNlbnRlciB1cmwoZGF0YTppbWFnZS9zdmcreG1sLCUzQ3N2ZyUyMHdpZHRoJTNEJTIyMTYlMjIlMjBoZWlnaHQlM0QlMjIxNiUyMiUyMHhtbG5zJTNEJTIyaHR0cCUzQS8vd3d3LnczLm9yZy8yMDAwL3N2ZyUyMiUyMHhtbG5zJTNBeGxpbmslM0QlMjJodHRwJTNBLy93d3cudzMub3JnLzE5OTkveGxpbmslMjIlM0UlM0NkZWZzJTNFJTNDcGF0aCUyMGQlM0QlMjJNMSUyMDh2Ni4xODJhLjg2Mi44NjIlMjAwJTIwMCUyMDAlMjAuODU3Ljg4M2guODkyVjcuMTE3aC0uODkyQS44NjIuODYyJTIwMCUyMDAlMjAwJTIwMS4wMDElMjA4Wm0xMy4zNzEtLjg0OGEyLjY1NSUyMDIuNjU1JTIwMCUyMDAlMjAwLTIuMDMxLS45MzdoLTIuMzA1Yy4wMzUtLjE2LjA3LS4zMTguMDk3LS40Ny40NS0yLjU5JTIwMC0zLjUzMi0uNDQyLTQuMDU3QTIuMTIlMjAyLjEyJTIwMCUyMDAlMjAwJTIwOC4wNjYuOTM1YTIuNzAzJTIwMi43MDMlMjAwJTIwMCUyMDAtMi41OTclMjAyLjQzYy0uMzQ0JTIwMS42MjQtLjM5NyUyMDEuNzY2LS44NDglMjAyLjQwNmwtLjY3Ljk2MmExLjc4NyUyMDEuNzg3JTIwMCUyMDAlMjAwLS4zMTklMjAxLjAwN3Y1LjU0YTEuNzclMjAxLjc3JTIwMCUyMDAlMjAwJTIwMS43NzUlMjAxLjc2Nmg2LjM0MWEyLjY0NCUyMDIuNjQ0JTIwMCUyMDAlMjAwJTIwMi42MjMtMi4yMTRsLjU5Mi0zLjUzMmEyLjYzMyUyMDIuNjMzJTIwMCUyMDAlMjAwLS41OTItMi4xNDhabS0xLjc0OSUyMDUuMzg3YS44ODMuODgzJTIwMCUyMDAlMjAxLS44NzQuNzM4aC02LjM0VjcuNzRsLjY3LS45NjFhNi43MyUyMDYuNzMlMjAwJTIwMCUyMDAlMjAxLjE0LTMuMWMuMDMyLS40NzguMzc4LS44NzguODQ4LS45NzguNjclMjAwJTIwLjUwMyUyMDEuNjkyLjMxNyUyMDIuNzM4YTE3LjUzJTIwMTcuNTMlMjAwJTIwMCUyMDEtLjc2JTIwMi41NDVsNC43MTctLjAwN2EuODgzLjg4MyUyMDAlMjAwJTIwMSUyMC44NzQlMjAxLjAyOWwtLjU5MiUyMDMuNTMzWiUyMiUyMGlkJTNEJTIyYSUyMi8lM0UlM0MvZGVmcyUzRSUzQ3VzZSUyMGZpbGwlM0QlMjIlMjMzRjRGNzElMjIlMjBmaWxsLXJ1bGUlM0QlMjJub256ZXJvJTIyJTIweGxpbmslM0FocmVmJTNEJTIyJTIzYSUyMi8lM0UlM0Mvc3ZnJTNFKTsKfQovKjxzdmcgd2lkdGg9IjE4IiBoZWlnaHQ9IjE4IiB2aWV3Qm94PSIwIDAgMTYgMTYiIHhtbG5zPSJodHRwOi8vd3d3LnczLm9yZy8yMDAwL3N2ZyIgeG1sbnM6eGxpbms9Imh0dHA6Ly93d3cudzMub3JnLzE5OTkveGxpbmsiPjxkZWZzPjxwYXRoIGQ9Ik04IDFhNyA3IDAgMSAxIDAgMTRBNyA3IDAgMCAxIDggMVptMCAxMi41YzMuMDMzIDAgNS41LTIuNDY3IDUuNS01LjVTMTEuMDMzIDIuNSA4IDIuNUE1LjUwNiA1LjUwNiAwIDAgMCAyLjUgOGMwIDMuMDMzIDIuNDY3IDUuNSA1LjUgNS41Wm0tMS41LTZhMSAxIDAgMSAxIDAtMiAxIDEgMCAwIDEgMCAyWm0zIDBhMSAxIDAgMSAxIDAtMiAxIDEgMCAwIDEgMCAyWm0uMjcgMS41ODNhLjYyNi42MjYgMCAwIDEgLjkzMi44MzRBMy42MyAzLjYzIDAgMCAxIDggMTEuMTI1YTMuNjMgMy42MyAwIDAgMS0yLjY5OC0xLjIwNC42MjUuNjI1IDAgMCAxIC45My0uODM1Yy45MDEgMS4wMDMgMi42MzkgMS4wMDMgMy41MzgtLjAwM1oiIGlkPSJhIi8+PC9kZWZzPjx1c2UgZmlsbD0iIzQ1NTI2QyIgZmlsbC1ydWxlPSJub256ZXJvIiB4bGluazpocmVmPSIjYSIvPjwvc3ZnPiovCi5pY29uLXJlYWN0aW9uIHsKICAgIGJhY2tncm91bmQ6IG5vLXJlcGVhdCBsZWZ0IGNlbnRlciB1cmwoZGF0YTppbWFnZS9zdmcreG1sLCUzQ3N2ZyUyMHdpZHRoJTNEJTIyMTYlMjIlMjBoZWlnaHQlM0QlMjIxNiUyMiUyMHZpZXdCb3glM0QlMjIwJTIwMCUyMDE2JTIwMTYlMjIlMjB4bWxucyUzRCUyMmh0dHAlM0EvL3d3dy53My5vcmcvMjAwMC9zdmclMjIlMjB4bWxucyUzQXhsaW5rJTNEJTIyaHR0cCUzQS8vd3d3LnczLm9yZy8xOTk5L3hsaW5rJTIyJTNFJTNDZGVmcyUzRSUzQ3BhdGglMjBkJTNEJTIyTTglMjAxYTclMjA3JTIwMCUyMDElMjAxJTIwMCUyMDE0QTclMjA3JTIwMCUyMDAlMjAxJTIwOCUyMDFabTAlMjAxMi41YzMuMDMzJTIwMCUyMDUuNS0yLjQ2NyUyMDUuNS01LjVTMTEuMDMzJTIwMi41JTIwOCUyMDIuNUE1LjUwNiUyMDUuNTA2JTIwMCUyMDAlMjAwJTIwMi41JTIwOGMwJTIwMy4wMzMlMjAyLjQ2NyUyMDUuNSUyMDUuNSUyMDUuNVptLTEuNS02YTElMjAxJTIwMCUyMDElMjAxJTIwMC0yJTIwMSUyMDElMjAwJTIwMCUyMDElMjAwJTIwMlptMyUyMDBhMSUyMDElMjAwJTIwMSUyMDElMjAwLTIlMjAxJTIwMSUyMDAlMjAwJTIwMSUyMDAlMjAyWm0uMjclMjAxLjU4M2EuNjI2LjYyNiUyMDAlMjAwJTIwMSUyMC45MzIuODM0QTMuNjMlMjAzLjYzJTIwMCUyMDAlMjAxJTIwOCUyMDExLjEyNWEzLjYzJTIwMy42MyUyMDAlMjAwJTIwMS0yLjY5OC0xLjIwNC42MjUuNjI1JTIwMCUyMDAlMjAxJTIwLjkzLS44MzVjLjkwMSUyMDEuMDAzJTIwMi42MzklMjAxLjAwMyUyMDMuNTM4LS4wMDNaJTIyJTIwaWQlM0QlMjJhJTIyLyUzRSUzQy9kZWZzJTNFJTNDdXNlJTIwZmlsbCUzRCUyMiUyMzQ1NTI2QyUyMiUyMGZpbGwtcnVsZSUzRCUyMm5vbnplcm8lMjIlMjB4bGluayUzQWhyZWYlM0QlMjIlMjNhJTIyLyUzRSUzQy9zdmclM0UpOwp9CgoucmVhY3Rpb25zLWNvbnRhaW5lciB7CiAgICBtYXJnaW4tYm90dG9tOiAzcHg7CiAgICBtYXJnaW4tbGVmdDogMnB4OwogICAgbWFyZ2luLXJpZ2h0OiAtMnB4Owp9CgoucmVhY3Rpb25zLWltYWdlIHsKICAgIGhlaWdodDogMjBweDsKICAgIHdpZHRoOiAyMHB4Owp9CgojdHJlZS1yb290LWRpdiBsaSBhLAphLmNvbnRlbnQtdHlwZS1zcGFjZSBzcGFuLApkaXYuY29udGVudC10eXBlLXNwYWNlLApzcGFuLmNvbnRlbnQtdHlwZS1zcGFjZSwKYS5jb250ZW50LXR5cGUtc3BhY2VkZXNjIHNwYW4sCmRpdi5jb250ZW50LXR5cGUtc3BhY2VkZXNjLApzcGFuLmNvbnRlbnQtdHlwZS1zcGFjZWRlc2MsCi5pY29uLXNwYWNlLAouaWNvbi1jcmVhdGUtc3BhY2UsCi5pY29uLWJyb3dzZS1zcGFjZSB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQTIwbEVRVlFvejMyUnNXckRNQlJGMzdmNEM0MmdZRHhsMGVRaGVNZ2c2S3JaUStnM21FS0dUcVdMQ2lWNHlISWhZNmJUUVlxcTBOTDdRQ0IwT0tCM3pVcGltSk1qejV4aXNEWkw1elh5ckpNdXV1aWt3SURYMHBYbll6ZXdSeUw5ekVXZWdXTkd2SGIwM1BValo1RklWKzN4TXJNWW5uam5SczZOdmlKbk9XS3dTU3R0ZXBhS3pFd3kxOGczb0FkaVFWWTVySCtRYi9SbFJraHFnYnQ4S3pjSDZTcUh1UWJJOHEwQ3IzTFlwSzhLVkhrQkRreXlHQTQ4eHBYekkzL1R6T3Z0VDJES2k4cXJYbjhCSXdNdlhWTlc0TE1DaWRDVzlYL2QzOElzYzZOQS9CZjBBQUFBQUVsRlRrU3VRbUNDKTsKICAgIGJhY2tncm91bmQtcmVwZWF0OiBuby1yZXBlYXQ7Cn0KCmEuY29udGVudC10eXBlLXVzZXIgc3BhbiwKZGl2LmNvbnRlbnQtdHlwZS11c2VyLApzcGFuLmNvbnRlbnQtdHlwZS11c2VyLAphLmNvbnRlbnQtdHlwZS11c2VyaW5mbyBzcGFuLApkaXYuY29udGVudC10eXBlLXVzZXJpbmZvLApzcGFuLmNvbnRlbnQtdHlwZS11c2VyaW5mbywKLmljb24tdXNlciB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQVprbEVRVlFvejJOZ2dJTUN3WUxkQmYrQmNIZUJJQU0yQUpVR0s4R3U0RDhDa3FtQW9CV0VIRWtRRkpRRGRiOER3djhGSGRpa1E1RWRXUkNLYWY4N0ZBWHYwTnhSMElFaURZTGxxQXJPWUNpNGl5eXRoQ0VOZ3NZSUJXbFlGWlJqQmhFeUpEYTRBSnF3bFlDRkxkYmRBQUFBQUVsRlRrU3VRbUNDKTsKICAgIGJhY2tncm91bmQtcmVwZWF0OiBuby1yZXBlYXQ7Cn0KCi51aS10cmVlIGxpIGEuaG9tZS1ub2RlLAouaWNvbi1ob21lLXBhZ2UgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBTUFBQUFvTFE5VEFBQUFTRkJNVkVYLy8vKzF0Yld3c0xDdHJhMy8vLytMaTR1cnE2dWhvYUg1K2ZuR3hzYjgvUHpaMmRuVDA5UFIwZEhQejg5d2NIQnljbkovZjM5MWRYVjZlbnA0ZUhoOGZIeUFnSUNEZzROM2VOKzJBQUFBRDNSU1RsTUFJaUpWM2U3dTd1N3U3dTd1N3U0UHYxMk5BQUFBYVVsRVFWUjQyblhLU1JLQUlBeEUwYWdKNE5oUkhPNS9VMFVRZE9HdjN1UlZpS2dXUUsvV2htTGlCOGZNRGo2Sjlud0hrMzZVRTdEMXpRZXUxZ2dsemRDaS9ZQlpzSmdYMkJuQWJBdHNRSkFDRElUOWd1T25Mc0tZb2I5QmpxbUw5N0JMZ0VvMEJhbm9CS1BhQnh0bmNQQ0lBQUFBQUVsRlRrU3VRbUNDKTsKfQoKYS5jb250ZW50LXR5cGUtcGVyc29uYWxzcGFjZWRlc2Mgc3BhbiwKZGl2LmNvbnRlbnQtdHlwZS1wZXJzb25hbHNwYWNlZGVzYywKc3Bhbi5jb250ZW50LXR5cGUtcGVyc29uYWxzcGFjZWRlc2MsCi5pY29uLXBlcnNvbmFsLXNwYWNlIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVFBQUFDMStqZnFBQUFBNGtsRVFWUW96MzFSc1FxRFFBeTliN2t2bE1QaEtLNU9EbklVQ2tKWHB5NEY2ZGE5RkFTMzR1TFNwVGdZY0NnZFgzTkc2Z2x0NzAyWDk1SzhKRXJOcnl4Y1p5QndYVm1vOEIxMVNnbjJWRlBQcUttQVJVcEhQZE9WdHNneEVMb0ZQYVVzcWtTUzBoWXZwcC9rRURFY252d2JLZWNxVSs5NHpoWmFKUDUvSndQMmtuRkhLUnRoZ1VRY01sSUdOL29sdUhBTkZVMzl2N1ZBTjZ3RmE1TWVveGNZdEtzQlExeTlZREY1cG9obnQxempNS2Zzdk1teU1MaHpvS0hRWk1PUlZzYVVSVDNJSWhSWWptU3lLRmwxc3FJOUVoYWRkSENzR0pzUHVVRWNIdXYvdWQ4THRtRjFBRERTYndBQUFBQkpSVTVFcmtKZ2dnPT0pOwogICAgYmFja2dyb3VuZC1yZXBlYXQ6IG5vLXJlcGVhdDsKfQoKYS5jb250ZW50LXR5cGUtc3RhdHVzIHNwYW4sCmRpdi5jb250ZW50LXR5cGUtc3RhdHVzLApzcGFuLmNvbnRlbnQtdHlwZS1zdGF0dXMsCi5pY29uLXN0YXR1cyB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQXkwbEVRVlFvejVYUE1ZckNRQmpGOFp3bFY4aFpjb1hVandHSlREZFlSQllzTGFaS1plRXVLMGlhaGJSN0NHRkpZV001Z28xWS9TMGNjQkprd2RlK0gyKyt5YklrUDNuYk93d0dnMlU5ZEVYYVp2dlNzcURqQkp6NXBjSFNsUWx3ckxud3pKVVdSd0lNUjhiNXd6eUFLcUhTNGJtTndEZnpDSUpRdnk4dEgzSGx4Z0dQWWVzZkFDSFV0NFZqZzhjeXc5Q0VYUjFmMXhCSnZRb0d4OWR5VjQrK3FFS0RVRkRSaERqNlpyUVU2b1dxMTNVVkwrQWxVYTZRZ0tCOENueFNJelE5Y2xJanB1RHovNFU3N3pEYUVobzdDOTRBQUFBQVNVVk9SSzVDWUlJPSk7CiAgICBiYWNrZ3JvdW5kLXJlcGVhdDogbm8tcmVwZWF0Owp9CgphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LWNzcyBzcGFuLApkaXYuY29udGVudC10eXBlLWF0dGFjaG1lbnQtY3NzLApzcGFuLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LWNzcywKLmljb24tZmlsZS1jc3MsCmEuY29udGVudC10eXBlLWF0dGFjaG1lbnQtamF2YSBzcGFuLApkaXYuY29udGVudC10eXBlLWF0dGFjaG1lbnQtamF2YSwKc3Bhbi5jb250ZW50LXR5cGUtYXR0YWNobWVudC1qYXZhLAouaWNvbi1maWxlLWphdmEsCmEuY29udGVudC10eXBlLWF0dGFjaG1lbnQtdGV4dC1odG1sIHNwYW4sCmRpdi5jb250ZW50LXR5cGUtYXR0YWNobWVudC10ZXh0LWh0bWwsCnNwYW4uY29udGVudC10eXBlLWF0dGFjaG1lbnQtdGV4dC1odG1sLAphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LWh0bWwgc3BhbiwKZGl2LmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LWh0bWwsCnNwYW4uY29udGVudC10eXBlLWF0dGFjaG1lbnQtaHRtbCwKYS5jb250ZW50LXR5cGUtYXR0YWNobWVudC10ZXh0LXhtbCBzcGFuLApkaXYuY29udGVudC10eXBlLWF0dGFjaG1lbnQtdGV4dC14bWwsCnNwYW4uY29udGVudC10eXBlLWF0dGFjaG1lbnQtdGV4dC14bWwsCmEuY29udGVudC10eXBlLWF0dGFjaG1lbnQteG1sIHNwYW4sCmRpdi5jb250ZW50LXR5cGUtYXR0YWNobWVudC14bWwsCnNwYW4uY29udGVudC10eXBlLWF0dGFjaG1lbnQteG1sLAphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LWpzIHNwYW4sCmRpdi5jb250ZW50LXR5cGUtYXR0YWNobWVudC1qcywKc3Bhbi5jb250ZW50LXR5cGUtYXR0YWNobWVudC1qcywKLmljb24tZmlsZS1qcywKLmljb24tZmlsZS1odG1sLAouaWNvbi1maWxlLXhtbCB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQW8wbEVRVlI0QVlYUk1XcENRUkNBNFhjL0x6T0N2bE5vNFJVU0N3L3dnaGFld1VJUWt0ckZ3aHNJeWhjTGw4VWg0YzJXK3hVL00xMU1adGZ3OTN2K1RMclo5ZVMvT1QxSkY5N254MHFiOEFKbkN3VjhHVExnWUc3akFWYStNeGlFSGVDbWQ4c2dmS2FDWXVIY3dON1VGcTNnWVdQdVVBRkh2Ylg3VzhGT0dDcWdXTHFrZ2cvUkFIa0hXMVA3RE9vTzd0WjZSektvQlJkTGhRUlNnUXJHanpWeTdsOW9Lc1JZWnFVZitBQUFBQUJKUlU1RXJrSmdnZz09KTsKICAgIGJhY2tncm91bmQtcmVwZWF0OiBuby1yZXBlYXQ7Cn0KLypFbmQgUERMIGljb25zLmNzcyovCgovKk5PTiBQREwgaWNvbnMgKFRvIGJlIHVwZGF0ZWQpKi8KLmljb24tYWRkLXBhZ2UgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBUUFBQUMxK2pmcUFBQUFMMGxFUVZRb3oyTmdvRElvbUZud3YyQW1QZ1gvUVpBbUNzQjJvOE9aR0RyUklTRVRWdEhYRnlodVdjVkFYd0FBekRoT3FpK0t3TzhBQUFBQVNVVk9SSzVDWUlJPSk7Cn0KCi5pY29uLWFkZC1wYWdlLWRpc2FibGVkIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL2dpZjtiYXNlNjQsUjBsR09EbGhFQUFRQVBNTUFNek16UGYzOStibTV0Ylcxc1BEdy8vLy8rN3U3cjYrdnQ3ZTNyVzF0YXlzckthbXBnQUFBQUFBQUFBQUFBQUFBQ0g1QkFVVUFBd0FMQUFBQUFBUUFCQUFRQVJrVU1oSkJic1hsY0I3R0FRaEtBUWlGSnRYQ0VNN1hNTldkY054WENlcW80SVJXTGlOWVVnY0lnaUFqTEJvR0NRU2lNVUJFZHRaVVM5RzdpcjRZYlFvNW1yb0JWL0RRNkRHd3lZRUVzcGZKVEV3QVFTRUdOdHpHQ2hnQlV4aVdTNkZoaTBYRVFBNyk7Cn0KCi5pY29uLXJlY2VudGx5LXVwZGF0ZWQtcGFnZSB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FZQUFBQWY4LzloQUFBQ0dVbEVRVlI0MnBXU1MyOFNVUmlHK1NrbTdseTVkdVBLdmY2RENvV21FcVgvd0hJdHNVWVh4cDJYUnBSaEJnYTBGMjVWcWJwUU42YVNOREZOMVVSTTFBSmxLTU9aSzMzOXpnQU5IY1hFSjNselRpYm5QT2Y3emh6UG1QbmJiekIzNnhVQ04xL0N2L3dDZ2VYbjhOM1l4T1ZrQmQ2bE1tWVNKYXhzMUM5NnB1R25qZE1vdi8rS2puS0k4SU10UEZ6ZkR2NVYwTzVxNEZqV0FLcG1vTlBUMGV3d2ZQK2w0UDdhTm4yM3dCZ2pTUTNoZTFzbkphZkUvZEM1dFJiT2I3UlJiT2l3YlpJd0V3cEpXZ3JEeXZwSGNFelRoS3FxdUNPOXhkM3N1K0I0OHdVS3hqa3Q3V08zYThPd2JERGRJcEdGUjZVNk9JUEJBSnFtT1pLWmlJeXhJRWJCWk5LZk5WcDhCSlBhTVV3YlFuVUh3dVlPNU5vblBIdTlpOXFIYi9DR2M4ZUNTMjVCL2NCeUJEYUYybkdxNld2bThGNFVGUnhmTkl2Sk93aWZrWnM0VzJnaXRjZXc5NlVCTVYrbFZKQ1JxOGprS1hJRlQ3SmxwTVFTT1A0WVZUQ0pibGo0Rjd3U3l4cUdFNGhsVHdwODhWVnd4RUxWT1YwYWoxU0ZJSmNoNU10SVV3V1B4U0k0OC9HY1N4QjdpbWtjRGYvQXFBcDdKSkJjZ21nQkJEK045MzRjZ1hwUHk2WGg2VklScVZFRndjUWZnanc0ZldaQ00wd1lwdVZFcHpuVERQVDZPcFREUHIxT0ZaeHJTZmNkUkhJZytPdHpGcXVNeDNER2JxK1BkcWVIbnkwRmpSOEg0Q3drSmJjZ2kvOGh0T1FTekVaRWVCY3o4RjZuTEFyd09VbGpsc1pBT0kyNWlJQXJVUUZYNHdKQ2lRd1dLQjdpTjRvVndJQUtVUFkwQUFBQUFFbEZUa1N1UW1DQyk7Cn0KCi5pY29uLWdyb3VwIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVFBQUFDMStqZnFBQUFBbjBsRVFWUjRBWVhQUVEzRE1BeEFVVk13aFZBb2hWSW9oWnovclJSQ1lSUktJUlJHd1JSQ3dXdFhTOWJVU01zLytrVng1UGVnZFB5c284aVZjRmRZS1VpTWI1S0FpbkZnVkR4TFVEQVVRUmx6ME03a1c4ZG1UMVJlQVl4MXZxVFJVQTRha2lWWUdTd0lGV1hIR1dkT1lFRXhsdkFibnJIZG9NVUd6MThNOUFKR0NkQmlrTzBYY0NSNlA0QWhlYS9na3hZaG9rN0Jua0RwajNGSGhUOTlBT2NWNWJ6MjZCdTZBQUFBQUVsRlRrU3VRbUNDKTsKfQoKLmljb24tdHJhY2tiYWNrIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL2dpZjtiYXNlNjQsUjBsR09EbGhFQUFRQU9ZQUFBNStDOWpvMkc5L29tZDVsMjdtU3B5cndFQzBQWFd1ZGVqMCt6RzBJRWFVWUxTODBPVHo4WUNQcnllZUxZTzZnMGJETDRYeld2ZjcvU09SSytEbzhEaWtPSXpTak1iVzFVVEJMVlcyVjVuL1pvUHhWMXpjUFhlSXJQRDQ4S1hHdERHb01TcXJISXF1bi9iNi9ZaVl1S2Exd2VUeCtuQ0FuMTZuYmgrYkdITzZmZHJ0K0VXMVNIT01yZS8zL0R1elBER3hNNG1ack5uczJUcTNKdlg4OWJ2RTEvLy8vMnQ4bTNDQ3A0T1R0SHFKcTRyMlhLdTF4blN6ZE9UMDZjN1U0WjZ5dGl5bE1PRHk2T24wK3RQbDRudU10ZS8vNzZhM3hFcTRTWXladE9iMzd3QUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUNINUJBVVVBRFlBTEFBQUFBQVFBQkFBQUFlMWdEYUNnNFNGZ2lRa09RMGRPRDhlQndHR0pJUTREVDhQQUQyRms0SVNPSjh5S1FtYWd3MkRFaElVSng0cEhDa2VwWjJvTlRVWElRUXpINE02Tmo0WlBpNGtGaUFRR3hnVEtvSWRIakFSTHd3a0VrZ1JHanNPUjRJbEJqc2FFU3dGTmpRdkVUQkVKNElvRlJnYkVFRUtna29aUWk0M2dqd2lNd1FoUUFzMnFCSXU2QTFpNWNwREJ4c2pBcnBBVUc5UWp3UXBaQWl3b1JDQmlRR0ZlZ0I0a01UR2tJOG1WbUFzRk9CQWpCc29VelkweEZKUUlBQTcpOwp9Ci5pY29uLW1haWwgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBWUFBQUFmOC85aEFBQUJrVWxFUVZSNEFjWFNUNGhTUVJ3SDhPK2JIcUpHU3d0QlJFdFFkRTZDSUFvNkNSMDJncWhUMUwxQTZOQ3BRNGNPUVVTbmlJNmRJaTlCaHkyS2hFQ1FvbEFybzZETWxBcDlodW42dHRUbmV6UHpiWWRTMEJCSkQzM2dCelBNSCtZM2ZERXZDelBZZGVyR1p2eld0dllta2d4SE40SmFRNitYUVpyeGNENnlwcFdDbE1GNitWQitBT3c1ZTV2dnY3YjRyOTZVYXR4Ky9DcUYxK3RnZ3hDNC82SUN0OVBITksyMUxwS3BseERDZ3RmckFydFAzNlR4dXRUZ3ZhZWZXSzY3REtTaVVwcEdYMnJXM1lBOVgvTERsd1p2UGNqeVNiWklZL0h3UldMbnllczAyajg5Zmx2dDhsSHVNd3VWN3pTOGdIVFdTS25KZk5IaDNmUTdWcHhWT3MwZk5CYmlGMmpqajBqSXhxYW9RRHkyaEV5aEJMVmpFU3J3c0dBTFNHbWpXcXZpeUlFWWxBWjhxVEVnbEZJWTV5T01oNWxYR0VnOUs2Q1BDTWFaczFnNmNXMWlDeXZwbkttSkxVUU9uU2UySGJzeTh5ZUdEcDRqdGg2OXpJL1ZObGVlbDgwck9FM1Q3ZkRPNHp6ZmxoM2EreE8wdGl4Zm9ra1pUZG80bWp3TzB2ZFhTdFZ3ajJYdE80TjVDQUQvOTRKZi9Jak5XUVdhQ3U4QUFBQUFTVVZPUks1Q1lJST0pOwp9CgouaWNvbi1zaG93LW1vcmUgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvZ2lmO2Jhc2U2NCxSMGxHT0RsaERBQU1BS0VEQUdSa1pKNmVuc0hCd2YvLy95SDVCQUVLQUFNQUxBQUFBQUFNQUF3QUFBSWpuQWNKeHlpeG1nRXZTZ3FaU25WVEFZWWdCQVJtOEp6azFpbGFKVTF3WEdhMGV4UUFPdz09KTsKfQouaWNvbi1zaG93LWxlc3MgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvZ2lmO2Jhc2U2NCxSMGxHT0RsaERBQU1BS0VEQUdSa1pKNmVuc0hCd2YvLy95SDVCQUVLQUFNQUxBQUFBQUFNQUF3QUFBSVZuSStweTMwQW80eWlXaHRnMkx6UER6bmlTQm9GQURzPSk7Cn0KCmEuY29udGVudC10eXBlLWF0dGFjaG1lbnQtdGV4dCBzcGFuLApkaXYuY29udGVudC10eXBlLWF0dGFjaG1lbnQtdGV4dCwKc3Bhbi5jb250ZW50LXR5cGUtYXR0YWNobWVudC10ZXh0LAouaWNvbi1maWxlLXRleHQgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBUUFBQUMxK2pmcUFBQUFOMGxFUVZSNEFXTW84QzU1VmZBZk93VEtlRE9VdkxyNkh4ZTRDbFRDVVBBZkR3Q2FRaFVGV0NBOXJDaWdxUlZVRHlpQ2tVVXd1Z0hxbDhoWjhhVldGd0FBQUFCSlJVNUVya0pnZ2c9PSk7CiAgICBiYWNrZ3JvdW5kLXJlcGVhdDogbm8tcmVwZWF0Owp9CgphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LWphciBzcGFuLApkaXYuY29udGVudC10eXBlLWF0dGFjaG1lbnQtamFyLApzcGFuLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LWphciwKLmljb24tZmlsZS1qYXIsCmEuY29udGVudC10eXBlLWF0dGFjaG1lbnQtemlwIHNwYW4sCmRpdi5jb250ZW50LXR5cGUtYXR0YWNobWVudC16aXAsCnNwYW4uY29udGVudC10eXBlLWF0dGFjaG1lbnQtemlwLAouaWNvbi1maWxlLXppcCB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FZQUFBQWY4LzloQUFBQVdrbEVRVlI0QVdNb0tDandMaWtwZVFXay81T0NvWHE4R1VDTXExZXYva2NIK1FVRllJd0xnUFNBOURJVW9Db2l5U0NRWHBnQmhEVVNOZ0FUakxwZzFBV0ZoWVhJbUhZRzRNcE1FRTBJTnM3TWhDczdvN3NBWjNZR0FPWjBYUVozQ0o3YkFBQUFBRWxGVGtTdVFtQ0MpOwogICAgYmFja2dyb3VuZC1yZXBlYXQ6IG5vLXJlcGVhdDsKfQoKLmljb24tZmlsZS13b3JkOTctdGVtcGxhdGUsCi5pY29uLWZpbGUtd29yZDk3LAouaWNvbi1maWxlLXdvcmQsCi5pY29uLWZpbGUtd29yZC10ZW1wbGF0ZSwKZGl2LmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LXdvcmQ5NywKc3Bhbi5jb250ZW50LXR5cGUtYXR0YWNobWVudC13b3JkOTcsCmEuY29udGVudC10eXBlLWF0dGFjaG1lbnQtd29yZDk3IHNwYW4sCmRpdi5jb250ZW50LXR5cGUtYXR0YWNobWVudC13b3JkLApzcGFuLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LXdvcmQsCmEuY29udGVudC10eXBlLWF0dGFjaG1lbnQtd29yZCBzcGFuIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVFBQUFDMStqZnFBQUFBWTBsRVFWUjRBV05vU2FuNVhmQWZPNno1M1pMQ1VQUDc2bjljNENwUUNVUEJmendBYUFvWkNwQmRBVkZRaUlBUUJVRDg2Lyt5Lzk5d20vRHhmOS8vcGYvci9wL0RwYUQrLzA0Z2ZmOS9KNW9WdU54QXVUY0xjVUFnb0lvVkJDT0xZSFFEQUtXendaZUxDU2RQQUFBQUFFbEZUa1N1UW1DQyk7CiAgICBiYWNrZ3JvdW5kLXJlcGVhdDogbm8tcmVwZWF0Owp9CgouaWNvbi1maWxlLWV4Y2VsOTctdGVtcGxhdGUsCi5pY29uLWZpbGUtZXhjZWw5NywKLmljb24tZmlsZS1leGNlbC1tYWNybywKLmljb24tZmlsZS1leGNlbCwKLmljb24tZmlsZS1leGNlbC10ZW1wbGF0ZSwKZGl2LmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LWV4Y2VsOTcsCnNwYW4uY29udGVudC10eXBlLWF0dGFjaG1lbnQtZXhjZWw5NywKYS5jb250ZW50LXR5cGUtYXR0YWNobWVudC1leGNlbDk3IHNwYW4sCmRpdi5jb250ZW50LXR5cGUtYXR0YWNobWVudC1leGNlbCwKc3Bhbi5jb250ZW50LXR5cGUtYXR0YWNobWVudC1leGNlbCwKYS5jb250ZW50LXR5cGUtYXR0YWNobWVudC1leGNlbCBzcGFuIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVFBQUFDMStqZnFBQUFBUGtsRVFWUjRBV01vOEM1NVZmQWZPd1RLZURPVXZMcjZId3lBQXVqZ0tsQUpBMGdZVXdGQ2pENEs4RUEwRXpCWnBDa1l4RlpnamF3Q3BNZ2lHTjBBeFArOER5eWdqZG9BQUFBQVNVVk9SSzVDWUlJPSk7CiAgICBiYWNrZ3JvdW5kLXJlcGVhdDogbm8tcmVwZWF0Owp9CgouaWNvbi1maWxlLXBvd2VycG9pbnQ5Ny10ZW1wbGF0ZSwKLmljb24tZmlsZS1wb3dlcnBvaW50OTcsCi5pY29uLWZpbGUtcG93ZXJwb2ludCwKLmljb24tZmlsZS1wb3dlcnBvaW50LW1hY3JvLAouaWNvbi1maWxlLXBvd2VycG9pbnQtc2xpZGVzaG93LAouaWNvbi1maWxlLXBvd2VycG9pbnQtdGVtcGxhdGUsCmRpdi5jb250ZW50LXR5cGUtYXR0YWNobWVudC1wb3dlcnBvaW50OTcsCnNwYW4uY29udGVudC10eXBlLWF0dGFjaG1lbnQtcG93ZXJwb2ludDk3LAphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LXBvd2VycG9pbnQ5NyBzcGFuLApkaXYuY29udGVudC10eXBlLWF0dGFjaG1lbnQtcG93ZXJwb2ludCwKc3Bhbi5jb250ZW50LXR5cGUtYXR0YWNobWVudC1wb3dlcnBvaW50LAphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LXBvd2VycG9pbnQgc3BhbiB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQWdFbEVRVlI0QVkyUk1Rb0NNUkJGYzcrOXpDK1dJRnFZQzZUMk5vSzFqY1VzekJrV0xJWHRubHRJcWxtVC84cDVmRWgrMGpTdkltYS9UR2xlRjQ2eTdFb1MveUthWUZReUZZc0ZvK0FJcDJDUlVIRkFnRk1qSWZQaGdYaXprUUhRajladzVjYUxNL2U0d2JqZ2JEdzVZU092VUdQOEh6cUNZb1lhK21OMTV2NENPKzYvOGZJcHBhNEFBQUFBU1VWT1JLNUNZSUk9KTsKICAgIGJhY2tncm91bmQtcmVwZWF0OiBuby1yZXBlYXQ7Cn0KCi5pY29uLWZpbGUtbXVsdGltZWRpYSwKZGl2LmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LW11bHRpbWVkaWEsCnNwYW4uY29udGVudC10eXBlLWF0dGFjaG1lbnQtbXVsdGltZWRpYSwKYS5jb250ZW50LXR5cGUtYXR0YWNobWVudC1tdWx0aW1lZGlhIHNwYW4gewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBUUFBQUMxK2pmcUFBQUFiMGxFUVZSNEFXTm9TYW41WGZBZk82ejUzWkxDVVBQNzZ2Ly8vMi8vcndKaWRQb3FVQWxEQVU1cElBQ2FBbFNBQjBJVTRBSG9DdDRRVWxEKy94eDJCUWh1Ni85bC8zL2lVL0RqLzlML2JmK2Y0RllBQW92L1YveC9SOEFFc3QxUS92OHM2ZUVBaml3Y0FCeFpCS01iQUFFQ3hIUTBmdjFlQUFBQUFFbEZUa1N1UW1DQyk7CiAgICBiYWNrZ3JvdW5kLXJlcGVhdDogbm8tcmVwZWF0Owp9CgphLmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LXVua25vd24gc3BhbiwKZGl2LmNvbnRlbnQtdHlwZS1hdHRhY2htZW50LXVua25vd24sCnNwYW4uY29udGVudC10eXBlLWF0dGFjaG1lbnQtdW5rbm93biwKLmljb24tZmlsZS11bmtub3duIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVFBQUFDMStqZnFBQUFBZWtsRVFWUjRBY1hSTVc2RFFCQ0dVVzdNaUpiVjNnUnhqTkNFVTZEa0VMR1JiTGNyUFcrSnRFYVVtYS81aTlkTjErY280YVRTNXk3S0FrWTM4Q2NCK0txa0MwRFFMTUlKR0s1QWZBYkRGWWgvQnNsVDl1dEg5cEJhTVB1MlNiV3RycmtGdTJUMXFxMlN2UVhjVGNiYVZKY0RLSXZtanMrNmV2Y2J5NisvdWxQaHRxc0FBQUFBU1VWT1JLNUNZSUk9KTsKICAgIGJhY2tncm91bmQtcmVwZWF0OiBuby1yZXBlYXQ7Cn0KCi5pY29uLXBlb3BsZS1kaXJlY3Rvcnkgc3Bhbi5pY29uIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVlBQUFBZjgvOWhBQUFDQ0VsRVFWUjQyclZTUzZ0U1VSaTFUaU14bENBYzNBUkZ4ZlNvWEErKzN3OThpMjhGSDZpcFdBWVNFZ282RVBzRkRTSWFCQkUwYU5LNFgxQ1RCa0dObXdSQjBiakpyV0QxZlFkcTRvMjZSQnNXNTNET3Q5WmVhKzJ0K0svTDZYU1ZYUzdYblZBbzlOcHVkNmZQU0hiZXF0Y2JtRXdtV0M2WGFEU2E4SGk4cjFxdGx2QlhBcExrZVQ2YlhjZGlzY0I4UHNkbXMwR2xVb1VvT3BOL0pKdE1wc3RrRytQeEdKMU9COTF1RjcxZUQ2UFJDRGFiN2Q2cHBGUXF0YXZYNjI5cXRkckRlRHgrMWVXU1RocU5CcHJOSnRydHRveHF0UXFyMWJvK0lBY0NBUk5sdzI2M3czYTdSYi9mZjJ5MWlrK3oyU3pacm9CRVpVU2pVUmdNQnU5UEhvQno4a3NrRXJreEhBNHhtODJ3V3Ewd0dBemUwdUJ0Y29KOFBvOWlzUWdTNDkwLzBiaHdJT0QxZWcybFVvbXpzZ2dMUEJKRlVXVTBHdDhsazBsa01oa0VnMEhvZExxYlBIOGd3Q3VSU0x5Z25GemFWOHJ1NW05bXMvbXUzKzhIT2VIZCtYbmh0d0s1WE80K095QjhYcS9YRjdWYTdVS3YxNSt3czNBNGpGZ3NCcnZkL21DLzMxOENjUDRYOGZqNE9PaDJ1MTl5V2RQcFZEN3JvNk1yM3h3T0J3cUZBaVJKZ3MvbjR6SlppRXY4WXJGWW5oSEtBQVRPencyemRWQm0wQjBBeFpGdHN3Z055dmJwU3N1RnNzdDBPaTMvMDJnMFR4U2NrUWw4UkdxMUdsd2F1UUxsUHdDTE1aSGpsTXRscUZTcTd3cHE5ajN0L0lGMithaFVLbkZXS0FSQnVQWXYrQUVOSVFlOFhvNjdaQUFBQUFCSlJVNUVya0pnZ2c9PSk7Cn0KCi5pY29uLXJzcy1mZWVkLXNtYWxsIHNwYW4uaWNvbiB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQWlrbEVRVlI0QVlXUm9hN0RNQXhGOHd2OVU5UFEwTkhRMHRMUlNrUGxZYUdocGFFUG5zVjZKcld5OWg3b2MyVkxEckxJUjVnekprdndZNjhFUWNtRHVXTENmd3JwUnJBY2N5R3pVa3lwTTZId0pnMU9XL1ZqeFVHa29WbTljSUoxSXgzb1hoQVNGYzAyMEx6OGtSdEN0YTVtdndwLzJpRlp0d0h0S2xRNjBXN0o3RTY0NWZGWmorLytBclExaVpMTjluNlBBQUFBQUVsRlRrU3VRbUNDKTsKfQoKLmljb24tY3JlYXRlLXVzZXItbWFjcm8gc3Bhbi5pY29uIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL3BuZztiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVlBQUFBZjgvOWhBQUFBbkVsRVFWUTR5Mk5JU1VreEJ1TC9SR0pqQm5RQWt0aXdZUU5Zd2YzNzkvRmlrQnE4QmhBQ09BMkFZWmdpZEF5eUFLY0J5QWFSNVFKMEEzRFpUaDhYa0JVTDZBWVFteWJJU2cvNVcvUC9NM1F5UUdoOEJvQm9iQmlrK2YvL0syQWFtd0U0a3pUTVpoQ2UrUzBYenNicUVxd0FxTGpwWlJ3R3h1b1NiQUJzVXhnREdFZGNzSWV6aVhjQmtrc3NEMm9TYnpNNlNKdVpCcllaVEVNQkFCYnJzaURzRG9EMEFBQUFBRWxGVGtTdVFtQ0MpOwp9CgouaWNvbi1yZW1vdmUgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBUUFBQUMxK2pmcUFBQUFPRWxFUVZRb3oyTmdJQVVVL0lmQ2Q3Z1V2RU9sVWFUZ3V2OWpOWWVnQWxJY0NJZkRVOEZkTkFWMzBSVVlGK3hHa3Q1ZFlFeGtBQUlBa0RxYTJETy9oQ0lBQUFBQVNVVk9SSzVDWUlJPSk7Cn0KCi5pY29uLXRpY2sgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBWUFBQUFmOC85aEFBQUF4VWxFUVZSNDJxMlRRUXNCY1JERmZhY2hJa1JLeWtISnlXVlB2aE1pc2lJbDVhRGs1T0xrTy8xNW95blRXN0t0cVYvVHpQKzk5OS9kMnR4ZlNzWVNQUWtwZ1VmTklNaTVIK1ErK2dsbzRmRUJ0K2c3aHc0TTZEcHp3SFg0bVgxYkRTalRjc0Jsa015dXBXSXIwM0xBcVJkazA4QVMzYzN2WlZvT09IYjlZNjdyWkxZOXRCUmc3Mm1WYUY1Vm9WTTRZTnQwSmpJdnlxb3hPQ0N1QlptWGttN0dIdWNPRGxoV1hzeUs3bHRneHA2Z0FBaU5hUUdINkxZak9HQ1NUd01GWlBxWk12RUFucFh2WnozcVVsY0FBQUFBU1VWT1JLNUNZSUk9KTsKfQoKLmljb24tY3Jvc3MgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBWUFBQUFmOC85aEFBQUJQa2xFUVZSNEFhMlRQWXJDY0JERkJSRXhLbGdJS2dHUklHSmhZU00yQ29LTlpRUkJzRXBsWldFajJMbUZ2VWZ3Q0I3QkkzaVVtV1cvUDlpM3pvTUk4bTkyMXgzNEVSamUvSmhKU09KZlNoS0p1elA0Rlp6aE1JR2gyKzJQaVBOWEFsMnZvYXNWZERpRVZLdVFUZ2M2bjBPblUwaWp3WjZHb2VXSUsxZ3NvUDArbnBkTGZCeVBlTjN0SUswV3RGREErK0hBM3NOZ0FCMlBtWFVGVVFTZHpUandKUUtyeHpEa3NKVTl1VVVVRVZjd21SQnBOamxvUlJINE5ERTNqSE91WURTNklMNlB0LzBlY2RucUVnUlhHVmZRNjRGMHU1QnNGcCtuRStMaSs2aFU0Z3h4QmUwMmtXSVJMNXNOckV6Q00rSXRTcVZMemhYVTYxRGZaekMrKzc1VzQxZTVlZzlCd0t3cktKY2htUXh2dDAvMkZFVThSVHpQVHJBZVpaclBNK3NLY2pub1dTQ3BGQ1NaaEtiVFlNL3puSjdoQ1A0RUJiZi9UTGZ4RGNYMUNDbVZZaHpXQUFBQUFFbEZUa1N1UW1DQyk7Cn0KCi5pY29uLW9wdHMgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvZ2lmO2Jhc2U2NCxSMGxHT0RkaEVBQVFBUFpNQU5qWDJPN3U3cy9iL2NMQndrMkR1Vmx3ZmxPNDkxSzQ5M0hFK0p1eHdkMlNrc3d6TS8vLy81dXd3WnV4d0p2VitvaUloOGJuL0hhT21IcWdwbStIa1hPTGxHcVh4WHVUb2ViRHcxbUx0R0IzZzJ5RGpXUjdobWgvaW5MRCtITzM2WC9KK1hpUm00U1duMy9LK2FxOTB1S3JxOFRXNkpIUitucVNublNJazhqUDFJREsrWG1peXJ6TTZKTFIrdm41K2JmSDRMcTZ1WnV3d01qVjlOTFIwdHAvZi9mMzk4UFI3ckRDMlorMHhhUzV5N2JINGIzTTZLUForbkxFK0tQWStsSzM5NjJ0ckl1aHNNelkrYkhDMmN6WithVForcCsweHNmVjlJdWdzTUxSNzZTNHkzdVRvd0FBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQ3dBQUFBQUVBQVFBQUFIdUlBbUZnU0VoWWFFRmlZc0h6MHVLeU1ySUNBakowWW5QeDhzR1FnUkR3Z2VQZ2lmRDUwUkNCa1RCMEFHQndjR3I2eXVyaE1YQXJhM3VMZ1hLRU5NdnIvQXZrVW9JVWhNQThqSnlrd3pJUkkzVEFIU0FTWFQwa3hLRWhVODBSZ0JDZ3MweVFCTUxSVVVNRXdZQytBS1FSRHdNVXc3RkJzNDBUWHNBUzhNL1RaTVJEWjBJQkdObXJWcFRFaDA0S0NqNEVHRVN6aG9PQktzSXBNY0drUWthWkRBUVFPUENXUWtHT2xBaUFnVktRcW9YTWxTWlFvVmdRQUFPdz09KTsKfQoKLmljb24tc3RvcC13YXRjaGluZyB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9naWY7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQVlFbEVRVlFvejJOZ29COG9VQ3FZV2ZDdTREOFF2Z095bE5DbE84QlN5TEFESVNsWXNCdERHZ1IzRndoQ3BNL0FoSnJ1Zm4zMzlWM1RYYmlTTTBBbENPbUMvMS9mL1FmQ3IrK1FURGxEaEFKQ1ZxQzZBZ1ZDcElud0psRUJSVXNBQVAzc3E3ZXRZb2ZQQUFBQUFFbEZUa1N1UW1DQyk7Cn0KCi5pY29uLXN0YXJ0LXdhdGNoaW5nIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybChkYXRhOmltYWdlL2dpZjtiYXNlNjQsaVZCT1J3MEtHZ29BQUFBTlNVaEVVZ0FBQUJBQUFBQVFDQVFBQUFDMStqZnFBQUFBdWtsRVFWUW96NzJRTFFwQ1FSU0Y3MXJjd216aGJjRjlYQWFVQ1dJVG1UN2xCWU5nc2swU0VUUllweGxNTjcwb0hnVEJZamdHRlo5aTFWTy9BK2RINUgvS0xsbWdwekl3V1hZZmVGWUhEcm5CR1Flc0VSazRuYmZ3Wk5WbnhoRVJuc3FJRXhib01ka0RKd3NzSUNLMGFFZWRXZ1JSRU82V0p5YVU2a1JFdEZJU3hCNERKcE5rbnJ0M1E5ZTNEU0xKQnR6aWlnZzFyYlM2UjJ6UmY3V29pMmZHQlJINnJlUno1cGhyTkdpd3hJaitmV2I3S1AvOXFGL3FCcHFKcTBwbWlqZndBQUFBQUVsRlRrU3VRbUNDKTsKfQoKLmljb24taW5mbyB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9naWY7YmFzZTY0LFIwbEdPRGxoRUFBUUFPWmNBUGhxUnE4NE9NZHljdkxmMzU4UEQrTzV1WjRNRE1kemMvbDFVdnA5Vy8vLy8veVhlZnlPYjh3NUp2MmZnL3FGWmZ1T2IvdUZaYjVjWFBodlMvcDlYT1ZoUi96VHh2UmxRL2x2Uy82bmpNNUJMZjJYZWYrMG10Nk5nLy9Sd3Y2dGsvMmdnL21NYitOVE4vdXduUCs4cGY3czV2L1F3UFhMd3RaMGFOWnhaZlY5WC9Ka1FzNDdKLzJxa3YzRHN2Ni9yUHZHdHYvbzRPaDVZLzI3cWVoOFpmL1V4Zit5bXYybmkvN093UC8zOWZ5MW8veWxpLzZ4bWYrNW92L3Q2UG1GWi8yWGV2YUhhZmwxVS9sMFUvdWZoL3A4Vy8yeW5QLzU5L2x1VFA2dWsvUnFTUHVOYnYzT3dmbCtYdldKYlBxU2QvKzlxUFJxUi82NW8vcUZaUDY1b2Y2eW1mbHVTOWgxYVB6SHVQM1p6LzdUeGRoelpmLy8vd0FBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFDSDVCQUVBQUZ3QUxBQUFBQUFRQUJBQUFBZlhnRnlDWEFVQ0FRUUVBUUlGZzRNREJ3WWRKeVVsSngwR0J3T09FaWdXSmlRY0hDUW1GaWdTbWx3SEtSNDlIMUExV2paSlZCNHBCNFFHV0ZVWkh3b3hSejRaTnp3d0JvVlhMUTRnRGtZZ0x3cFNEZzR0VzRZeUd4c0xRQXNMT0FvNzNBczBoMEVRREF3UUVETUtMdWZtVG9ncUVWTVJEMHM1VFBVUDlTcUpGUWtKaWlRZ291QkhBZ29BS1ZSUXBBSEJFQVFJbXVpQVNGR0loa1VHbEV5WWdDRkVsaEJXTUNDWkVNVllxZ1lBVW81UU1DSmx5Z2EzdUF5UTBPQkN5aWN1THpRNDVRZ1NDeEVyVm9oZ2dRbFZvMEtIRWkxcUZBZ0FPdz09KTsKfQoKLmljb24tbWFya2V0cGxhY2UgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBWUFBQUFmOC85aEFBQUNla2xFUVZRNHkyTmd3QUw4OG1mNEtudlZyQXdvbWwzcm16OGp0bWJHRGlNR1lrSHpnbjFod25ZbEh4THFsNFExejluTk5Hbk5jVzNOb09ZTGkzZGZVaWFvZWNudVM3eThsdmtmaWlkdWpvV0p0Y3pmeTJlZk1tR0JUVkxmVW9JRzVIU3ZpMkkzemZtVDE3TSswRGF4TjlNa3FtTzJRVmpyTWdYUDZxTlNyaFUzT2hjZEVNYXB1WGJXVGs5ZTQrejc0bzZsTjYzamV6S1RtNWJaNVhldmt5cnEyeUNjVUw4MFFNS3g5Q0dQUmY3YjRnbWJOTEFhNEZzd2F4cURZdXgvbThTK0pibWRheFc5Y3FabHFQalVyZU15ejN2R1pKRDVUOGl1K0tHc1crV1JrTkk1Z1ZnTjBBdHBuc2VnbC9hZjFUanJINmRaN25jR3JaVC9JQ3p1V0haTHpMSDBmdSt5d3pKK0JiUHFnREdUaDlVQVNlZnlqVXo2R2Y4WnRGUC9lK1JNYTIrY3M5dmNJS0o5WDBIdmhzQ0lxb1VkSURVcWZ2VzNuTk1uVldNMVFEKzg3UkJJTTZOdTJuLzFnSVlESUxIRmU2NmFhUWMwbnBpNSthelIwbjNYREJqVWsvNjdwRStlaU5VQWVZL3FFd3g2NmY5WmpMTCtNMmdtLzQrdFc5b1BFbCsyNXdydmpuT1BXSUd1T2N5Z252aGYxYmQrTTFZRGdQRi9qc2t3RTJ3QXpCRHJoTDZscXc1ZTU5bDg4aDYvckh2MVBWQ1lTTHRXbk1acWdLaGo2WFZHZ3d5NEFZeDZHZitCcmpyald6Q3o0ZEQxbHp3OUs0N1lNT3VtL1JPMkw3NkQxUUFSKzVJSHlBWXc2S1Q5eit0ZVo1L2V1aklzcUhUdUZIQkF1MVE4QklyL3hHb0FqMFhlQzFnWU1CdG0vV2NDR3BiV3ZNSXd0SHhlRlJ0UVROMi80UUF3SVgyUWRxMDhodFdBN0s2MVBnTFdCYzhaZEZML000Q2lFeGdiZ3JaRnowVWRTbTVyQnpVdDhzeWVtcHZSdGtxN2R2cDJacHpKdVdIMkxsV2dUYnNsSE10T1djYjF0UGpsei9STnJGc2lpaS8vQUFCOG53Z1oyNGh4UXdBQUFBQkpSVTVFcmtKZ2dnPT0pOwp9CgouaWNvbi1ibG9nLWxhcmdlLAouaWNvbi1ibG9ncG9zdC1sYXJnZSB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoJ2RhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBREFBQUFBd0NBUUFBQUQ5Q3pFTUFBQUI2a2xFUVZSWXcrMllNV3ZETUJDRi9aLzZXL28vekExQlpDaGVBaldsUXlDTGh5eUJCSUlwSFR6R2M5ZXVXVHg1S0tYd2xtQTZkSGdkakdOWmRXSkpkcmI2SmkzNnBMdjNaT21Dd1BqV2g0aGlIYXZpOVM1dytSWVFDbWVjRDRhaVlzaVFpaS8zMXRPdkQwTGhGaVYrd0dJb0tvUU1LUzZJaU1LOXhkUk5oQXo1UktIaS90RUtJSnp4d3hGd3dqT0Z3bTFpQlpqenh4SEE0aHRQdGdqaG5QYlROd0FXSjF1RUw4QjZGejZBTHpTYWlvY1JQb0FNWFlUaUZldTVBcFlNZTJKVlRBYW8wSWNRVGdiNEd5Zk1iZ3Rnb2Y0QkV3STJxTS83SlZNY2NRTkFWM3lLNmRsU053SFVrYUxDVFFFaEZkOHhFU0JEaG9SUkQyU0RTVlgwaFJ3bUp1YWxWSG5MOUdoQUxpRkcrU0RETU1LeHlCRTMwSXRhUWczVXdrdEZFZC9RSW1JTmtXTXltY1lzMFJSZGFhSTF6VGZDQjYwRFNseE9rNU1QRWlvRDBxUXExeERkUFRpcjZOajVMYXB6b3VJTGUvQ1M2YnVXOVlnTnVJVk80QU5kbmhuTU80VittSHNicmRSV1hCdnNEU2JTRVZBaFI2NGR6NjJQNjFKWDUzRkNEMEFGMVZsdjM0Uk5vWmMrZ0ZhSXJWc1RvOUFiZE1lakFXMlMrc2NqVTZSTHN3dVlxTWovRjY4Z0NJVEs2UkhvQVhCN3h2WmRFSzVlM3lNS3Q2TUFPd2dmT05CSzJGbTJFc3o0UkFxaGNIMndhSVlvaTNhSUdUTUtoUXNNTmtSYzJqbmRpSHBXL3d2cTNDM2o1S2RCMHdBQUFBQkpSVTVFcmtKZ2dnPT0nKTsKfQoKLmljb24tY29udGVudC10ZW1wbGF0ZS1sYXJnZSB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFEQUFBQUF3Q0FRQUFBRDlDekVNQUFBQk1VbEVRVlJZdyszWXYycUVRQkRIY2Q4cHo1TDNXS2FReGNMdUlPRkljWEJOQ2tzdERrS0tsRDZMVllvUURxYVJrT0tLYnlvTFBlOTJWaTlnaVB2cjk2T080NTlKa3NFcTZod3haOSs4M2lVeGE2T0NrSklGNC9FNEhKNlhlL1AyUlMwSXBiN3JTV2xDK1ZLSFEyS0lIT0ZnMkxxTHc3RkY4RlFQSmtCSStZZ0VXbjFDRU1wbkU1QnhpZ1Jvdm5WckpZUU0rL1lkUU5OYWlhbUErU3ltQUVmdDdxbkhNREVGZU5NKzRiblNlckhBRGplU2ZXTUNkdlF6M21wamhIQXo0RHl0cGxaZ2F2ei9BWVkxc0Zaa0JXNExYT3JmRlZnT01GYjJ2d1VzdndhaFJwdDlpWDRWV0Y4NE0ydHcrZm02SEdBdDhscmt4ZGJBUi8wRVRnRGlmbVBQYzd6KytaNGpsTE9BU29XY3dDaWhNbzRTaHZuVWd3cENVUnVHSWQ0d0Roa21SUkEyR2h5SXhJeHorc2xIanY0SGl2TktpalV3TzA4QUFBQUFTVVZPUks1Q1lJST0pOwp9CgouaWNvbi1ibGFuay1wYWdlLWxhcmdlIHsKICAgIGJhY2tncm91bmQtaW1hZ2U6IHVybCgnZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFEQUFBQUF3Q0FRQUFBRDlDekVNQUFBQkZVbEVRVlJZdyszWXNXckRNQlNGWWI5VG42WHZJZTVnaEFkdmdaVFFvWkNsZzBkN0NKUU1IZjBzbWpxVUVyaUxDUjB5L0owOHhESFJsWnRBQnVucytteXVETllwaXNscStob3haeHYyVDBYS1dxa2dsRlRSZUR3T2grZmoyYng5MHd0Q3ExOTZVa0lzUjNVNEpJV29FWGFHcmNjNEhCc0VUN2MyQVVMSmR5SXc2Q3VDMEw2YmdJcFRJa0Q0MVkyVkVDcnMyNDhBWWJBU1N3SHpXeXdCRGpxZXFaYzRzUVQ0MUhQQ2MrWFRTd1hlY0RQWmhwc0JSNTBqaEpzQmx4bTB2QzlBOEJuSVFBWWVDSENrNWZHQVBPUTg1RHprUE9RODVEemtPd0krNlJLNEFFaTd4bDdtY1AzM3ZVWm8vd1YwS3RSRXFvVE9XQ1ZNODZNN0ZZU21ONVFoM2xDSFRGTWlDQ3VORmlJcGRjNTU2cG1uL3dNNEtrVmlDem5KYlFBQUFBQkpSVTVFcmtKZ2dnPT0nKTsKfQoKLnN1YmhlYWRpbmcgLnJzcy1pY29uLAouc3ViaGVhZGluZyAuZW1haWwtbm90aWZpY2F0aW9uLWljb257CiAgICBiYWNrZ3JvdW5kOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQWlrbEVRVlI0QVlXUm9hN0RNQXhGOHd2OVU5UFEwTkhRMHRMUlNrUGxZYUdocGFFUG5zVjZKcld5OWg3b2MyVkxEckxJUjVnekprdndZNjhFUWNtRHVXTENmd3JwUnJBY2N5R3pVa3lwTTZId0pnMU9XL1ZqeFVHa29WbTljSUoxSXgzb1hoQVNGYzAyMEx6OGtSdEN0YTVtdndwLzJpRlp0d0h0S2xRNjBXN0o3RTY0NWZGWmorLytBclExaVpMTjluNlBBQUFBQUVsRlRrU3VRbUNDKSBuby1yZXBlYXQgbGVmdCBjZW50ZXI7CiAgICBmbG9hdDogcmlnaHQ7CiAgICBoZWlnaHQ6IDE2cHg7CiAgICB3aWR0aDogMTZweDsKICAgIHRleHQtZGVjb3JhdGlvbjogbm9uZTsKICAgIGxpbmUtaGVpZ2h0OiAwOwp9Cgouc3ViaGVhZGluZyAuZW1haWwtbm90aWZpY2F0aW9uLWljb24gewogICAgYmFja2dyb3VuZDogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBUUFBQUMxK2pmcUFBQUFnVWxFUVZRb3oyTmdvQmdVZEJUOHh3TTdHSURFekFJbHJGcGRDbllYL0FjcEVBUXlYTkFrQllFbXJ3S1NJQVZnN2htZ1lRaHBZeGdmcWdDc1pEZFFVQkRxcXJzd0UrRUt3RXJlQVdFb1dLRXgzQ3dVRTR3THlvRXVLZ2Z5ZGhlRW9paUFTSU1GbEtES2tkMkFrRVlMbjVrd0JUTWhqc01TRHF0QUNnaUZKTzBCQURUYWRuVVRucUNkQUFBQUFFbEZUa1N1UW1DQykgbm8tcmVwZWF0IGxlZnQgY2VudGVyOwogICAgbWFyZ2luLXJpZ2h0OiA1cHg7Cn0KCi51aS10cmVlIGxpIGEuYWJjLAouaWNvbi1vcmRlci1hbHBoYWJldGljYWwgewogICAgYmFja2dyb3VuZC1pbWFnZTogdXJsKGRhdGE6aW1hZ2UvcG5nO2Jhc2U2NCxpVkJPUncwS0dnb0FBQUFOU1VoRVVnQUFBQkFBQUFBUUNBUUFBQUMxK2pmcUFBQUFsMGxFUVZSNEFYMlJNUkhETUF4RlJTRVVRa0VVVE9GUjZLeXRGRVRCRkV6QkZFb2hGRW9oMVowR2UxRHFOMmpRdTlPL2I4bUhBbEloSkoySkxPdzJTWElJRTBjZkJlQ040czlDNThQa29oWTRHRWpnYUMyOEFna1VyNFNhWGFEaENXZDk0cVFGY0ZFTENUMGoyckNXSjJLT1ZaUm4wQkFPKzVxSDRERjFGZFczRGpSV2Q2NHpnNjUvMkpTMlFnNmlSd0swL3MyLy9BQmZjNHE1alhaaHlnQUFBQUJKUlU1RXJrSmdnZz09KTsKfQoKLnVpLXRyZWUgbGkgYS5yb2xsYmFjaywKLmljb24tdW5kbyB7CiAgICBiYWNrZ3JvdW5kLWltYWdlOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFCQUFBQUFRQ0FRQUFBQzEramZxQUFBQWpVbEVRVlFvRmEzQlFXMkVBQlFGd0c4QkM3V0FCU3lzQmM0ajQxdFlDMWpBQWhhd2dJWFhrcllKYVpiMjBwbXF2NWxNOWNGb0VSRzdOdFEzRDExbEZyRnBiUmVIc1Q1cHExa2NIdlZGaThOUUowOFJNZFdGRmwwbnE0aTFmckRiNitRUUVZdEhYV2lwazlpczJteXFDeTMxRzR2VVBhTlk2bzdCSnFaNnhXQzJpMmU5SmlLNjdtaXp0L28zNzdSeFhLR2hHeHNGQUFBQUFFbEZUa1N1UW1DQyk7Cn0KCiN0YWItbmF2aWdhdGlvbiB7CiAgICBtYXJnaW46IDE2cHggLTEwcHg7CiAgICBwYWRkaW5nOiAwIDEwcHg7Cn0KCi50YWItbmF2aWdhdGlvbiB7CiAgICBsaXN0LXN0eWxlOiBub25lOwogICAgZGlzcGxheTogYmxvY2s7CiAgICBtYXJnaW46IDAgMCAtMXB4OwogICAgcGFkZGluZzogMDsKICAgIG92ZXJmbG93OiBoaWRkZW47Cn0KLnRhYi1uYXZpZ2F0aW9uIC50YWIgewogICAgZmxvYXQ6IGxlZnQ7CiAgICBkaXNwbGF5OiBpbmxpbmU7CiAgICAvKiBmb250LXdlaWdodDogYm9sZDsgICAqLwogICAgbWFyZ2luOiAwIDVweCAtMXB4IDA7Cn0KLnRhYi1uYXZpZ2F0aW9uIC50YWIgYSB7CiAgICBwYWRkaW5nOiAuM2VtIC40ZW07CiAgICB0ZXh0LWRlY29yYXRpb246IG5vbmU7CiAgICBkaXNwbGF5OiBibG9jazsKICAgIC1tb3otYm9yZGVyLXJhZGl1cy10b3BsZWZ0OiAzcHg7CiAgICAtbW96LWJvcmRlci1yYWRpdXMtdG9wcmlnaHQ6IDNweDsKICAgIC13ZWJraXQtYm9yZGVyLXRvcC1sZWZ0LXJhZGl1czogM3B4OwogICAgLXdlYmtpdC1ib3JkZXItdG9wLXJpZ2h0LXJhZGl1czogM3B4OwogICAgYm9yZGVyLXRvcC1sZWZ0LXJhZGl1czogM3B4OwogICAgYm9yZGVyLXRvcC1yaWdodC1yYWRpdXM6IDNweDsKfQoudGFiLW5hdmlnYXRpb24gLm5vdGFiIHsKICAgIG1hcmdpbjogMXB4IDFweCAwIDFweDsKICAgIHBhZGRpbmc6IC4zZW07CiAgICBmbG9hdDogbGVmdDsKfQoKLyogTWFrZSB0aGUgZm9sbG93aW5nIG1vcmUgc3BlY2lmaWMsIHNvIHRoZXkgYXJlbid0IG92ZXJyaWRlbiBieSB0aGVtZXMuICovCnVsLnRhYi1uYXZpZ2F0aW9uIC5jdXJyZW50IGEgewogICAgY29sb3I6ICMwMDA7CiAgICBiYWNrZ3JvdW5kLWNvbG9yOiAjZmZmOwogICAgYm9yZGVyLWJvdHRvbS1jb2xvcjogI2ZmZjsgLyogSUUgKi8KfQp1bC50YWItbmF2aWdhdGlvbiAuY3VycmVudCBhOmhvdmVyIHsKICAgIGNvbG9yOiAjMDAwOwogICAgYmFja2dyb3VuZC1jb2xvcjogI2ZmZjsKICAgIGJvcmRlci1ib3R0b20tY29sb3I6ICNmZmY7IC8qIElFICovCn0KCi8qIFBETCBtZW51LmNzcyovCi5jb250ZW50LW5hdmlnYXRpb24gewogICAgZmxvYXQ6IHJpZ2h0OwogICAgcG9zaXRpb246IHJlbGF0aXZlOwogICAgbWFyZ2luLXRvcDogMXB4OyAvKiBUbyBhbGlnbiB0aGUgbWVudSBpdGVtcyB3aXRoIHRoZSBwYWdlIGJhbm5lciAqLwp9CgovKiBjb250ZW50LW5hdmlnYXRpb24gZWxlbWVudHMgYXJlIDxsaT4ncyAqLwouY29udGVudC1uYXZpZ2F0aW9uIC5hanMtbWVudS1pdGVtLAouY29udGVudC1uYXZpZ2F0aW9uIC5hanMtYnV0dG9uIHsKICAgIHBhZGRpbmctbGVmdDogNXB4Owp9CgovKiBDdXN0b21pc2F0aW9ucyBmb3IgbmF2aWdhdGlvbiBtZW51IGJhcgogICBXb3VsZCBsaWtlIHRvIGtpbGwgdGhlc2Ugc3R5bGVzIGlmIHdlIGV2ZXIgdXNlIGljb24gZm9udCBpbiBhbGwgdGhlIHBsYWNlcywgZm9yIG5vdyB3ZSBuZWVkIHRvIHJlc2VydmUKICAgcGFkZGluZyBpbiBjYXNlIGRldmVsb3BlcnMgYXJlIHVzaW5nIGEgYmFja2dyb3VuZC1pbWFnZSwgYnV0IHRoZXkgc2hvdWxkIGRlZmluZSB0aGVpciBvd24gcGFkZGluZyEKICovCgouY29udGVudC1uYXZpZ2F0aW9uIC5hanMtbWVudS1iYXIgLmFqcy1idXR0b24gc3BhbiwKLmNvbnRlbnQtbmF2aWdhdGlvbiAuYWpzLW1lbnUtYmFyIC5hanMtbWVudS1pdGVtIC5hanMtbWVudS10aXRsZSBzcGFuIHsKICAgIHBhZGRpbmctbGVmdDogMjFweDsKfQoKLyogVGhlc2Ugc3R5bGVzIHJlc2V0IHN0eWxlcyBpbnRlbmRlZCB0byBhbGxvdyBmb3IgcGFkZGluZyBpZiB1c2luZyBiYWNrZ3JvdW5kLWltYWdlIGljb25zIHNpbmNlIHdlIGFyZSB1c2luZyBmb250IGljb25zCiAgIElmIHdlIG1vdmUgdG8gb25seSBpY29uIGZvbnQgdGhlbiB3ZSBjYW4gcmVtb3ZlIHRoZXNlIHN0eWxlcywgYnV0IHRoZXkgb3ZlcnJpZGUgc3R5bGVzIG5lZWRlZCBmb3IgZGV2ZWxvcGVycwogICB0byB1c2UgYmFja2dyb3VuZCBpbWFnZXMKICovCi5jb250ZW50LW5hdmlnYXRpb24gLmFqcy1tZW51LWJhciAuYWpzLWJ1dHRvbiBhID4gc3BhbiA+IHNwYW4uYXVpLWljb24sCi5jb250ZW50LW5hdmlnYXRpb24gLmFqcy1tZW51LWJhciAuYWpzLW1lbnUtaXRlbSBhID4gc3BhbiA+IHNwYW4uYXVpLWljb24gewogICAgbWFyZ2luLWxlZnQ6IC0yMXB4OwogICAgcGFkZGluZy1sZWZ0OiAwcHg7CiAgICBjb2xvcjogIzcwNzA3MDsKfQovKiBlbmQgYWxsb3dzIHVzYWdlIG9mIGljb24tZm9udCAqLwoKLmFqcy1tZW51LWJhciAuYWpzLW1lbnUtaXRlbSwKLmFqcy1tZW51LWJhciAuYWpzLWJ1dHRvbiB7CiAgICBmbG9hdDogbGVmdDsKICAgIGxpc3Qtc3R5bGU6IG5vbmU7CiAgICBwb3NpdGlvbjogcmVsYXRpdmU7Cn0KCi8qIHN0eWxlcyBjb3BpZWQgZnJvbSBvbGQgbWVudS5jc3MgKi8KLmFqcy1tZW51LWJhciwKLmFqcy1tZW51LWJhciAuYWpzLWRyb3AtZG93biBhLAouYWpzLW1lbnUtYmFyIC5hanMtZHJvcC1kb3duIGEgc3BhbiB7CiAgICBkaXNwbGF5OiBibG9jazsKICAgIG1hcmdpbjogMDsKICAgIHBhZGRpbmc6IDAgMCAwIDIwcHg7Cn0KLmFqcy1tZW51LWJhciAuYWpzLW1lbnUtaXRlbSAudHJpZ2dlciBzcGFuIHsKICAgIGJhY2tncm91bmQtcG9zaXRpb246IDEwMCUgNTAlOwogICAgYmFja2dyb3VuZC1yZXBlYXQ6IG5vLXJlcGVhdDsKfQoKLmFqcy1tZW51LWJhciAuYWpzLWJ1dHRvbiBzcGFuIHsKICAgIGJhY2tncm91bmQtcmVwZWF0OiBuby1yZXBlYXQ7Cn0KCi5hanMtbWVudS1iYXIgLmFqcy1kcm9wLWRvd24gdWwsCi5hanMtbWVudS1iYXIgdWwuYWpzLWRyb3AtZG93biB7CiAgICBib3JkZXItdG9wOiBzb2xpZCAxcHggI2UxZTFlMTsKICAgIG1hcmdpbjogMDsKICAgIHBhZGRpbmc6IDVweCAwOwogICAgcG9zaXRpb246IHJlbGF0aXZlOwogICAgbGlzdC1zdHlsZTogbm9uZTsKfQouYWpzLW1lbnUtYmFyIC5hanMtbWVudS1pdGVtIGRpdi5hanMtZHJvcC1kb3duIGEgewogICAgYmFja2dyb3VuZC1wb3NpdGlvbjogLjVlbSA1MCU7CiAgICBiYWNrZ3JvdW5kLXJlcGVhdDogbm8tcmVwZWF0OwogICAgYm9yZGVyOiBub25lOwogICAgZGlzcGxheTogYmxvY2s7CiAgICBsaW5lLWhlaWdodDogMjsKICAgIG1hcmdpbjogMDsKICAgIHBhZGRpbmc6IDAgMWVtIDAgMjhweDsKICAgIHBvc2l0aW9uOiByZWxhdGl2ZTsKICAgIHRleHQtZGVjb3JhdGlvbjogbm9uZTsKICAgIHdoaXRlLXNwYWNlOiBub3dyYXA7Cn0KCi5hanMtbWVudS1iYXIgLmFqcy1kcm9wLWRvd24gewogICAgYm9yZGVyLWJvdHRvbS1yaWdodC1yYWRpdXM6IDNweDsKICAgIGJvcmRlci1ib3R0b20tbGVmdC1yYWRpdXM6IDNweDsKICAgIGJhY2tncm91bmQtY29sb3I6ICNmZmY7IC8qIHN5c3RlbSBjb2xvdXIgLSBub3QgZGVyaXZlZCBmcm9tIGNvbG91ciBzY2hlbWUgKi8KICAgIGJvcmRlcjogc29saWQgMXB4ICNjMWMxYzE7CiAgICBmb250LXdlaWdodDogbm9ybWFsOwogICAgbWluLXdpZHRoOiAxOTJweDsKICAgIHBhZGRpbmc6IDA7CiAgICBwb3NpdGlvbjogYWJzb2x1dGU7CiAgICBsZWZ0OiAwOwogICAgd2hpdGUtc3BhY2U6IG5vd3JhcDsKICAgIHotaW5kZXg6IDEwMDA7Cn0KCi8qRW5kIFBETCBtZW51LmNzcyovCgoKLyogUERMIGRlZmF1bHQtdGhlbWUuY3NzICovCiNicmVhZGNydW1icyB7CiAgICBwYWRkaW5nOiAwOwogICAgbWFyZ2luOiAwOwogICAgZm9udC1zaXplOiAwOwp9CgojYnJlYWRjcnVtYnMgbGkgewogICAgZGlzcGxheTogaW5saW5lLWJsb2NrOwogICAgZm9udC1zaXplOiAxNHB4OwogICAgY29sb3I6ICMzMzM7Cn0KCiNicmVhZGNydW1icyBsaTpiZWZvcmUgewogICAgY29udGVudDogIi8iOwogICAgZGlzcGxheTogaW5saW5lLWJsb2NrOwogICAgcGFkZGluZzogMCAycHggMCA2cHg7Cn0KCiNicmVhZGNydW1icyBsaTpmaXJzdC1jaGlsZDpiZWZvcmUgewogICAgZGlzcGxheTogbm9uZTsKfQoKI2VsbGlwc2lzIHsKICAgIGN1cnNvcjogcG9pbnRlcjsKfQoKLnBhZ2UtbWV0YWRhdGEgewogICAgbWFyZ2luOiAwIDAgMjBweDsKfQoKLnBhZ2UtbWV0YWRhdGEgdWwgewogICAgcGFkZGluZzogMDsKICAgIGxpc3Qtc3R5bGUtdHlwZTogbm9uZTsKICAgIGxpbmUtaGVpZ2h0OiAxNnB4Owp9CgoucGFnZS1tZXRhZGF0YSwKLnBhZ2UtbWV0YWRhdGEgdWwgbGkgYTpsaW5rLAoucGFnZS1tZXRhZGF0YSB1bCBsaSBhOmZvY3VzLAoucGFnZS1tZXRhZGF0YSB1bCBsaSBhOmhvdmVyLAoucGFnZS1tZXRhZGF0YSB1bCBsaSBhOmFjdGl2ZSwKLnBhZ2UtbWV0YWRhdGEgdWwgbGkgYTp2aXNpdGVkewogICAgZm9udC1zaXplOiAxMnB4OwogICAgY29sb3I6ICM3MDcwNzA7CiAgICBsaW5lLWhlaWdodDogMS41Owp9CgoucGFnZS1tZXRhZGF0YSAubW9kaWZpZWR7CiAgICBtYXJnaW4tbGVmdDo1cHg7Cn0KCi5oYXMtc2lkZWJhciAjY29udGVudC5lZGl0IGZvcm0ubWFya3VwLAouaGFzLXNpZGViYXIgLndpa2ktY29udGVudCwKLmhhcy1zaWRlYmFyLmFjdGl2ZS13aWtpbWFya3VwIC5lcnJvckJveHsKICAgIG1hcmdpbi1yaWdodDogMTdlbTsKfQoKLmhhcy1zaWRlYmFyICNjb250ZW50LnNwYWNlIHsKICAgIG1hcmdpbi1yaWdodDogMThlbTsKfQojc2lkZWJhciwKLnNpZGViYXIgewogICAgY2xlYXI6IHJpZ2h0OwogICAgZmxvYXQ6IHJpZ2h0OwogICAgd2lkdGg6IDE2ZW07CiAgICBtYXJnaW4tbGVmdDogMTBweDsKICAgIHBhZGRpbmc6IDEwcHg7Cn0KCi5jb250ZW50LXByZXZpZXcgI21haW4gewogICAgbWluLWhlaWdodDogMDsKfQoKYm9keS5wb3B1cC13aW5kb3csCmJvZHkuY29udGVudC1wcmV2aWV3LAouY29udGVudC1wcmV2aWV3LmF1aS10aGVtZS1kZWZhdWx0IHsKICAgIGJhY2tncm91bmQtY29sb3I6ICNmZmY7IC8qIG92ZXJyaWRlIGF1aS10aGVtZS1kZWZhdWx0IGJhY2tncm91bmQgKi8KfQoKLmNvbnRlbnQtcHJldmlldy5hdWktdGhlbWUtZGVmYXVsdCAjbWFpbi5hdWktcGFnZS1wYW5lbCB7CiAgICBib3JkZXI6IDA7Cn0KCiN0aXRsZS1oZWFkaW5nLndpdGgtYnJlYWRjcnVtYnMgaW1nIHsKICAgIG1hcmdpbi1yaWdodDogIDEwcHg7CiAgICBmbG9hdDogbGVmdDsKfQoKLmVkaXQtbGluayB7CiAgICBmbG9hdDogcmlnaHQ7Cn0KLyogRW5kIFBETCBkZWZhdWx0LXRoZW1lLmNzcyAqLwoKYm9keS5jb250ZW50LXR5cGUtcGFnZSAjbWFpbi1oZWFkZXIsCmJvZHkuY29udGVudC10eXBlLWJsb2dwb3N0ICNtYWluLWhlYWRlciB7CiAgICBtYXJnaW4tYm90dG9tOjA7CiAgICBwYWRkaW5nLWJvdHRvbTo1cHg7Cn0KCi5yZWNlbnRseS11cGRhdGVkIC53YWl0aW5nLWltYWdlIHsKICAgIHZlcnRpY2FsLWFsaWduOiB0ZXh0LWJvdHRvbTsKICAgIGRpc3BsYXk6IG5vbmU7Cn0KCi5sb2FkaW5nIC53YWl0aW5nLWltYWdlIHsKICAgIGRpc3BsYXk6IGlubGluZTsKfQoKLnJlY2VudGx5LXVwZGF0ZWQgdWwgewogICAgcGFkZGluZzogMDsKICAgIG1hcmdpbjogMDsKICAgIGxpc3Qtc3R5bGUtdHlwZTogbm9uZTsKfQoKLyogT3ZlcnJpZGUgaDIgdG9wIG1hcmdpbiBmb3Igbm9uLXBkbCAocGRsIGFscmVhZHkgaGFzIG5vIG1hcmdpbikqLwoucmVjZW50bHktdXBkYXRlZCA+IGgyIHsKICAgIG1hcmdpbi10b3A6IDA7Cn0KCi8qIFRoaXMgY2xhc3MgaXMgYXNzaWduZWQgdG8gdGhlIGNvbnRhaW5lciBvZiBDb25mbHVlbmNlJ3MgLmljb24KICogVGhpcyBpcyBiZXR0ZXIgdGhhbiBmbG9hdGluZyAuaWNvbiwgc2luY2UgZmxvYXRpbmcgYSBESVYgY29udGFpbmluZyB0aGUgLmljb24gYWxsb3dzIHVzIHRvIHByZXNlcnZlIGFueSBsaW5lLWhlaWdodAogKiBvbiB0aGUgaWNvbiwgbWFraW5nIGl0IGVhc2llciB0byBsaW5lIHVwIHdpdGggdGV4dCBuZWFyIGl0CiAqLwoudXBkYXRlLWl0ZW0taWNvbiB7CiAgICBmbG9hdDogbGVmdDsKfQoKLnVwZGF0ZS1pdGVtLWRldGFpbHMgewogICAgcGFkZGluZy1sZWZ0OiAyMXB4OyAvKiAxNnB4IGljb24gKyA1cHggcGFkZGluZyAqLwogICAgbWFyZ2luLXJpZ2h0OiAxMHB4Owp9CgoucmVjZW50bHktdXBkYXRlZCAucmVzdWx0cy1jb250YWluZXIgewogICAgbWFyZ2luLXRvcDogMTBweDsKfQoKLm1vcmUtbGluay1jb250YWluZXIgewogICAgbWFyZ2luLXRvcDogMTBweDsKICAgIHBhZGRpbmctdG9wOiA3cHg7CiAgICBib3JkZXItdG9wOiAxcHggc29saWQgI2NjYzsKfQoKLnJlY2VudGx5LXVwZGF0ZWQubWFjcm8tYmxhbmstZXhwZXJpZW5jZSB7CiAgICBiYWNrZ3JvdW5kOiB1cmwoZGF0YTppbWFnZS9wbmc7YmFzZTY0LGlWQk9SdzBLR2dvQUFBQU5TVWhFVWdBQUFMc0FBQUQ0Q0FNQUFBQ0RnbWVhQUFBQUdGQk1WRVgvLy85d2NIQndjSEJ3Y0hCd2NIQndjSEJ3Y0hCd2NIREpZTVRBQUFBQUNIUlNUbE1BRVNJelJGVm1kMWxzcUQ4QUFBUzJTVVJCVkhqYTdkM2hidHdnRUFSZ0JsajgvbS9jTk8zZFZJcFNud0V6TnkzN3MxTGpyNVAxMXNhV1NRc0xRUDVMQVVodldFU2ZGMkRKWmdGcWRwOWIzME4wai9rZDNheVZmTUtkK0lUNzhVRzVtWjV3SXo3bGJucktIZldnM0UyUHZMd2dsNWNhdGZUcXhablhkclFxeUo3eUFmdHhISkg3UzlubkpTS0t3RTc2Q0w2VUxMQWo2OHVZbnYza0xBRmRhYy9aMVk1c2EwZTJ0ZWQzS3IvUVdYUG81YVBXbVIvSG1wTjZiYTB1a3o4UE5xZGgybkcwYTZ0MXdIUGREN2htYjQrcnp6bTlIcTNGNkJJamNKZjk3UGRZeXlsNzFxMXZmUnhzeVd5a2U1Yi9nbjJabS81WjlzVnc4c2Z0c3BVSUROc1ZjSVkvWkpjdlhmWGJkWExxTysxU09mVTlkcnp6RXZrOE91V3I5TFBvbEsvVE85QUpldDJ1bFovcnA5RUZEMjhuZFF3VWo1NTk2SlNkMnVYeWM3MGZuZmlCamlGZGhCK21KMUY5ZStoM0RwMzVmdlBIQ3ZwbC9GeTZ2b3pwMkhSRkdkUGhTMC9HZFBqUzA5d1ZXRTNzNDNTVWlLWDJpUjFUSTZKSVloOXY5aHdmaFhYMmJybytlTHp5SENtOVdtRHdpdGpqT0tKMHhNN2dxeUIyUGpjdFBYUUduMVhkSHExRjZiL0ZLOHVDeC9sN0FsZERqRlduYS9lTTBjOUpURjRVNEp3MGlGMDNKOUZOMTg5SmRIZU1mazdlRUR2bnBHSHNuSk1Hc1d2bVpIZnMram1KbTJMbm5EU0lYVEFudTJQWHowbmNGanZucEZmc3JEdUQ3NDVkUHlkeForeWNrNFoyQnUvWE1weVRCckd2bkpQb2psMC9KOUVkdTM1T1htNlpOenBkRjdRTTU2UmZ5ekI0UHp2bnBHSExjRTc2MlRrbkRWdUdjOUxBdnVKMHhiS1c0WnpVdDdzKytIVXR3em1wdCt2bjVMcVc0WncwUEZVNUovM3NQRjNWZHYyY3hOSlRsWFBTNzFUbG5EUzBjMDc2MlRrblRlM2d5V3BuVHhuUElZRTU5aTc3K0hpRGpaMTA0bTNzcEJOdll5ZWRlQi83ck1NSVhwN0ZyT044ZlZQcGR2dTAzKy9YRDVyOGlVOUc5amlPVmpNTDIzNlQvZVJGVkFzN2E5c3Z6c2o5ZjlQTmR0UmllazJBR2hHd3ZCWXI4Yk9LNFRWd2psOTAyTjE3NVBvcHJ3K3F6ejBmZnN2NUExMldzRkhpMGVrc2p6Vzl3a1puV2F5bDVtQ2pDK3lDUmhmYktZOVJ1V0RRc05FbGRnZ2FYVzdQYkhTVlBRc2FYWHV5OHFwTGFrZjNneGsydWxuRFp6YTZUOE96dG4xQnc0dnN1MmxFOXQwMDJ6NjlhVHp0L2syRGJkOU5jMkszYXBvZHZNaHVIRHgyOENLN2NmRFl3ZS9nTDFjMnhtZmpyc0VPWGxOd3htZmpyc0hKcHJvMndYTlRYUTg4WHRsVTF5VDRFaTJxUzh2amxVMTFZVGxxaU4rZk50NmZsTDRZZkg3VDREZGVVM2dadnorOHZ6YzhJTjY0YmJJeEhoYjRQSXFITU4rK3J0SGo0YjczbENVZVhYdXQ2ZlhNTmZuaDhhL3RMWGlPMSt2UnNRK295VjZhTE1jOVRJbC9KejJNOSt6OUQvWktac0Z2ajJvVy9QWUdaOEZ5VDNiK1pSa2ZvemMreUNJOVptdzlsbnNMZy9BZSt6aWUvTmx3Mm0vRWs0OUw3bmtyYzhUZjd3ZmRVK3pFanhlQWNUYnRNL0NsUnJuOGo4QW4rRHFaMzkxSU0vQzFIVkh6b2lyeGVPZzd1TmJIcjZlc3M3ZmphTFNQUmwralJWbVhlMlB1UTNodXliYXFlTFRVamRkWHVsek85Z1JmTy9GNnU2OCtHZUpwOTlVblkzd2FLTWp0dG5va0RaNnZNQTdSSlhxK3dqZ2kxK2o1Q21PL1hLb3ZFYlgweXRYNlVrcXZYSzlYeTZtSG81ejhSWEN2OEhFaTE0Y3ZoSk52Q1NjZm5uRDZQZDMwVzdwWkFQcVhpdlYxdHVxTDYrZ2ZoZ1IvWWFUSDJZRUFBQUFBU1VWT1JLNUNZSUk9KQogICAgICAgIHJpZ2h0IG5vLXJlcGVhdDsKfQo=");

	public static readonly byte[] BulletBlueGifContents = Convert.FromBase64String(@"R0lGODlhCAAIAJEAAAAzZp272v4BAgAAACH5BAQUAP8ALAAAAAAIAAgAAAINhH+ha8vgVIvT1YdOAQA7");
	
	public static readonly byte[] WaitGifContents = Convert.FromBase64String(@"R0lGODlhEAAQAMQAAP///+7u7t3d3bu7u6qqqpmZmYiIiHd3d2ZmZlVVVURERDMzMyIiIhEREQARAAAAAP///wAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQFBwAQACwAAAAAEAAQAAAFdyAkQgGJJOWoQgIjBM8jkKsoPEzgyMGsCjPDw7ADpkQBxRDmSCRetpRA6Rj4kFBkgLC4IlUGhbNQIwXOYYWCXDufzYPDMaoKGBoKb886OjAKdgZAAgQkfCwzAgsDBAUCgl8jAQkHEAVkAoA1AgczlyIDczUDA2UhACH5BAUHABAALAAAAAAPABAAAAVjICSO0IGIATkqIiMKDaGKC8Q49jPMYsE0hQdrlABCGgvT45FKiRKQhWA0mPKGPAgBcTjsspBCAoH4gl+FmXNEUEBVAYHToJAVZK/XWoQQDAgBZioHaX8igigFKYYQVlkCjiMhACH5BAUHABAALAAAAAAQAA8AAAVgICSOUGGQqIiIChMESyo6CdQGdRqUENESI8FAdFgAFwqDISYwPB4CVSMnEhSej+FogNhtHyfRQFmIol5owmEta/fcKITB6y4choMBmk7yGgSAEAJ8JAVDgQFmKUCCZnwhACH5BAUHABAALAAAAAAQABAAAAViICSOYkGe4hFAiSImAwotB+si6Co2QxvjAYHIgBAqDoWCK2Bq6A40iA4yYMggNZKwGFgVCAQZotFwwJIF4QnxaC9IsZNgLtAJDKbraJCGzPVSIgEDXVNXA0JdgH6ChoCKKCEAIfkEBQcAEAAsAAAAABAADgAABUkgJI7QcZComIjPw6bs2kINLB5uW9Bo0gyQx8LkKgVHiccKVdyRlqjFSAApOKOtR810StVeU9RAmLqOxi0qRG3LptikAVQEh4UAACH5BAUHABAALAAAAAAQABAAAAVxICSO0DCQKBQQonGIh5AGB2sYkMHIqYAIN0EDRxoQZIaC6bAoMRSiwMAwCIwCggRkwRMJWKSAomBVCc5lUiGRUBjO6FSBwWggwijBooDCdiFfIlBRAlYBZQ0PWRANaSkED1oQYHgjDA8nM3kPfCmejiEAIfkEBQcAEAAsAAAAABAAEAAABWAgJI6QIJCoOIhFwabsSbiFAotGMEMKgZoB3cBUQIgURpFgmEI0EqjACYXwiYJBGAGBgGIDWsVicbiNEgSsGbKCIMCwA4IBCRgXt8bDACkvYQF6U1OADg8mDlaACQtwJCEAIfkEBQcAEAAsAAABABAADwAABV4gJEKCOAwiMa4Q2qIDwq4wiriBmItCCREHUsIwCgh2q8MiyEKODK7ZbHCoqqSjWGKI1d2kRp+RAWGyHg+DQUEmKliGx4HBKECIMwG61AgssAQPKA19EAxRKz4QCVIhACH5BAUHABAALAAAAAAQABAAAAVjICSOUBCQqHhCgiAOKyqcLVvEZOC2geGiK5NpQBAZCilgAYFMogo/J0lgqEpHgoO2+GIMUL6p4vFojhQNg8rxWLgYBQJCASkwEKLC17hYFJtRIwwBfRAJDk4ObwsidEkrWkkhACH5BAUHABAALAAAAQAQAA8AAAVcICSOUGAGAqmKpjis6vmuqSrUxQyPhDEEtpUOgmgYETCCcrB4OBWwQsGHEhQatVFhB/mNAojFVsQgBhgKpSHRTRxEhGwhoRg0CCXYAkKHHPZCZRAKUERZMAYGMCEAIfkEBQcAEAAsAAABABAADwAABV0gJI4kFJToGAilwKLCST6PUcrB8A70844CXenwILRkIoYyBRk4BQlHo3FIOQmvAEGBMpYSop/IgPBCFpCqIuEsIESHgkgoJxwQAjSzwb1DClwwgQhgAVVMIgVyKCEAIfkECQcAEAAsAAAAABAAEAAABWQgJI5kSQ6NYK7Dw6xr8hCw+ELC85hCIAq3Am0U6JUKjkHJNzIsFAqDqShQHRhY6bKqgvgGCZOSFDhAUiWCYQwJSxGHKqGAE/5EqIHBjOgyRQELCBB7EAQHfySDhGYQdDWGQyUhADs=");
	
	public static readonly byte[] SmilePngContents = Convert.FromBase64String(@"iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAYAAAAf8/9hAAAABGdBTUEAALGPC/xhBQAAACBjSFJNAAB6JgAAgIQAAPoAAACA6AAAdTAAAOpgAAA6mAAAF3CculE8AAAACXBIWXMAAKfwAACn8AG1cEgFAAABWWlUWHRYTUw6Y29tLmFkb2JlLnhtcAAAAAAAPHg6eG1wbWV0YSB4bWxuczp4PSJhZG9iZTpuczptZXRhLyIgeDp4bXB0az0iWE1QIENvcmUgNS40LjAiPgogICA8cmRmOlJERiB4bWxuczpyZGY9Imh0dHA6Ly93d3cudzMub3JnLzE5OTkvMDIvMjItcmRmLXN5bnRheC1ucyMiPgogICAgICA8cmRmOkRlc2NyaXB0aW9uIHJkZjphYm91dD0iIgogICAgICAgICAgICB4bWxuczp0aWZmPSJodHRwOi8vbnMuYWRvYmUuY29tL3RpZmYvMS4wLyI+CiAgICAgICAgIDx0aWZmOk9yaWVudGF0aW9uPjE8L3RpZmY6T3JpZW50YXRpb24+CiAgICAgIDwvcmRmOkRlc2NyaXB0aW9uPgogICA8L3JkZjpSREY+CjwveDp4bXBtZXRhPgpMwidZAAACNElEQVQ4EY1TTY9MURA9dV/3vCZh0JJJzCxELNg1C2Jhg4iQFktrEsO/mH/BLFhb+oogTCIWE9+9QyKGZJpMaGYiae/N63fLqXpamghucm9uquqc+hbwqM4EkZno/86JPYh6itKDVGyFQhBkge8cJFyU1pVHoxgZgtUMn7fPoxam0UiAbyVQOCdQD8AayjLKBpjFrqvnREhvjis2gjvH72B87JD2ckOVNCACRPqJdEA0EmmmASurd9G6dthIKgPz7OAsl5qIpEmdxkOwMQSTmU57WW62Hi0VopZz0Ieaxyh1kd6nTF68+YJ9rQkkVYAoo2K+s4Sd2zaiubmhWqhKGgKi7A3UnvacGTb55PaDd9h/5jFeLSyD9fBrf5OZzmz4lI5hsQOzP+AFq3Km7j+O2VqR2SmJT9oDUiYsEpgjer0Mf02h2YAO2DKLQzAQfdou2Oua+XWSxOh4hy00hR1rZalQXgebjARMQd66EiyJsbJgJLS6m0l17G8y6n6Ao2OIDVTM2ZB4n1m0Dx/7uD+/iH6/oL06n/1NZjorrNvaYEHu/dpGxhNLlcs3X+PGrZeYmhr3CLqLKzh2ZAdOHt2OkHB4I3620ePUZ+0L2JBO6+c8l4AxhCDd91/RXaJHnsmJtZjcso4pMKaIVdmUpljOZ2X39bMVge3ByCgzz5I5Jky4mkaDFYxN/zDK1TKxEpxtZ7UJW1+vUxI0H8Cu/U3m00fPwz0YWSZu1b/WmQVjZy79vs7fAdlwN9BZNtNNAAAAAElFTkSuQmCC");
	
	public static readonly byte[] HomePage16PngContents = Convert.FromBase64String(@"iVBORw0KGgoAAAANSUhEUgAAABAAAAAQCAMAAAAoLQ9TAAAASFBMVEX///+1tbWwsLCtra3///+Li4urq6uhoaH5+fnZ2dnGxsb8/PzT09PR0dHPz89wcHBycnJ/f396enp4eHh1dXWAgICDg4N8fHyiV7lhAAAAD3RSTlMAIiJV3e7u7u7u7u7u7u4Pv12NAAAAaElEQVR42nXKSRKAIAxE0agJ4Nhxvv9NlUHQhb+yyasmoloAvVsbiskyOGZ2WJJoyyGYtFFOwDZsCtytEUqaoUP3AbNhMy+wM4DZFjgBLwUY8PcLjp/6CGOGNoAcUx//YRcPlWgKUtEFoOAHGf+lx/AAAAAASUVORK5CYII=");
	
	public static string RenderIndexPage(Space currentSpace, IEnumerable<Page> pages, bool isChunk)
	{
		// Note: Notion에서 HTML 요소를 상당히 깐깐하게 검사한다. footer 태그 같은 부분을 임의로 바꾸면 안되는 듯!
		var buffer = new StringBuilder();
		buffer.Append("<!DOCTYPE html>");
		buffer.Append("<html>");
		buffer.Append("<head>");
		buffer.Append($"  <title>{currentSpace.key} ({currentSpace.name})</title>");
		buffer.Append("  <link rel=\"stylesheet\" href=\"styles/site.css\" type=\"text/css\" />");
		buffer.Append("  <meta http-equiv=\"Content-Type\" content=\"text/html; charset=UTF-8\">");
		buffer.Append("</head>");
		buffer.Append("<body class=\"theme-default aui-theme-default\">");
		buffer.Append("<div id=\"page\">");
		buffer.Append("  <div id=\"main\" class=\"aui-page-panel\">");
		buffer.Append("    <div id=\"main-header\">");
		buffer.Append("      <h1 id=\"title-heading\" class=\"pagetitle\">");
		buffer.Append("        <span id=\"title-text\">Space Details:</span>");
		buffer.Append("      </h1>");
		buffer.Append("    </div>");
		buffer.Append("    <div id=\"content\">");
		buffer.Append(RenderSpaceInfo(currentSpace));
		buffer.Append("      <br />");
		buffer.Append("      <br />");
		buffer.Append(RenderPageHierachy(null, pages, isChunk));
		buffer.Append("    </div>");
		buffer.Append("  </div>");
		buffer.Append("</div>");
		buffer.Append("<div id=\"footer\" role=\"contentinfo\">");
		buffer.Append("  <section class=\"footer-body\">");
		buffer.Append($"    <p>Document generated by Confluence on {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
		buffer.Append("    <div id=\"footer-logo\"><a href=\"http://www.atlassian.com/\">Atlassian</a></div>");
		buffer.Append("  </section>");
		buffer.Append("</div>");
		buffer.Append("</body>");
		buffer.Append("</html>");
		return buffer.ToString();
	}
	
	public static string RenderSpaceInfo(Space currentSpace)
	{
		var buffer = new StringBuilder();
		buffer.Append("<div id=\"main-content\" class=\"pageSection\">");
		buffer.Append("  <table class=\"confluenceTable\">");
		buffer.Append("    <tr>");
		buffer.Append("      <th class=\"confluenceTh\">Key</th>");
		buffer.Append($"      <td class=\"confluenceTd\">{currentSpace.key}</td>");
		buffer.Append("    </tr>");
		buffer.Append("    <tr>");
		buffer.Append("      <th class=\"confluenceTh\">Name</th>");
		buffer.Append($"      <td class=\"confluenceTd\">{currentSpace.name}</td>");
		buffer.Append("    </tr>");
		buffer.Append("    <tr>");
		buffer.Append("      <th class=\"confluenceTh\">Description</th>");
		buffer.Append($"      <td class=\"confluenceTd\">{currentSpace.description?.title}</td>");
		buffer.Append("    </tr>");
		buffer.Append("    <tr>");
		buffer.Append("      <th class=\"confluenceTh\">Created by</th>");
		buffer.Append($"      <td class=\"confluenceTd\">{currentSpace.description?.creator?.name} ({currentSpace.description?.creationDate})</td>");
		buffer.Append("    </tr>");
		buffer.Append("  </table>");
		buffer.Append("</div>");
		return buffer.ToString();
	}
	
	public static string RenderPageHierachy(Page currentPage, IEnumerable<Page> pages, bool isChunk, int depth = 0)
	{
		var buffer = new StringBuilder();

		if (currentPage == null)
		{
			buffer.Append("<div class=\"pageSection\">");
			
			buffer.Append("<div class=\"pageSectionHeader\">");
			buffer.Append("<h2 class=\"pageSectionTitle\">Available Pages:</h2>");
			buffer.Append("</div>");
			
			var rootTargets = pages.Where(x => x.parent == null).OrderBy(x => x.position);

			if (rootTargets.Any())
			{
				foreach (var eachPage in rootTargets)
				{
					buffer.Append(RenderPageHierachy(eachPage, pages, isChunk, depth));
				}
			}
			
			buffer.Append("</div>");
		}
		else
		{
			buffer.Append("<ul><li>");
			buffer.Append($"<a href=\"{currentPage.hibernateId}.html\">{currentPage.title}</a>");
			
			if (!isChunk && depth == 0)
				buffer.Append("<img src=\"images/icons/contenttypes/home_page_16.png\" height=\"16\" width=\"16\" border=\"0\" align=\"absmiddle\" />");
			
			var targets = pages.Where(x => object.ReferenceEquals(currentPage, x.parent)).OrderBy(x => x.position);

			if (targets.Any())
			{
				foreach (var eachPage in targets)
				{
					buffer.Append(RenderPageHierachy(eachPage, pages, isChunk, depth + 1));
				}
			}
			buffer.Append("</li></ul>");
		}

		return buffer.ToString();
	}
	
	public static string RenderPage(Space space, Page page, BodyContent bodyContent, IEnumerable<Attachment> attachments, IEnumerable<Page> pages)
	{
		var buffer = new StringBuilder();

		buffer.Append("<!DOCTYPE html>");
		buffer.Append("<html>");
		buffer.Append("<head>");
		buffer.Append($"  <title>{space.key} ({page.title})</title>");
		buffer.Append("  <link rel=\"stylesheet\" href=\"styles/site.css\" type=\"text/css\" />");
		buffer.Append("  <meta http-equiv=\"Content-Type\" content=\"text/html; charset=UTF-8\">");
		buffer.Append("</head>");
		
		buffer.Append("<body class=\"theme-default aui-theme-default\">");
		
		buffer.Append("<div id=\"page\">");
		
		buffer.Append("<div id=\"main\" class=\"aui-page-panel\">");
		
		buffer.Append("<div id=\"main-header\">");
		
		buffer.Append("<div id=\"breadcrumb-section\">");
		buffer.Append("<ol id=\"breadcrumbs\">");
		buffer.Append($"<li class=\"first\"><span><a href=\"index.html\">{space.name}</a></span></li>");
		
		var parent = page;
		var pageList = new List<Page>();
		do
		{
			pageList.Insert(0, parent);
			parent = parent?.parent;
		}
		while (parent != null);
		
		if (pageList.Count > 1)
			pageList = pageList.Skip(1).ToList();

		foreach (var eachPath in pageList)
			buffer.Append($"<li><span><a href=\"{eachPath.hibernateId}.html\">{eachPath.title}</a></span></li>");

		buffer.Append("</ol>");
		buffer.Append("</div>");

		buffer.Append("<h1 id=\"title-heading\" class=\"pagetitle\">");
		buffer.Append($"<span id=\"title-text\">{space.name}: {page.title}</span>");
		buffer.Append("</h1>");
		
		buffer.Append("</div>");
		
		buffer.Append("<div id=\"content\" class=\"view\">");
		
		var author = default(string);
		var editor = default(string);
		if (page != null)
		{
			author = page?.creator?.name;
			editor = page?.lastModifier?.name;
		}
		buffer.Append($"<div class=\"page-metadata\">Created by <span class=\"author\">{author ?? "Unknown"}</span>, last modified by <span class=\"editor\">{editor ?? "Unknown"}</span></div>");
		
		buffer.Append("<div id=\"main-content\" class=\"wiki-content group\">");
		buffer.Append(HtmlProcessor.NormalizeBodyContent(bodyContent.body, attachments, pages));
		buffer.Append("</div>");

		if (attachments != null && attachments.Count() > 0)
		{
			buffer.Append("<div class=\"pageSection group\">");
			
			buffer.Append("<div class=\"pageSectionHeader\">");
			buffer.Append("<h2 id=\"attachments\" class=\"pageSectionTitle\">Attachments:</h2>");
			buffer.Append("</div>");

			buffer.Append("<div class=\"greybox\" align=\"left\">");
			foreach (var eachAttachment in attachments)
			{
				var ext = Path.GetExtension(eachAttachment.title);
				buffer.Append("<img src=\"images/icons/bullet_blue.gif\" height=\"8\" width=\"8\" alt=\"\" />");
				buffer.Append($"<a href=\"attachments/{page.hibernateId}/{eachAttachment.hibernateId}{ext}\">{eachAttachment.title}</a>");

				var contentType = eachAttachment.contentProperties.Where(x => string.Equals("MEDIA_TYPE", x.name, StringComparison.Ordinal)).Select(x => x.stringValue).FirstOrDefault();
				if (string.IsNullOrWhiteSpace(contentType))
					contentType = "application/octet-stream";

				buffer.Append($"&nbsp;{contentType}&nbsp;"); // To Do
				buffer.Append("<br />");
			}
			buffer.Append("</div>");
			
			buffer.Append("</div>");
		}
		
		buffer.Append("</div>");
		
		buffer.Append("</div>");

		buffer.Append("<div id=\"footer\" role=\"contentinfo\">");
		buffer.Append("  <section class=\"footer-body\">");
		buffer.Append($"    <p>Document generated by Confluence on {DateTime.Now:yyyy-MM-dd HH:mm}</p>");
		buffer.Append("    <div id=\"footer-logo\"><a href=\"https://www.atlassian.com/\">Atlassian</a></div>");
		buffer.Append("  </section>");
		buffer.Append("</div>");

		buffer.Append("</div>");
		
		buffer.Append("</body>");
		
		buffer.Append("</html>");		
		return buffer.ToString();
	}
}

/* Confluence */

#region Confluence

[ConfluenceType("bucket.user.propertyset", "BucketPropertySetItem")]
public sealed class BucketPropertySetItem : IConfluenceObject
{
	public string hibernateId { get; private set; } = null;
	public string type { get; private set; } = null;
	public string booleanVal { get; private set; } = null;
	public string doubleVal { get; private set; } = null;
	public string stringVal { get; private set; } = null;
	public string textVal { get; private set; } = null;
	public string longVal { get; private set; } = null;
	public string intVal { get; private set; } = null;
	public string dateVal { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier?.Content;
		type = x.GetPropertyValue(nameof(type));
		booleanVal = x.GetPropertyValue(nameof(booleanVal));
		doubleVal = x.GetPropertyValue(nameof(doubleVal));
		stringVal = x.GetPropertyValue(nameof(stringVal));
		textVal = x.GetPropertyValue(nameof(textVal));
		longVal = x.GetPropertyValue(nameof(longVal));
		intVal = x.GetPropertyValue(nameof(intVal));
		dateVal = x.GetPropertyValue(nameof(dateVal));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o) { }
}

[ConfluenceType("com.atlassian.confluence.labels", "Labelling")]
public sealed class Labelling : IConfluenceObject
{
	public string hibernateId { get; private set; } = null;
	public string labelId { get; private set; } = null;
	public string contentId { get; private set; } = null;
	public string owningUserId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;
	public string labelableId { get; private set; } = null;
	public string labelableType { get; private set; } = null;

	public Label label { get; private set; } = null;
	public IConfluenceObject content { get; private set; } = null;
	public ConfluenceUserImpl owningUser { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		labelId = x.GetRelationValue(nameof(label));
		contentId = x.GetRelationValue(nameof(content));
		owningUserId = x.GetRelationValue(nameof(owningUser));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
		labelableId = x.GetRelationValue(nameof(labelableId));
		labelableType = x.GetPropertyValue(nameof(labelableType));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		label = o.TryGetItem(labelId) as Label;
		content = o.TryGetItem(contentId);
		owningUser = o.TryGetItem(owningUserId) as ConfluenceUserImpl;
	}
}

[ConfluenceType("com.atlassian.confluence.labels", "Label")]
public sealed class Label : IConfluenceObject
{
	public string hibernateId { get; private set; } = null;
	public string name { get; private set; } = null;
	public string owningUserId { get; private set; } = null;
	public string @namespace { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;
	
	public IConfluenceObject owningUser { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		name = x.GetPropertyValue(nameof(name));
		owningUserId = x.GetRelationValue(nameof(owningUser));
		@namespace = x.GetPropertyValue(nameof(@namespace));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		owningUser = o.TryGetItem(owningUserId) as ConfluenceUserImpl;
	}
}

[ConfluenceType("com.atlassian.confluence.links", "ReferralLink")]
public sealed class ReferralLink : IConfluenceObject
{
	public string hibernateId { get; private set; } = null;
	public string viewCount { get; private set; } = null;
	public string url { get; private set; } = null;
	public string lowerUrl { get; private set; } = null;
	public string sourceContentId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;
	
	public IConfluenceObject sourceContent { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		viewCount = x.GetPropertyValue(nameof(viewCount));
		url = x.GetPropertyValue(nameof(url));
		lowerUrl = x.GetPropertyValue(nameof(lowerUrl));
		sourceContentId = x.GetRelationValue(nameof(sourceContent));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		sourceContent = o.TryGetItem(sourceContentId);
	}
}

[ConfluenceType("com.atlassian.confluence.links", "OutgoingLink")]
public sealed class OutgoingLink : IConfluenceObject
{
	public string hibernateId { get; private set; } = null;
	public string destinationPageTitle { get; private set; } = null;
	public string lowerDestinationPageTitle { get; private set; } = null;
	public string destinationSpaceKey { get; private set; } = null;
	public string lowerDestinationSpaceKey { get; private set; } = null;
	public string sourceContentId { get; private set; } = null;
	public string creatorId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModifierId { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;

	public IConfluenceObject sourceContent { get; private set; } = null;
	public ConfluenceUserImpl creator { get; private set; } = null;
	public ConfluenceUserImpl lastModifier { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		destinationPageTitle = x.GetPropertyValue(nameof(destinationPageTitle));
		lowerDestinationPageTitle = x.GetPropertyValue(nameof(lowerDestinationPageTitle));
		destinationSpaceKey = x.GetPropertyValue(nameof(destinationSpaceKey));
		lowerDestinationSpaceKey = x.GetPropertyValue(nameof(lowerDestinationSpaceKey));
		sourceContentId = x.GetRelationValue(nameof(sourceContent));
		creatorId = x.GetRelationValue(nameof(creator));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModifierId = x.GetRelationValue(nameof(lastModifier));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		sourceContent = o.TryGetItem(sourceContentId);
		creator = o.TryGetItem(creatorId) as ConfluenceUserImpl;
		lastModifier = o.TryGetItem(lastModifierId) as ConfluenceUserImpl;
	}
}

[ConfluenceType("com.atlassian.confluence.pages", "Attachment")]
public sealed class Attachment : IConfluenceObject, IHasVersion
{
	public string hibernateId { get; private set; } = null;
	public int? hibernateVersion { get; private set; } = null;
	public string title { get; private set; } = null;
	public string lowerTitle { get; private set; } = null;
	public int? version { get; private set; } = null;
	public string creatorId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModifierId { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;
	public string versionComment { get; private set; } = null;
	public string originalVersionId { get; private set; } = null;
	public string contentStatus { get; private set; } = null;
	public string containerContentId { get; private set; } = null;
	public string spaceId { get; private set; } = null;
	public string[] contentPropertiesIds { get; private set; } = null;
	
	public ConfluenceUserImpl creator { get; private set; } = null;
	public ConfluenceUserImpl lastModifier { get; private set; } = null;
	public IConfluenceObject containerContent { get; private set; } = null;
	public Space space { get; private set; } = null;
	public ConfluenceObjectCollection<ContentProperty> contentProperties { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		hibernateVersion = x.GetPropertyIntegerValue(nameof(hibernateVersion));
		title = x.GetPropertyValue(nameof(title));
		lowerTitle = x.GetPropertyValue(nameof(lowerTitle));
		version = x.GetPropertyIntegerValue(nameof(version));
		creatorId = x.GetRelationValue(nameof(creator));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModifierId = x.GetRelationValue(nameof(lastModifier));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
		versionComment = x.GetPropertyValue(nameof(versionComment));
		originalVersionId = x.GetRelationValue(nameof(originalVersionId));
		contentStatus = x.GetPropertyValue(nameof(contentStatus));
		containerContentId = x.GetRelationValue(nameof(containerContent));
		spaceId = x.GetRelationValue(nameof(space));
		contentPropertiesIds = x.GetIdentifierCollections(nameof(contentProperties)).ToArray();
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		creator = o.TryGetItem(creatorId) as ConfluenceUserImpl;
		lastModifier = o.TryGetItem(lastModifierId) as ConfluenceUserImpl;
		containerContent = o.TryGetItem(containerContentId);
		space = o.TryGetItem(spaceId) as Space;
		contentProperties = o.GetMultipleItems(contentPropertiesIds).Cast<ContentProperty>().ToCollection();
	}
	
	public string safeFileName => Path.GetFileName(new Uri(new Uri("file:///", UriKind.Absolute), new Uri(title, UriKind.Relative)).LocalPath);
	public string safeLowerFileName => Path.GetFileName(new Uri(new Uri("file:///", UriKind.Absolute), new Uri(lowerTitle, UriKind.Relative)).LocalPath);
}

[ConfluenceType("com.atlassian.confluence.core", "BodyContent")]
public sealed class BodyContent : IConfluenceObject
{
	public string hibernateId { get; private set; } = null;
	public string body { get; private set; } = null;
	public string contentId { get; private set; } = null;
	public string bodyType { get; private set; } = null;
	
	public IConfluenceObject content { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		body = x.GetPropertyValue(nameof(body));
		contentId = x.GetRelationValue(nameof(content));
		bodyType = x.GetPropertyValue(nameof(bodyType));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		content = o.TryGetItem(contentId);
	}
}

[ConfluenceType("com.atlassian.confluence.pages", "Comment")]
public sealed class Comment : IConfluenceObject, IHasVersion
{
	public string hibernateId { get; private set; } = null;
	public int? hibernateVersion { get; private set; } = null;
	public string title { get; private set; } = null;
	public string lowerTitle { get; private set; } = null;
	public int? version { get; private set; } = null;
	public string creatorId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModifierId { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;
	public string versionComment { get; private set; } = null;
	public string originalVersionId { get; private set; } = null;
	public string contentStatus { get; private set; } = null;
	public string[] bodyContentsIds { get; private set; } = null;
	public string[] contentPropertiesIds { get; private set; } = null;

	public ConfluenceUserImpl creator { get; private set; } = null;
	public ConfluenceUserImpl lastModifier { get; private set; } = null;
	public ConfluenceObjectCollection<BodyContent> bodyContents { get; private set; } = null;
	public ConfluenceObjectCollection<ContentProperty> contentProperties { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		hibernateVersion = x.GetPropertyIntegerValue(nameof(hibernateVersion));
		title = x.GetPropertyValue(nameof(title));
		lowerTitle = x.GetPropertyValue(nameof(lowerTitle));
		version = x.GetPropertyIntegerValue(nameof(version));
		creatorId = x.GetRelationValue(nameof(creator));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModifierId = x.GetRelationValue(nameof(lastModifier));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
		versionComment = x.GetPropertyValue(nameof(versionComment));
		originalVersionId = x.GetRelationValue(nameof(originalVersionId));
		contentStatus = x.GetPropertyValue(nameof(contentStatus));
		bodyContentsIds = x.GetIdentifierCollections(nameof(bodyContents)).ToArray();
		contentPropertiesIds = x.GetIdentifierCollections(nameof(contentProperties)).ToArray();
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		creator = o.TryGetItem(creatorId) as ConfluenceUserImpl;
		lastModifier = o.TryGetItem(lastModifierId) as ConfluenceUserImpl;
		bodyContents = o.GetMultipleItems(bodyContentsIds).Cast<BodyContent>().ToCollection();
		contentProperties = o.GetMultipleItems(contentPropertiesIds).Cast<ContentProperty>().ToCollection();
	}
}

[ConfluenceType("com.atlassian.confluence.security", "SpacePermission")]
public sealed class SpacePermission : IConfluenceObject
{
	public string hibernateId { get; private set; } = null;
	public string spaceId { get; private set; } = null;
	public string type { get; private set; } = null;
	public string group { get; private set; } = null;
	public string allUsersSubject { get; private set; } = null;
	public string creatorId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModifierId { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;

	public Space space { get; private set; } = null;
	public ConfluenceUserImpl creator { get; private set; } = null;
	public ConfluenceUserImpl lastModifier { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		spaceId = x.GetRelationValue(nameof(space));
		type = x.GetPropertyValue(nameof(type));
		group = x.GetPropertyValue(nameof(group));
		allUsersSubject = x.GetPropertyValue(nameof(allUsersSubject));
		creatorId = x.GetRelationValue(nameof(creationDate));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModifierId = x.GetRelationValue(nameof(lastModifier));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		space = o.TryGetItem(spaceId) as Space;
		creator = o.TryGetItem(creatorId) as ConfluenceUserImpl;
		lastModifier = o.TryGetItem(lastModifierId) as ConfluenceUserImpl;
	}
}

[ConfluenceType("com.atlassian.confluence.content", "ContentProperty")]
public sealed class ContentProperty : IConfluenceObject
{
	public string hibernateId { get; private set; } = null;
	public string name { get; private set; } = null;
	public string stringValue { get; private set; } = null;
	public string longValue { get; private set; } = null;
	public string dateValue { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		name = x.GetPropertyValue(nameof(name));
		stringValue = x.GetPropertyValue(nameof(stringValue));
		longValue = x.GetPropertyValue(nameof(longValue));
		dateValue = x.GetPropertyValue(nameof(dateValue));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o) { }
}

[ConfluenceType("com.atlassian.confluence.internal.relations.dao", "User2ContentRelationEntity")]
public sealed class UserToContentRelationEntity : IConfluenceObject
{
	public string hibernateId { get; private set; } = null;
	public string targetContentId { get; private set; } = null;
	public string sourceContentId { get; private set; } = null;
	public string targetType { get; private set; } = null;
	public string relationName { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;
	public string creatorId { get; private set; } = null;
	public string lastModifierId { get; private set; } = null;

	public IConfluenceObject targetContent { get; private set; } = null;
	public IConfluenceObject sourceContent { get; private set; } = null;
	public ConfluenceUserImpl creator { get; private set; } = null;
	public ConfluenceUserImpl lastModifier { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		targetContentId = x.GetRelationValue(nameof(targetContent));
		sourceContentId = x.GetRelationValue(nameof(sourceContent));
		targetType = x.GetPropertyValue(nameof(targetType));
		relationName = x.GetPropertyValue(nameof(relationName));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
		creatorId = x.GetRelationValue(nameof(creationDate));
		lastModifierId = x.GetRelationValue(nameof(lastModifier));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		targetContent = o.TryGetItem(targetContentId);
		sourceContent = o.TryGetItem(sourceContentId);
		creator = o.TryGetItem(creatorId) as ConfluenceUserImpl;
		lastModifier = o.TryGetItem(lastModifierId) as ConfluenceUserImpl;
	}
}

[ConfluenceType("com.atlassian.confluence.like", "LikeEntity")]
public sealed class LikeEntity : IConfluenceObject
{
	public string hibernateId { get; private set; } = null;
	public string userId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string sequenceNumber { get; private set; } = null;
	public string liked { get; private set; } = null;
	
	public ConfluenceUserImpl user { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		userId = x.GetRelationValue(nameof(user));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		sequenceNumber = x.GetPropertyValue(nameof(sequenceNumber));
		liked = x.GetPropertyValue(nameof(liked));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		user = o.TryGetItem(userId) as ConfluenceUserImpl;
	}
}

[ConfluenceType("com.atlassian.confluence.content", "CustomContentEntityObject")]
public sealed class CustomContentEntityObject : IConfluenceObject, IHasVersion
{
	public string hibernateId { get; private set; } = null;
	public int? hibernateVersion { get; private set; } = null;
	public string title { get; private set; } = null;
	public string lowerTitle { get; private set; } = null;
	public int? version { get; private set; } = null;
	public string creatorId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModifierId { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;
	public string versionComment { get; private set; } = null;
	public string originalVersionId { get; private set; } = null;
	public string contentStatus { get; private set; } = null;
	public string spaceId { get; private set; } = null;
	public string pluginModuleKey { get; private set; } = null;
	public string pluginVersion { get; private set; } = null;
	public string[] bodyContentsIds { get; private set; } = null;
	public string[] contentsPropertiesIds { get; private set; } = null;

	public ConfluenceUserImpl creator { get; private set; } = null;
	public ConfluenceUserImpl lastModifier { get; private set; } = null;
	public Space space { get; private set; } = null;
	public ConfluenceObjectCollection<BodyContent> bodyContents { get; private set; } = null;
	public ConfluenceObjectCollection<ContentProperty> contentsProperties { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		hibernateVersion = x.GetPropertyIntegerValue(nameof(hibernateVersion));
		title = x.GetPropertyValue(nameof(title));
		lowerTitle = x.GetPropertyValue(nameof(lowerTitle));
		version = x.GetPropertyIntegerValue(nameof(version));
		creatorId = x.GetRelationValue(nameof(creator));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModifierId = x.GetRelationValue(nameof(lastModifier));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
		versionComment = x.GetPropertyValue(nameof(versionComment));
		originalVersionId = x.GetRelationValue(nameof(originalVersionId));
		contentStatus = x.GetPropertyValue(nameof(contentStatus));
		spaceId = x.GetRelationValue(nameof(space));
		pluginModuleKey = x.GetPropertyValue(nameof(pluginModuleKey));
		pluginVersion = x.GetPropertyValue(nameof(pluginVersion));
		bodyContentsIds = x.GetIdentifierCollections(nameof(bodyContents)).ToArray();
		contentsPropertiesIds = x.GetIdentifierCollections(nameof(contentsProperties)).ToArray();
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		space = o.TryGetItem(spaceId) as Space;
		creator = o.TryGetItem(creatorId) as ConfluenceUserImpl;
		lastModifier = o.TryGetItem(lastModifierId) as ConfluenceUserImpl;
		bodyContents = o.GetMultipleItems(bodyContentsIds).Cast<BodyContent>().ToCollection();
		contentsProperties = o.GetMultipleItems(contentsPropertiesIds).Cast<ContentProperty>().ToCollection();
	}
}

[ConfluenceType("com.atlassian.confluence.spaces", "Space")]
public sealed class Space : IConfluenceObject
{	
	public string hibernateId { get; private set; } = null;
	public string name { get; private set; } = null;
	public string key { get; private set; } = null;
	public string lowerKey { get; private set; } = null;
	public string descriptionId { get; private set; } = null;
	public string homePageId { get; private set; } = null;
	public string creatorId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModifierId { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;
	public string spaceType { get; private set; } = null;
	public string spaceStatus { get; private set; } = null;
	public string[] permissionsIds { get; private set; } = null;

	public SpaceDescription description { get; private set; } = null;
	public Page homePage { get; private set; } = null;
	public ConfluenceUserImpl creator { get; private set; } = null;
	public ConfluenceUserImpl lastModifier { get; private set; } = null;
	public ConfluenceObjectCollection<SpacePermission> permissions { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		name = x.GetPropertyValue(nameof(name));
		key = x.GetPropertyValue(nameof(key));
		lowerKey = x.GetPropertyValue(nameof(lowerKey));
		descriptionId = x.GetRelationValue(nameof(description));
		homePageId = x.GetRelationValue(nameof(homePage));
		creatorId = x.GetRelationValue(nameof(creator));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModifierId = x.GetRelationValue(nameof(lastModifier));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
		spaceType = x.GetPropertyValue(nameof(spaceType));
		spaceStatus = x.GetPropertyValue(nameof(spaceStatus));
		permissionsIds = x.GetIdentifierCollections(nameof(permissions)).ToArray();
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		description = o.TryGetItem(descriptionId) as SpaceDescription;
		homePage = o.TryGetItem(homePageId) as Page;
		creator = o.TryGetItem(creatorId) as ConfluenceUserImpl;
		lastModifier = o.TryGetItem(lastModifierId) as ConfluenceUserImpl;
		permissions = o.GetMultipleItems(permissionsIds).Cast<SpacePermission>().ToCollection();
	}
}

[ConfluenceType("com.atlassian.confluence.pages", "Page")]
public sealed class Page : IConfluenceObject, IHasVersion
{
	public string hibernateId { get; private set; } = null;
	public int? hibernateVersion { get; private set; } = null;
	public string title { get; private set; } = null;
	public string lowerTitle { get; private set; } = null;
	public int? version { get; private set; } = null;
	public string creatorId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModifierId { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;
	public string versionComment { get; private set; } = null;
	public string originalVersionId { get; private set; } = null;
	public string contentStatus { get; private set; } = null;
	public string spaceId { get; private set; } = null;
	public int? position { get; private set; } = null;
	public string parentId { get; private set; } = null;
	public string[] bodyContentsIds { get; private set; } = null;
	public string[] outgoingLinksIds { get; private set; } = null;
	public string[] referralLinksIds { get; private set; } = null;
	public string[] contentPropertiesIds { get; private set; } = null;
	public string[] historicalVersionsIds { get; private set; } = null;
	public string[] attachmentsIds { get; private set; } = null;
	public string[] commentsIds { get; private set; } = null;
	public string[] customContentIds { get; private set; } = null;
	public string[] childrenIds { get; private set; } = null;

	public ConfluenceUserImpl creator { get; private set; } = null;
	public ConfluenceUserImpl lastModifier { get; private set; } = null;
	public Space space { get; private set; } = null;
	public Page parent { get; private set; } = null;
	public ConfluenceObjectCollection<BodyContent> bodyContents { get; private set; } = null;
	public ConfluenceObjectCollection<OutgoingLink> outgoingLinks { get; private set; } = null;
	public ConfluenceObjectCollection<ReferralLink> referralLinks { get; private set; } = null;
	public ConfluenceObjectCollection<ContentProperty> contentProperties { get; private set; } = null;
	public ConfluenceObjectCollection<Page> historicalVersions { get; private set; } = null;
	public ConfluenceObjectCollection<Attachment> attachments { get; private set; } = null;
	public ConfluenceObjectCollection<Comment> comments { get; private set; } = null;
	public ConfluenceObjectCollection<CustomContentEntityObject> customContent { get; private set; } = null;
	public ConfluenceObjectCollection<Page> children { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		hibernateVersion = x.GetPropertyIntegerValue(nameof(hibernateVersion));
		title = x.GetPropertyValue(nameof(title));
		lowerTitle = x.GetPropertyValue(nameof(lowerTitle));
		version = x.GetPropertyIntegerValue(nameof(version));
		creatorId = x.GetRelationValue(nameof(creator));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModifierId = x.GetRelationValue(nameof(lastModifier));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
		versionComment = x.GetPropertyValue(nameof(versionComment));
		originalVersionId = x.GetRelationValue(nameof(originalVersionId));
		contentStatus = x.GetPropertyValue(nameof(contentStatus));
		spaceId = x.GetRelationValue(nameof(space));
		position = x.GetPropertyIntegerValue(nameof(position));
		parentId = x.GetRelationValue(nameof(parent));
		bodyContentsIds = x.GetIdentifierCollections(nameof(bodyContents)).ToArray();
		outgoingLinksIds = x.GetIdentifierCollections(nameof(outgoingLinks)).ToArray();
		referralLinksIds = x.GetIdentifierCollections(nameof(referralLinks)).ToArray();
		contentPropertiesIds = x.GetIdentifierCollections(nameof(contentProperties)).ToArray();
		historicalVersionsIds = x.GetIdentifierCollections(nameof(historicalVersions)).ToArray();
		attachmentsIds = x.GetIdentifierCollections(nameof(attachments)).ToArray();
		commentsIds = x.GetIdentifierCollections(nameof(comments)).ToArray();
		customContentIds = x.GetIdentifierCollections(nameof(customContent)).ToArray();
		childrenIds = x.GetIdentifierCollections(nameof(children)).ToArray();
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		creator = o.TryGetItem(creatorId) as ConfluenceUserImpl;
		lastModifier = o.TryGetItem(lastModifierId) as ConfluenceUserImpl;
		space = o.TryGetItem(spaceId) as Space;
		parent = o.TryGetItem(parentId) as Page;
		bodyContents = o.GetMultipleItems(bodyContentsIds).Cast<BodyContent>().ToCollection();
		outgoingLinks = o.GetMultipleItems(outgoingLinksIds).Cast<OutgoingLink>().ToCollection();
		referralLinks = o.GetMultipleItems(referralLinksIds).Cast<ReferralLink>().ToCollection();
		contentProperties = o.GetMultipleItems(contentPropertiesIds).Cast<ContentProperty>().ToCollection();
		historicalVersions = o.GetMultipleItems(historicalVersionsIds).Cast<Page>().ToCollection();
		attachments = o.GetMultipleItems(attachmentsIds).Cast<Attachment>().ToCollection();
		comments = o.GetMultipleItems(commentsIds).Cast<Comment>().ToCollection();
		customContent = o.GetMultipleItems(customContentIds).Cast<CustomContentEntityObject>().ToCollection();
		children = o.GetMultipleItems(childrenIds).Cast<Page>().ToCollection();
	}
	
	public void DetachFromParent()
	{
		if (parent != null)
			parent.children.Remove(this);
		parent = null;
		parentId = null;
	}
}

[ConfluenceType("com.atlassian.confluence.spaces", "SpaceDescription")]
public sealed class SpaceDescription : IConfluenceObject, IHasVersion
{	
	public string hibernateId { get; private set; } = null;
	public int? hibernateVersion { get; private set; } = null;
	public string title { get; private set; } = null;
	public string lowerTitle { get; private set; } = null;
	public int? version { get; private set; } = null;
	public string creatorId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModifierId { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;
	public string versionComment { get; private set; } = null;
	public string originalVersionId { get; private set; } = null;
	public string contentStatus { get; private set; } = null;
	public string spaceId { get; private set; } = null;
	public string[] bodyContentsIds { get; private set; } = null;
	public string[] labellingsIds { get; private set; } = null;

	public ConfluenceUserImpl creator { get; private set; } = null;
	public ConfluenceUserImpl lastModifier { get; private set; } = null;
	public Space space { get; private set; } = null;
	public ConfluenceObjectCollection<BodyContent> bodyContents { get; private set; } = null;
	public ConfluenceObjectCollection<Labelling> labellings { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		hibernateVersion = x.GetPropertyIntegerValue(nameof(hibernateVersion));
		title = x.GetPropertyValue(nameof(title));
		lowerTitle = x.GetPropertyValue(nameof(lowerTitle));
		version = x.GetPropertyIntegerValue(nameof(version));
		creatorId = x.GetRelationValue(nameof(creator));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModifierId = x.GetRelationValue(nameof(lastModifier));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
		versionComment = x.GetPropertyValue(nameof(versionComment));
		originalVersionId = x.GetRelationValue(nameof(originalVersionId));
		contentStatus = x.GetPropertyValue(nameof(contentStatus));
		spaceId = x.GetRelationValue(nameof(space));
		bodyContentsIds = x.GetIdentifierCollections(nameof(bodyContents)).ToArray();
		labellingsIds = x.GetIdentifierCollections(nameof(labellings)).ToArray();
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		creator = o.TryGetItem(creatorId) as ConfluenceUserImpl;
		lastModifier = o.TryGetItem(lastModifierId) as ConfluenceUserImpl;
		space = o.TryGetItem(spaceId) as Space;
		bodyContents = o.GetMultipleItems(bodyContentsIds).Cast<BodyContent>().ToCollection();
		labellings = o.GetMultipleItems(labellingsIds).Cast<Labelling>().ToCollection();
	}
}

[ConfluenceType("com.atlassian.confluence.mail.notification", "Notification")]
public sealed class Notification : IConfluenceObject
{	
	public string hibernateId { get; private set; } = null;
	public string receiverId { get; private set; } = null;
	public string creatorId { get; private set; } = null;
	public string creationDate { get; private set; } = null;
	public string lastModifierId { get; private set; } = null;
	public string lastModificationDate { get; private set; } = null;
	public string digest { get; private set; } = null;
	public string network { get; private set; } = null;
	public string type { get; private set; } = null;

	public ConfluenceUserImpl receiver { get; private set; } = null;
	public ConfluenceUserImpl creator { get; private set; } = null;
	public ConfluenceUserImpl lastModifier { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		receiverId = x.GetRelationValue(nameof(receiver));
		creatorId = x.GetRelationValue(nameof(creator));
		creationDate = x.GetPropertyValue(nameof(creationDate));
		lastModifierId = x.GetRelationValue(nameof(lastModifier));
		lastModificationDate = x.GetPropertyValue(nameof(lastModificationDate));
		digest = x.GetPropertyValue(nameof(digest));
		network = x.GetPropertyValue(nameof(network));
		type = x.GetPropertyValue(nameof(type));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o)
	{
		receiver = o.TryGetItem(receiverId) as ConfluenceUserImpl;
		creator = o.TryGetItem(creatorId) as ConfluenceUserImpl;
		lastModifier = o.TryGetItem(lastModifierId) as ConfluenceUserImpl;
	}
}

[ConfluenceType("com.atlassian.confluence.user", "ConfluenceUserImpl")]
public sealed class ConfluenceUserImpl : IConfluenceObject
{	
	public string hibernateId { get; private set; } = null;
	public string name { get; private set; } = null;
	public string lowerName { get; private set; } = null;
	public string atlassianAccountId { get; private set; } = null;
	
	public void Load(HibernateObject x)
	{
		hibernateId = x.Identifier.Content;
		name = x.GetPropertyValue(nameof(name));
		lowerName = x.GetPropertyValue(nameof(lowerName));
		atlassianAccountId = x.GetPropertyValue(nameof(atlassianAccountId));
	}

	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o) { }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class ConfluenceTypeAttribute : Attribute
{
	public ConfluenceTypeAttribute(string package, string @class)
	{
		Package = package ?? throw new ArgumentNullException(nameof(package));
		Class = @class ?? throw new ArgumentNullException(nameof(@class));
	}
	
	public string Package { get; }
	public string Class { get; }

	public override string ToString() => $"{Package}.{Class}";
}

public static class ConfluenceExtensions
{
	public static IEnumerable<T> ExtractLatestVersionsOnly<T>(this IEnumerable<T> o)
		where T : IHasVersion
	{
		return o
			.Where(x => string.Equals("current", x.contentStatus, StringComparison.Ordinal))
			.GroupBy(x => x.title)
			.Select(x => x.OrderByDescending(y => y.version).FirstOrDefault())
			.Where(x => x != null);
	}
	
	public static ConfluenceObjectCollection<BucketPropertySetItem> GetBucketPropertySetItems(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is BucketPropertySetItem).Select(x => x as BucketPropertySetItem).ToCollection();
	
	public static ConfluenceObjectCollection<Labelling> GetLabellings(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is Labelling).Select(x => x as Labelling).ToCollection();
	
	public static ConfluenceObjectCollection<Label> GetLabels(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is Label).Select(x => x as Label).ToCollection();

	public static ConfluenceObjectCollection<ReferralLink> GetReferralLinks(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is ReferralLink).Select(x => x as ReferralLink).ToCollection();

	public static ConfluenceObjectCollection<OutgoingLink> GetOutgoingLinks(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is OutgoingLink).Select(x => x as OutgoingLink).ToCollection();

	public static ConfluenceObjectCollection<Attachment> GetAttachments(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is Attachment).Select(x => x as Attachment).ToCollection();
	
	public static ConfluenceObjectCollection<BodyContent> GetBodyContents(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is BodyContent).Select(x => x as BodyContent).ToCollection();
	
	public static ConfluenceObjectCollection<Comment> GetComments(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is Comment).Select(x => x as Comment).ToCollection();
	
	public static ConfluenceObjectCollection<SpacePermission> GetSpacePermissions(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is SpacePermission).Select(x => x as SpacePermission).ToCollection();
	
	public static ConfluenceObjectCollection<ContentProperty> GetContentProperties(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is ContentProperty).Select(x => x as ContentProperty).ToCollection();
	
	public static ConfluenceObjectCollection<UserToContentRelationEntity> GetUserToContentRelationEntities(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is UserToContentRelationEntity).Select(x => x as UserToContentRelationEntity).ToCollection();
	
	public static ConfluenceObjectCollection<LikeEntity> GetLikeEntities(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is LikeEntity).Select(x => x as LikeEntity).ToCollection();
	
	public static ConfluenceObjectCollection<CustomContentEntityObject> GetCustomContentEntityObjects(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is CustomContentEntityObject).Select(x => x as CustomContentEntityObject).ToCollection();
	
	public static ConfluenceObjectCollection<Space> GetSpaces(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is Space).Select(x => x as Space).ToCollection();

	public static ConfluenceObjectCollection<Page> GetPages(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is Page).Select(x => x as Page).ToCollection();

	public static ConfluenceObjectCollection<SpaceDescription> GetSpaceDescriptions(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is SpaceDescription).Select(x => x as SpaceDescription).ToCollection();

	public static ConfluenceObjectCollection<Notification> GetNotifications(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is Notification).Select(x => x as Notification).ToCollection();

	public static ConfluenceObjectCollection<ConfluenceUserImpl> GetConfluenceUesrImpls(this IEnumerable<IConfluenceObject> o)
		=> o.Where(x => x is ConfluenceUserImpl).Select(x => x as ConfluenceUserImpl).ToCollection();
	
	public static ConfluenceObjectCollection<IConfluenceObject> GetConfluenceObjects(this HibernateGeneric o, bool expand = true)
	{
		var list = new ConfluenceObjectCollection<IConfluenceObject>();
		foreach (var eachType in typeof(ConfluenceExtensions).Assembly.GetTypes())
		{
			var confluenceTypeAttr = eachType.GetCustomAttribute<ConfluenceTypeAttribute>(false);
			
			if (confluenceTypeAttr == null)
				continue;

			foreach (var x in o.Objects)
			{
				if (!string.Equals(x.Package, confluenceTypeAttr.Package, StringComparison.Ordinal) ||
					!string.Equals(x.Class, confluenceTypeAttr.Class, StringComparison.Ordinal))
					continue;
				
				var instance = (IConfluenceObject)Activator.CreateInstance(eachType);
				instance.Load(x);
				list.Add(instance);
			}
		}
		
		if (expand)
		{
			foreach (var eachObject in list)
				eachObject.Expand(list);
		}
		
		return list;
	}

	public static TConfluenceObject GetRelatedObject<TConfluenceObject>(this HibernateObject o, string propertyName, ConfluenceObjectCollection<TConfluenceObject> entities)
		where TConfluenceObject : IConfluenceObject
		=> entities[o.GetHibernateIdentifier(propertyName)];

	public static ConfluenceObjectCollection<TConfluenceObject> ToCollection<TConfluenceObject>(this IEnumerable<TConfluenceObject> o)
		where TConfluenceObject : IConfluenceObject
	{
		var data = new ConfluenceObjectCollection<TConfluenceObject>();
		foreach (var eachItem in o) data.Add(eachItem);
		return data;
	}
}

public interface IHasVersion
{
	public string title { get; }
	public int? version { get; }
	public string contentStatus { get; }
}

public interface IConfluenceObject
{
	public string hibernateId { get; }
	public void Load(HibernateObject x);
	public void Expand(ConfluenceObjectCollection<IConfluenceObject> o);
}

public sealed class ConfluenceObjectCollection<TConfluenceObject> : KeyedCollection<string, TConfluenceObject>
	where TConfluenceObject : IConfluenceObject
{
	protected override string GetKeyForItem(TConfluenceObject item) => item.hibernateId;
	
	public TConfluenceObject TryGetItem(string key)
	{
		if (key == null) return default(TConfluenceObject);
		else if (this.TryGetValue(key, out TConfluenceObject o)) return o;
		else return default(TConfluenceObject);
	}
	
	public IEnumerable<TConfluenceObject> GetMultipleItems(string[] idlist)
	{
		var list = new List<TConfluenceObject>();
		
		foreach (var eachId in idlist)
			list.Add(TryGetItem(eachId));
			
		return list;
	}
}

#endregion

/* Hibernate */

#region "Hibernate"

public static class HibernateExtensions
{
	public static string GetHibernateType(this IHibernateType type)
		=> $"{type.Package}.{type.Class}";
		
	public static string GetPropertyValue(this HibernateObject o, string propertyName)
		=> o.Properties.Where(x => string.Equals(x.Name, propertyName, StringComparison.Ordinal)).Select(x => x.Content?.Normalize()).FirstOrDefault();

	public static int? GetPropertyIntegerValue(this HibernateObject o, string propertyName)
		=> int.TryParse(o.GetPropertyValue(propertyName), out int v) ? v : null;

	public static string GetRelationValue(this HibernateObject o, string propertyName)
		=> o.Properties.Where(x => string.Equals(x.Name, propertyName, StringComparison.Ordinal)).Select(x => x.Identifier?.Content).FirstOrDefault();

	public static string GetHibernateIdentifier(this HibernateObject o, string propertyName)
		=> o.Properties.Where(x => string.Equals(x.Name, propertyName, StringComparison.Ordinal)).Select(x => x.Identifier?.Content).FirstOrDefault();
		
	public static IEnumerable<string> GetIdentifierCollections(this HibernateObject o, string collectionName)
		=> o.Collections.Where(x => string.Equals(x.Name, collectionName, StringComparison.Ordinal)).SelectMany(x => x.Elements.Select(y => y.Identifier.Content)).ToList();
}

public interface IHibernateType
{
	public string Package { get; }

	public string Class { get; }
}

[XmlRoot("hibernate-generic")]
public sealed class HibernateGeneric
{
	[XmlAttribute("datetime")]
	public string DateTime { get; set; } = null;

	[XmlElement("object")]
	public List<HibernateObject> Objects { get; } = new List<HibernateObject>();
}

[XmlType(TypeName = "object")]
public sealed class HibernateObject : IHibernateType
{
	[XmlAttribute("class")]
	public string Class { get; set; } = null;

	[XmlAttribute("package")]
	public string Package { get; set; } = null;

	[XmlElement("id")]
	public HibernateIdentifier Identifier { get; set; }

	[XmlElement("property")]
	public List<HibernateProperty> Properties { get; } = new List<HibernateProperty>();
	
	[XmlElement("collection")]
	public List<HibernateCollection> Collections { get; } = new List<HibernateCollection>();
}

[XmlType(TypeName = "collection")]
public sealed class HibernateCollection
{
	[XmlAttribute("name")]
	public string Name { get; set; } = null;
	
	[XmlAttribute("class")]
	public string Class { get; set; } = null;
	
	[XmlElement("element")]
	public List<HibernateElement> Elements { get; } = new List<HibernateElement>();
}

[XmlType(TypeName = "element")]
public sealed class HibernateElement : IHibernateType
{
	[XmlAttribute("class")]
	public string Class { get; set; } = null;

	[XmlAttribute("package")]
	public string Package { get; set; } = null;

	[XmlElement("id")]
	public HibernateIdentifier Identifier { get; set; }
}

[XmlType(TypeName = "id")]
public sealed class HibernateIdentifier
{
	[XmlAttribute("name")]
	public string Name { get; set; } = null;

	[XmlText]
	public string Content { get; set; } = null;
}

[XmlType(TypeName = "property")]
public sealed class HibernateProperty : IHibernateType
{
	[XmlAttribute("name")]
	public string Name { get; set; } = null;

	[XmlAttribute("class")]
	public string Class { get; set; } = null;

	[XmlAttribute("enum-class")]
	public string EnumClass { get; set; } = null;

	[XmlAttribute("package")]
	public string Package { get; set; } = null;

	[XmlElement("id")]
	public HibernateIdentifier Identifier { get; set; }

	[XmlText]
	public string Content { get; set; } = null;
}

#endregion