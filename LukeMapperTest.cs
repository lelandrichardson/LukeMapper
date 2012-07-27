using System;
using System.Collections.Generic;
using System.Text;
using Lucene.Net.Documents;
using Lucene.Net.Search;

namespace LukeMapper
{
    //class LukeMapperTest
    //{
    //    //void DesiredInterface()
    //    //{
    //    //    IndexSearcher searcher;
    //    //    Query qry;
    //    //    int numberToReturn = 10;

    //    //    List<PocoClass> results = searcher.Query<PocoClass>(qry, numberToReturn);
    //    //}

    //    //public static PocoClass ExampleMethod(Document document)
    //    //{
    //    //    var poco = new PocoClass();

    //    //    poco.Id = Convert.ToInt32(document.Get("Id"));
    //    //    poco.Name = document.Get("Name");

    //    //    poco.PropId = int.Parse(document.Get("PropId"));
    //    //    poco.PropName = document.Get("PropName");

    //    //    return poco;
    //    //}

    //}

    class PocoClass
    {
        public int Id;
        public string Name;

        public int PropId { get; set; }
        public string PropName { get; set; }
    }
}
