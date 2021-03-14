using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace RavenNest.Blazor.Services.RSS
{
    public class NewsReader
    {
        private readonly string feedUrl;

        public NewsReader(string rssFeedUrl)
        {
            this.feedUrl = rssFeedUrl;
        }

        public async Task<List<NewsItem>> GetNewsAsync()
        {
            var output = new List<NewsItem>();

            try
            {
                using var reader = XmlReader.Create(this.feedUrl, new XmlReaderSettings
                {
                    Async = true
                });
                NewsItem currentItem = null;
                while (await reader.ReadAsync())
                {
                    if (reader.Name == "item")
                    {
                        if (reader.NodeType == XmlNodeType.EndElement)
                            continue;

                        currentItem = new NewsItem();
                        output.Add(currentItem);
                        continue;
                    }

                    if (currentItem == null)
                    {
                        continue;
                    }

                    if (reader.Name == "title")
                    {
                        currentItem.Title = await reader.ReadElementContentAsStringAsync();
                        continue;
                    }

                    if (reader.Name == "link")
                    {
                        currentItem.NewsSource = await reader.ReadElementContentAsStringAsync();
                        continue;
                    }

                    if (reader.Name == "category")
                    {
                        currentItem.Categories.Add(await reader.ReadElementContentAsStringAsync());
                        continue;
                    }

                    if (reader.Name == "dc:creator")
                    {
                        currentItem.Publisher = await reader.ReadElementContentAsStringAsync();
                        continue;
                    }

                    if (reader.Name == "pubDate")
                    {
                        if (DateTime.TryParse(await reader.ReadElementContentAsStringAsync(), out var dt))
                            currentItem.Published = dt;
                        continue;
                    }

                    if (reader.Name == "content:encoded")
                    {
                        var content = await reader.ReadElementContentAsStringAsync();

                        if (content.Contains("<h4>"))
                        {
                            currentItem.ShortDescription = content.Split(new string[] { "<h4>", "</h4>" }, StringSplitOptions.None)[1];
                        }
                        else if (content.Contains("<ul>"))
                        {
                            currentItem.ShortDescription = content
                                .Split(new string[] { "<ul>", "</ul>" }, StringSplitOptions.None)[1]
                                .Replace("</li><li>", "<br/>")
                                .Replace("<li>", "");
                        }

                        currentItem.ImageSource = content.Substring(content.IndexOf("https://cdn-images-")).Split('"')[0];

                        continue;
                    }
                }
            }
            catch (Exception exc)
            {

            }
            return output;
        }
    }
    public class NewsItem
    {
        public string NewsSource { get; set; }
        public string ImageSource { get; set; }
        public string Title { get; set; }
        public string ShortDescription { get; set; }
        public List<string> Categories { get; set; } = new List<string>();
        public string Publisher { get; set; }
        public DateTime Published { get; set; }
    }
}
