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
            const string index = "test-index";
            IndexManager.Instance.DeleteAll(index);
            IndexManager.Instance.Write(index,
                new List<Document>
                {
                    PocoDocument("1","the quick brown fox","123"),
                    PocoDocument("2","the lazy brown fox","123"),
                    PocoDocument("3","jumped over the lazy dog","123"),
                    PocoDocument("4","is not so fast any more","123")
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
                Console.WriteLine("{0}, {1}, {2}",poco.ID, poco.DisplayText, poco.PropId);
            }
            Console.ReadLine();

        }

        static Document PocoDocument(string id, string dispText, string propId)
        {
            var doc = new Document();
            doc.Add(new Field("ID", id, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("DisplayText", dispText, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("PropId", propId, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            return doc;
        }

        public static PocoObject ExampleMethod(Document document)
        {
            var poco = new PocoObject();
            try
            {
                poco.ID = document.Get("Id");
                poco.DisplayText = document.Get("DisplayText");

                poco.PropId = document.Get("PropId");
            }
            catch(Exception ex)
            {
                LukeMapper.LukeMapper.ThrowDataException(ex,"",document);
            }
            return poco;
        }

        public static PocoObject ExampleMethod2(Document document)
        {
            var poco = new PocoObject();
            poco.ID = document.Get("Id");
            poco.DisplayText = document.Get("DisplayText");

            poco.PropId = document.Get("PropId");
            return poco;
        }
    }

    class PocoObject
    {
        public string ID;
        public string DisplayText;

        public string PropId { get; set; }

    }
}

