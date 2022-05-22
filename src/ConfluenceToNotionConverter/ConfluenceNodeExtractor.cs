using System.Xml;

namespace ConfluenceToNotionConverter
{
    internal static class ConfluenceNodeExtractor
	{
		public static IEnumerable<XmlElement> PopulateCustomContentEntityObjects(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.content", "CustomContentEntityObject");
		public static IEnumerable<XmlElement> PopulateUser2ContentRelationEntities(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.internal.relations.dao", "User2ContentRelationEntity");
		public static IEnumerable<XmlElement> PopulateLikeEntities(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.like", "LikeEntity");
		public static IEnumerable<XmlElement> PopulateNotifications(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.mail.notification", "Notification");
		public static IEnumerable<XmlElement> PopulateConfluenceUserImpls(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.user", "ConfluenceUserImpl");
		public static IEnumerable<XmlElement> PopulatePages(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.pages", "Page");
		public static IEnumerable<XmlElement> PopulateConfluenceBandanaRecords(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.setup.bandana", "ConfluenceBandanaRecord");
		public static IEnumerable<XmlElement> PopulateSpaces(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.spaces", "Space");
		public static IEnumerable<XmlElement> PopulateContent2ContentRelationEntities(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.internal.relations.dao", "Content2ContentRelationEntity");
		public static IEnumerable<XmlElement> PopulateAttachments(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.pages", "Attachment");
		public static IEnumerable<XmlElement> PopulateContentProperties(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.content", "ContentProperty");
		public static IEnumerable<XmlElement> PopulateBodyContents(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.core", "BodyContent");
		public static IEnumerable<XmlElement> PopulateOutgoingLinks(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.links", "OutgoingLink");
		public static IEnumerable<XmlElement> PopulateLabellings(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.labels", "Labelling");
		public static IEnumerable<XmlElement> PopulateReferralLinks(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.links", "ReferralLink");
		public static IEnumerable<XmlElement> PopulateSpacePermissions(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.security", "SpacePermission");
		public static IEnumerable<XmlElement> PopulateComments(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.pages", "Comment");
		public static IEnumerable<XmlElement> PopulateContentPermissionSets(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.security", "ContentPermissionSet");
		public static IEnumerable<XmlElement> PopulateSpaceDescriptions(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.spaces", "SpaceDescription");
		public static IEnumerable<XmlElement> PopulateLabels(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.labels", "Label");
		public static IEnumerable<XmlElement> PopulateContentPermissions(XmlNode documentNode)
			=> PopulateObjects(documentNode, "com.atlassian.confluence.security", "ContentPermission");
		public static IEnumerable<XmlElement> PopulateBucketPropertySetItems(XmlNode documentNode)
			=> PopulateObjects(documentNode, "bucket.user.propertyset", "BucketPropertySetItem");

		public static IEnumerable<XmlElement> PopulateObjects(XmlNode documentNode, string package, string @class)
		{
			if (package.Contains('\'') || package.Contains('"'))
				throw new ArgumentException("Unexpected character included in package name.", nameof(package));

			if (@class.Contains('\'') || @class.Contains('"'))
				throw new ArgumentException("Unexpected character included in class name.", nameof(@class));

			return documentNode
				.SelectNodes($"/hibernate-generic/object[@package='{package}' and @class='{@class}']")
				.Cast<XmlElement>();
		}
	}
}
