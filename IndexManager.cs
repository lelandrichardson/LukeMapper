using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Instrumentation;
using System.Threading;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using LukeMapper;
using Version = Lucene.Net.Util.Version;

namespace LukeMapper
{
    public static class IndexManager
    {
        //TODO: remove filepath dependancy
        private const string IndexDirectory = @"E:\code\LukeMapper\_indexes\";

        public static readonly Version Version = Version.LUCENE_29;

        private static readonly ConcurrentDictionary<string, Singlet> _singlets = new ConcurrentDictionary<string, Singlet>();
        
        public static Singlet Of(string indexName)
        {
            return _singlets.GetOrAdd(indexName, (s) => new Singlet(s));
        }

        public sealed class Singlet
        {

            private static object syncRoot = new Object();

            private IndexWriter _writer;
            private IndexSearcher _searcher;

            private int _activeSearches = 0;
            private int _activeWrites = 0;
            private string indexName;

            private static DirectoryInfo GetDirectory(string indexName)
            {
                return new DirectoryInfo(Path.Combine(IndexDirectory, indexName));
            }

            private static IndexWriter CreateWriter(string indexName)
            {
                var dir = FSDirectory.Open(GetDirectory(indexName));

                return new IndexWriter(dir, new StandardAnalyzer(Version), !IndexReader.IndexExists(dir), IndexWriter.MaxFieldLength.UNLIMITED);
            }
            private Singlet() { }

            internal Singlet(string indexName)
            {
                this.indexName = indexName;
                lock (syncRoot)
                {
                    _writer = CreateWriter(indexName);
                    _searcher = new IndexSearcher(_writer.GetReader());
                }
            }

            public IEnumerable<Document> Search(Func<IndexSearcher, IEnumerable<Document>> searchMethod)
            {
                lock (syncRoot)
                {
                    if (_searcher != null && !_searcher.GetIndexReader().IsCurrent() && _activeSearches == 0)
                    {
                        _searcher.Close();
                        _searcher = null;
                    }
                    if (_searcher == null)
                    {
                        _searcher = new IndexSearcher((_writer ?? (_writer = CreateWriter(indexName))).GetReader());
                    }
                }
                IEnumerable<Document> results;
                Interlocked.Increment(ref _activeSearches);
                try
                {
                    results = searchMethod(_searcher);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeSearches);
                }
                return results;
            }

            public IEnumerable<T> Search<T>(Func<IndexSearcher, IEnumerable<T>> searchMethod)
            {
                lock (syncRoot)
                {
                    if (_searcher != null && !_searcher.GetIndexReader().IsCurrent() && _activeSearches == 0)
                    {
                        _searcher.Close();
                        _searcher = null;
                    }
                    if (_searcher == null)
                    {
                        _searcher = new IndexSearcher((_writer ?? (_writer = CreateWriter(indexName))).GetReader());
                    }
                }
                IEnumerable<T> results;
                Interlocked.Increment(ref _activeSearches);
                try
                {
                    results = searchMethod(_searcher);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeSearches);
                }
                return results;
            }

            public IEnumerable<T> Query<T>(Query query, int n)
            {
                lock (syncRoot)
                {
                    if (_searcher != null && !_searcher.GetIndexReader().IsCurrent() && _activeSearches == 0)
                    {
                        _searcher.Close();
                        _searcher = null;
                    }
                    if (_searcher == null)
                    {
                        _searcher = new IndexSearcher((_writer ?? (_writer = CreateWriter(indexName))).GetReader());
                    }
                }
                IEnumerable<T> results;
                Interlocked.Increment(ref _activeSearches);
                try
                {
                    results = _searcher.Query<T>(query, n);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeSearches);
                }
                return results;
            }


            //public void Write<T>(IEnumerable<T> entities)
            //{
            //    lock (syncRoot)
            //    {
            //        if (_writer == null)
            //        {
            //            _writer = CreateWriter(indexName);
            //        }
            //    }
            //    try
            //    {
            //        Interlocked.Increment(ref _activeWrites);
            //        _writer.Write<T>(entities, new StandardAnalyzer(Version));
            //    }
            //    finally
            //    {
            //        lock (syncRoot)
            //        {
            //            int writers = Interlocked.Decrement(ref _activeWrites);
            //            if (writers == 0)
            //            {
            //                _writer.Close();
            //                _writer = null;
            //            }
            //        }
            //    }
            //}

            public void Write(IEnumerable<Document> docs)
            {
                lock (syncRoot)
                {
                    if (_writer == null)
                    {
                        _writer = CreateWriter(indexName);
                    }
                }
                try
                {
                    Interlocked.Increment(ref _activeWrites);
                    foreach (Document document in docs)
                    {
                        _writer.AddDocument(document, new StandardAnalyzer(Version));
                    }

                }
                finally
                {
                    lock (syncRoot)
                    {
                        int writers = Interlocked.Decrement(ref _activeWrites);
                        if (writers == 0)
                        {
                            _writer.Close();
                            _writer = null;
                        }
                    }
                }
            }

            public void DeleteAll()
            {
                lock (syncRoot)
                {
                    if (_writer == null)
                    {
                        _writer = CreateWriter(indexName);
                    }
                }
                try
                {
                    _writer.DeleteAll();
                }
                finally
                {
                    lock (syncRoot)
                    {
                        int writers = Interlocked.Decrement(ref _activeWrites);
                        if (writers == 0)
                        {
                            _writer.Close();
                            _writer = null;
                        }
                    }
                }
            }

            public void Update(IEnumerable<Document> docs, Func<Document, Term> getIdentiferTerm)
            {
                lock (syncRoot)
                {
                    if (_writer == null)
                    {
                        _writer = CreateWriter(indexName);
                    }
                }
                try
                {
                    Interlocked.Increment(ref _activeWrites);
                    foreach (Document document in docs)
                    {
                        _writer.UpdateDocument(getIdentiferTerm(document), document, new StandardAnalyzer(Version));
                    }

                }
                finally
                {
                    lock (syncRoot)
                    {
                        int writers = Interlocked.Decrement(ref _activeWrites);
                        if (writers == 0)
                        {
                            _writer.Close();
                            _writer = null;
                        }
                    }
                }
            }

            public void Close()
            {
                lock (syncRoot)
                {
                    _searcher.Close();
                    _searcher.Dispose();
                    _searcher = null;

                    _writer.Close();
                    _writer.Dispose();
                    _writer = null;
                }
            }
        }

    }

}
