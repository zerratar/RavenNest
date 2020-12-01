using Microsoft.VisualStudio.TestTools.UnitTesting;
using RavenNest.Blazor.Services;
using RavenNest.Blazor.Services.RSS;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml;

namespace RavenNest.UnitTests
{
    [TestClass]
    public class RssFeedReaderTest
    {
        [TestMethod]
        public async Task RssFeedParser_ParseMediumFeed_ReturnsNewsFeedItems()
        {
            var url = "https://medium.com/feed/ravenfall";
            var reader = new NewsReader(url);
            var news = await reader.GetNewsAsync();
            Assert.AreNotEqual(null, news);
            Assert.IsTrue(news.Count > 0);
            foreach (var item in news)
            {
                Assert.AreNotEqual(null, item.ImageSource);
                Assert.AreNotEqual(null, item.NewsSource);
                Assert.AreNotEqual(null, item.Title);
                Assert.AreNotEqual(null, item.ShortDescription);
                Assert.AreNotEqual(DateTime.MinValue, item.Published);
            }
        }
    }
}
