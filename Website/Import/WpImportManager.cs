﻿using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Sitecore.Data.Items;
using Sitecore.Data;
using Sitecore.Data.Managers;
using Sitecore.Modules.WeBlog.Items.WeBlog;
using Sitecore.Modules.WeBlog.Managers;
using System.Xml;
using System;

namespace Sitecore.Modules.WeBlog.Import
{
    public static class WpImportManager
    {
        /// <summary>
        /// Imports the specified file.
        /// </summary>
        /// <param name="fileLocation">The file location.</param>
        /// <param name="includeComments">Determines if comments should be imported</param>
        /// <param name="includeCategories">Determines if categories should be imported</param>
        /// <param name="includeTags">Determines if tags should be imported</param>
        public static List<WpPost> Import(string fileLocation, bool includeComments, bool includeCategories, bool includeTags)
        {
            var nsm = new XmlNamespaceManager(new NameTable());
            nsm.AddNamespace("atom", "http://www.w3.org/2005/Atom");

            var parseContext = new XmlParserContext(null, nsm, null, XmlSpace.Default);
            using (var reader = XmlReader.Create(fileLocation, null, parseContext))
            {
                var doc = XDocument.Load(reader);

                var posts = (from item in doc.Descendants("item")
                             select new WpPost(item, includeComments, includeCategories, includeTags)).ToList();

                return posts;
            }
        }

        internal static void ImportPosts(Item blogItem, List<WpPost> listWordpressPosts, Database db, Action<string, int> logger = null)
        {
            BlogHomeItem customBlogItem = blogItem;
            var entryTemplate = TemplateManager.GetTemplate(customBlogItem.BlogSettings.EntryTemplateID, db);

            var processCount = 0;

            foreach (WpPost post in listWordpressPosts)
            {
                processCount++;

                if (!string.IsNullOrEmpty(post.Content))
                {
                    var name = ItemUtil.ProposeValidItemName(post.Title);

                    if (logger != null)
                        logger(name, processCount);

                    EntryItem entry = ItemManager.AddFromTemplate(name, entryTemplate.ID, blogItem);

                    entry.BeginEdit();
                    entry.Title.Field.Value = post.Title;
                    entry.Introduction.Field.Value = string.Empty;
                    entry.Content.Field.Value = post.Content;
                    entry.Tags.Field.Value = string.Join(", ", post.Tags.ToArray());

                    var categorieItems = new List<string>();

                    foreach (string categoryName in post.Categories)
                    {
                        var categoryItem = ManagerFactory.CategoryManagerInstance.Add(categoryName, blogItem);
                        categorieItems.Add(categoryItem.ID.ToString());
                    }
                 
                    if (categorieItems.Count > 0)
                    {
                        entry.Category.Field.Value = string.Join("|", categorieItems.ToArray());
                    }

                    foreach (WpComment wpComment in post.Comments)
                    {
                        ManagerFactory.CommentManagerInstance.AddCommentToEntry(entry.ID, wpComment);
                    }

                    var publicationDate = DateUtil.ToIsoDate(post.PublicationDate);
                    entry.InnerItem.Fields[FieldIDs.Created].Value = publicationDate;
                    entry.InnerItem.Fields["Entry Date"].Value = publicationDate;
                    entry.EndEdit();
                }
            }
        }
    }
}