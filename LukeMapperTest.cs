using System;
using System.Text;
using Lucene.Net.Documents;

namespace LukeMapper
{
    class LukeMapperTest
    {
        void DesiredInterface()
        {
            //List<PocoClass> results;
            //IndexSearcher searcher;
            //var qry = new BooleanQuery();

            //results = searcher.Query<PocoClass>(qry);
        }

        public static PocoClass ExampleMethod(Document document)
        {
            var poco = new PocoClass();

            poco.Id = Convert.ToInt32(document.Get("Id"));
            poco.Name = document.Get("Name");

            poco.PropId = int.Parse(document.Get("PropId"));
            poco.PropName = document.Get("PropName");

            return poco;
        }

    }

    class PocoClass
    {
        public int Id;
        public string Name;

        public int PropId { get; set; }
        public string PropName { get; set; }
    }
}
