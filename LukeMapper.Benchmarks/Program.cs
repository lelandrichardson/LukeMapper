using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Version = Lucene.Net.Util.Version;

namespace LukeMapper.Benchmarks
{
    class Program
    {
        private const int ObjectCount = 500;
        private const int IteratorCount = 20;
        private static IndexWriter GetWriter()
        {
            var dir = FSDirectory.Open(new DirectoryInfo(@"E:\code\LukeMapper\_indexes\test-index"));

            return new IndexWriter(dir, 
                new StandardAnalyzer(Version.LUCENE_29), 
                !IndexReader.IndexExists(dir), 
                IndexWriter.MaxFieldLength.UNLIMITED);
        }

        static void Main(string[] args)
        {

            // Initialize Everything
            // ----------------------------------------------------------------
            var watch = new Stopwatch();
            var writer = GetWriter();
            var analyzer = new StandardAnalyzer(Version.LUCENE_29);
            
            var query = new TermQuery(new Term("PropString", "teststring"));
            int iteratorCount;
            IndexReader reader;
            IndexSearcher searcher;
            List<TestClass1> testObjects;
            List<TestClass1> objectResults;
            List<Document> documentResults;
            List<long> Times = new List<long>();


            // Create Test Objects
            // ----------------------------------------------------------------
            testObjects = new List<TestClass1>();

            for (int i = 0; i < ObjectCount; i++)
            {
                testObjects.Add(new TestClass1(i));
            }



            // Test speed of writes
            // ----------------------------------------------------------------
            watch.Reset();
            watch.Start();
            writer.Write(testObjects.Take(1), analyzer);
            watch.Stop();
            Console.WriteLine("First Write: {0}ms", watch.ElapsedMilliseconds);

            iteratorCount = IteratorCount;
            while (iteratorCount-- > 0)
            {
                watch.Reset();
                watch.Start();
                writer.Write(testObjects, analyzer);
                watch.Stop();

                Times.Add(watch.ElapsedMilliseconds);
                //Console.WriteLine("{0} Objects Written in {1}ms and {2:#,#} ticks", ObjectCount, watch.ElapsedMilliseconds, watch.ElapsedTicks);
            }
            Console.WriteLine("Average After Cached: {0}ms", Times.Skip(1).Average());
            Times.Clear();

            reader = writer.GetReader();
            searcher = new IndexSearcher(reader);





            // Test speed of reads
            // ----------------------------------------------------------------
            iteratorCount = IteratorCount;
            while (iteratorCount-- > 0)
            {
                watch.Reset();
                watch.Start();
                objectResults = searcher.Query<TestClass1>(query, ObjectCount).ToList();
                watch.Stop();
                Times.Add(watch.ElapsedMilliseconds);
                //Console.WriteLine("{0} Objects Read in {1}ms and {2:#,#} ticks", objectResults.Count, watch.ElapsedMilliseconds, watch.ElapsedTicks);    
            }

            Console.WriteLine("First Read: {0}ms", Times[0]);
            Console.WriteLine("Average After Cached: {0}ms", Times.Average());
            Times.Clear();

            // delete all
            writer.DeleteAll();


            Console.WriteLine("-------------- Native Lucene Methods ---------------");


            watch.Reset();
            watch.Start();
            writer.AddDocument(TestClass1.ToDocument(testObjects[0]));
            watch.Stop();
            Console.WriteLine("First Write: {0}ms", watch.ElapsedMilliseconds);

            iteratorCount = IteratorCount;
            while (iteratorCount-- > 0)
            {
                watch.Reset();
                watch.Start();
                foreach (var testObject in testObjects)
                {
                    writer.AddDocument(TestClass1.ToDocument(testObject));
                }
                watch.Stop();

                Times.Add(watch.ElapsedMilliseconds);
            }
            Console.WriteLine("Average After Cached: {0}ms", Times.Average());
            Times.Clear();

            iteratorCount = IteratorCount;
            while (iteratorCount-- > 0)
            {
                watch.Reset();
                watch.Start();
                documentResults =
                    searcher.Search(query, ObjectCount).ScoreDocs.Select(sd => reader.Document(sd.doc)).ToList();
                watch.Stop();
                Times.Add(watch.ElapsedMilliseconds);
            }

            Console.WriteLine("First Read: {0}ms", Times[0]);
            Console.WriteLine("Average After Cached: {0}ms", Times.Skip(1).Average());
            Times.Clear();

            // delete all
            writer.DeleteAll();


            Console.ReadLine();
        }

    }

    public class TestClass1
    {
        public int Id;

        public string PropString { get; set; }
        //public DateTime DateTime { get; set; }
        //public int? NullId { get; set; }

        public TestClass1(){}
        public TestClass1(int i)
        {
            Id = i;
            PropString = "teststring";
            //DateTime = DateTime.UtcNow;
            //NullId = 23;
        }

        public static Document ToDocument(TestClass1 obj)
        {
            var doc = new Document();

            doc.Add(new Field("Id", obj.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropString", obj.PropString, Field.Store.YES, Field.Index.ANALYZED));
            //doc.Add(new Field("DateTime", LukeMapper.ToDateString(obj.DateTime), Field.Store.YES,
            //                  Field.Index.NOT_ANALYZED_NO_NORMS));
            //doc.Add(new Field("NullId", obj.NullId.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            return doc;
        }
    }
}
