using Raven.Client.Documents.Indexes;
using Raven.TestDriver;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace RavenTestCasesLoadDoc
{
    public class LoadDocumentOrRevisionHelper
    {
        public static T[] LoadDocument<T>(IEnumerable<string> ids)
        {
            throw new NotSupportedException("This can only be run on the server side");
        }

        public static T LoadDocument<T>(string id)
        {
            throw new NotSupportedException("This can only be run on the server side");
        }
    }

    public class TestCase : RavenTestDriver
    {
        public class Configuration
        {
            public string Id { get; set; }
            public bool ShowBlogPosts { get; set; }
        }

        public class BlogPost
        {
            public string Id { get; set; }
            public string Title { get; set; }
        }

        public class UsingNormalLoadDocument : AbstractIndexCreationTask<BlogPost>
        {
            public UsingNormalLoadDocument()
            {
                Map = blogposts => from post in blogposts
                                   let settings = LoadDocument<Configuration>("Config")
                                   where settings == null || settings.ShowBlogPosts
                                   select new
                                   {
                                       post.Title
                                   };

            }
        }

        public class UsingExtraLoadDocument : AbstractIndexCreationTask<BlogPost>
        {
            public UsingExtraLoadDocument()
            {
                Map = blogposts => from post in blogposts
                                   let settings = LoadDocumentOrRevisionHelper.LoadDocument<Configuration>("Config")
                                   where settings == null || settings.ShowBlogPosts
                                   select new
                                   {
                                       post.Title
                                   };

                this.AddLoadDocumentOrRevisionAdditionalSources();
            }
        }

        /// <summary>
        /// The index in this test uses RavenDB's default load document. This works
        /// </summary>
        [Fact]
        public void Test_UsingNormalLoadDocument()
        {
            TestIndex<UsingNormalLoadDocument>();
        }

        /// <summary>
        /// The index in this test uses an external method which then uses RavenDB's load document. This does not work
        /// </summary>
        [Fact]
        public void Test_UsingExtraLoadDocument()
        {
            TestIndex<UsingExtraLoadDocument>();
        }

        /// <summary>
        /// The test case goes as follows:
        /// - Save a blogpost and a config which shows blogposts
        /// - Query, see that the blogpost appears
        /// - Change config to hide blogposts
        /// - Blogposts should no longer appear
        /// </summary>
        /// <typeparam name="TIndex"></typeparam>
        private void TestIndex<TIndex>() where TIndex : AbstractIndexCreationTask<BlogPost>, new()
        {
            var store = GetDocumentStore();
            store.ExecuteIndex(new TIndex());

            using (var session = store.OpenSession())
            {
                session.Store(new BlogPost()
                {
                    Title = "This is a title"
                });
                session.Store(new Configuration()
                {
                    Id = "Config",
                    ShowBlogPosts = true
                });

                session.SaveChanges();
            }

            WaitForIndexing(store);
            using (var session = store.OpenSession())
            {
                var posts = session.Query<BlogPost, TIndex>().ToList();

                Assert.Single(posts);
            }

            using (var session = store.OpenSession())
            {
                var config = session.Load<Configuration>("Config");
                config.ShowBlogPosts = false;
                session.SaveChanges();
            }
            WaitForIndexing(store);
            using (var session = store.OpenSession())
            {
                var posts = session.Query<BlogPost, TIndex>().ToList();

                Assert.Empty(posts);
            }

        }
    }

    public static class LoadDocumentOrRevisionHelperIndexingExtensions
    {
        private static Dictionary<string, string> _additionalSources = new Dictionary<string, string>();

        static LoadDocumentOrRevisionHelperIndexingExtensions()
        {
            var additionalSources = new Dictionary<string, string>()
            {
                { "LoadDocumentOrRevisionHelper", "RavenTestCasesLoadDoc.AdditionalSources.LoadDocumentOrRevisionHelper.cs" }
            };

            foreach (var additionalSource in additionalSources)
            {
                var source = GetEmbeddedAdditionalSourceCode(additionalSource.Value);
                _additionalSources.Add(additionalSource.Key, source);
            }
        }

        public static void AddLoadDocumentOrRevisionAdditionalSources<T>(this AbstractGenericIndexCreationTask<T> self)
        {
            if (self.AdditionalSources == null)
                self.AdditionalSources = new Dictionary<string, string>(_additionalSources);
            else
            {
                foreach (var additionalSource in _additionalSources)
                {
                    self.AdditionalSources.Add(additionalSource.Key, additionalSource.Value);
                }
            }
        }

        public static string GetEmbeddedAdditionalSourceCode(string additionalSourcePath)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(additionalSourcePath))
            using (var sr = new StreamReader(stream))
            {
                return sr.ReadToEnd();
            }
        }
    }
}
