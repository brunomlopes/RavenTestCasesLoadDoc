using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Corax.Utils;
using Raven.Server.Documents.Indexes.Persistence.Lucene.Documents;
using Raven.Server.Documents.Indexes.Static;
using Raven.Server.Documents.Indexes.Static.Spatial;
using Raven.Server.NotificationCenter.Notifications;
using Sparrow.Json;
using Sparrow.Logging;

namespace SocialStack.RavenDB.IndexingExtensions.AdditionalSources
{
    public class LoadDocumentOrRevisionHelper
    {
        // Inspired in code from class StaticIndexBase (RavenDB)
        // BL: For testing purposes, this is currently a copy of RavenDB's LoadDocument
        public static dynamic LoadDocument(object keyOrEnumerable, string collectionName)
        {
            if (CurrentIndexingScope.Current == null)
                throw new InvalidOperationException(
                    "Indexing scope was not initialized. Key: " + keyOrEnumerable);

            if (keyOrEnumerable is LazyStringValue keyLazy)
                return LoadDocumentById(keyLazy, null, collectionName);

            if (keyOrEnumerable is string keyString)
                return LoadDocumentById(null, keyString, collectionName);

            if (keyOrEnumerable is DynamicNullObject || keyOrEnumerable is null)
                return DynamicNullObject.Null;

            if (keyOrEnumerable is IEnumerable enumerable)
            {
                var enumerator = enumerable.GetEnumerator();
                using (enumerable as IDisposable)
                {
                    var items = new List<dynamic>();
                    while (enumerator.MoveNext())
                    {
                        items.Add(LoadDocument(enumerator.Current, collectionName));
                    }
                    if (items.Count == 0)
                        return DynamicNullObject.Null;

                    return new DynamicArray(items);
                }
            }

            throw new InvalidOperationException(
                "LoadDocument may only be called with a string or an enumerable, but was called with a parameter of type " +
                keyOrEnumerable.GetType().FullName + ": " + keyOrEnumerable);
        }

        private static dynamic LoadDocumentById(LazyStringValue lazyId, string id, string collectionName)
        {
            var isRevision = lazyId?.Contains("/revisions/")
                             ?? id?.Contains("/revisions/")
                             ?? false;

            return isRevision
                ? CurrentIndexingScope.Current.LoadDocument(lazyId, id, "Revisions/" + collectionName)
                : CurrentIndexingScope.Current.LoadDocument(lazyId, id, collectionName);
        }
    }
}
