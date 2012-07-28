using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using LukeMapper;

namespace LukeMapperTest
{
    class Program
    {
        static void Main(string[] args)
        {

            //Console.WriteLine(typeof(bool).FullName);
            //Console.WriteLine(typeof(int).FullName);
            //Console.WriteLine(typeof(char).FullName);
            //Console.WriteLine(typeof(long).FullName);
            //Console.WriteLine(typeof(DateTime).FullName);


            //Console.WriteLine(typeof(int?).FullName);
            //Console.WriteLine(typeof(char?).FullName);
            //Console.WriteLine(typeof(DateTime?).FullName);
            //Console.WriteLine(DateTime.UtcNow.ToString("s"));
            //Console.ReadLine();
            //return;
            const string index = "test-index";
            IndexManager.Instance.DeleteAll(index);
            IndexManager.Instance.Write(index,
                new List<Document>
                {
                    PocoDocument("1","the quick brown fox",DateTime.UtcNow.ToString("s"),"1"),
                    PocoDocument("2","the lazy brown fox",DateTime.UtcNow.ToString("s"),"1"),
                    PocoDocument("3","jumped over the lazy dog",DateTime.UtcNow.ToString("s"),"1"),
                    PocoDocument("4","is not so fast any more",DateTime.UtcNow.ToString("s"),"1")
                });

            var results = IndexManager.Instance.Search<PocoObject>(index,
                    searcher =>
                        {
                            var qry = new TermQuery(new Term("ID", "1"));
                            return
                                searcher.Query<PocoObject>(qry, 2).ToList();
                        }
                );

            foreach (var poco in results)
            {
                Console.WriteLine("{0}, {1}, {2}, {3}",poco.Id, poco.DisplayText, poco.PropId,poco.Dt);
            }
            Console.ReadLine();

        }

        static Document PocoDocument(
            string id, 
            string dispText, 
            string dateTime,
            string theBool
            )
        {
            var doc = new Document();
            doc.Add(new Field("ID", id, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("DisplayText", dispText, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("PropId", id, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("Dt", dateTime, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropDt", dateTime, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("Bl", theBool, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("Ch", dispText[0].ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            return doc;
        }

        public static PocoObject ExampleMethod(Document document)
        {
            var poco = new PocoObject();
            try
            {
                //poco.ID = document.Get("Id");
                //poco.DisplayText = document.Get("DisplayText");

                //poco.PropId = document.Get("PropId");
            }
            catch(Exception ex)
            {
                LukeMapper.LukeMapper.ThrowDataException(ex,"",document);
            }
            return poco;
        }

        public static object ExampleMethod2(Document document)
        {
            var poco = new PocoObject();

            int.TryParse(document.Get("Id"), out poco.Id);

            //poco.ID = int.Parse(document.Get("ID"));

            poco.DisplayText = document.Get("DisplayText");

            int newid2;
            int.TryParse(document.Get("PropId"), out newid2);
            poco.PropId = newid2;

            poco.Dt = LukeMapper.LukeMapper.GetDateTime(document.Get("Dt"));

            string s = document.Get("Ch");
            if(!string.IsNullOrEmpty(s))
            {
                poco.Ch = s[0];
            }

            return poco;
        }
    }

    public class PocoObject
    {
        public int Id;
        public int PropId { get; set; }

        public string DisplayText;
        public string PropDisplayText { get; set; }

        public char Ch;
        public char PropCh { get; set; }

        public bool Bl;
        public bool PropBl { get; set; }

        public DateTime Dt;
        public DateTime PropDt { get; set; }
    }
}

