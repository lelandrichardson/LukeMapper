using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lucene.Net.Documents;

namespace LukeMapper
{
    public class ExampleMethods
    {
        public static Document MapperFunction(Poco obj)
        {
            var doc = new Document();

            doc.Add(new Field("Int", obj.Int.ToString(), Field.Store.YES,Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropInt", obj.PropInt.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field("String", obj.String, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropString", obj.PropString, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field("NullInt",obj.NullInt.ToString(),Field.Store.YES,Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropNullInt", obj.PropNullInt.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            return doc;
        }
    }

    public class Poco
    {
        public int Int;
        public int PropInt { get; set; }

        public int? NullInt;
        public int? PropNullInt { get; set; }

        public string String;
        public string PropString { get; set; }
    }
}
