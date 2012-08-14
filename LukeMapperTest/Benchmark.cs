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
    class Benchmark
    {
        const string indexName = "benchmark-index";
        static void Main(string[] args)
        {
            IndexManager.Instance.DeleteAll(indexName);

            var bm = new Benchmark();




            IndexManager.Instance.Write(indexName,);
            

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
            doc.Add(new Field("NullId", id, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("Dt", dateTime, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropDt", dateTime, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field("NullPropDt", dateTime, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("NullDt", dateTime, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field("Bl", theBool, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropBl", theBool, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("Ch", dispText[0].ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropCh", dispText[0].ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            
            return doc;
        }



        public static Document SomeObjectToDocument(SomeObject obj)
        {
            var doc = new Document();

            doc.Add(new Field("ID", 
                obj.Id.ToString(), 
                Field.Store.YES, 
                Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field("NullableIntProperty", 
                obj.NullableIntProperty.ToString(),
                Field.Store.YES, 
                Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field("FullName", 
                obj.FullName, 
                Field.Store.YES, 
                Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field("DisplayName", 
                obj.DisplayName, 
                Field.Store.YES, 
                Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field("SomeDateProperty", 
                obj.SomeDateProperty.ToString("s"), 
                Field.Store.YES, 
                Field.Index.NOT_ANALYZED_NO_NORMS));

            //add some more search-related fields if desired

            return doc;
        }

        private int ObjectCount = 0;
        TestObject GenerateTestObject()
        {
            var user = new TestObject();

            

            return user;
        }
    }

    public class TestObject
    {
        public int Id;
        public int? NullableIntProperty { get; set; }
        public string FullName;
        public string DisplayName { get; set; }
        public DateTime SomeDateProperty { get; set; }
    }
}

