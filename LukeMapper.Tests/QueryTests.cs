using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using LukeMapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LukeMapperTests
{
    [TestClass]
    public class QueryTests
    {
        private const string Index = "test-index";
        private List<PocoObject> TestObjects { get; set; }

        /// <summary>
        /// Map from PocoObject to Lucene Document
        /// </summary>
        private static Document MapPocoToDocument(PocoObject poco)
        {
            var doc = new Document();

            doc.Add(new Field("Id", poco.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("DisplayText", poco.DisplayText, Field.Store.YES, Field.Index.ANALYZED));
            doc.Add(new Field("PropDisplayText", poco.PropDisplayText, Field.Store.YES, Field.Index.ANALYZED));

            doc.Add(new Field("PropId", poco.PropId.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropNullId", poco.PropNullId.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("NullId", poco.NullId.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field("Long", poco.Long.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropLong", poco.PropLong.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropNullLong", poco.PropNullLong.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("NullLong", poco.NullLong.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));


            doc.Add(new Field("Dt", LukeMapper.LukeMapper.ToDateString(poco.Dt), Field.Store.YES,
                              Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropDt", LukeMapper.LukeMapper.ToDateString(poco.PropDt), Field.Store.YES,
                              Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field("NullPropDt",
                              poco.NullPropDt.HasValue ? LukeMapper.LukeMapper.ToDateString(poco.NullPropDt.Value) : "",
                              Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("NullDt",
                              poco.NullDt.HasValue ? LukeMapper.LukeMapper.ToDateString(poco.NullDt.Value) : "",
                              Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            doc.Add(new Field("Bl", poco.Bl.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropBl", poco.PropBl.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("Ch", poco.Ch.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropCh", poco.PropCh.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            return doc;
        }

        [TestInitialize]
        public void Init()
        {
            // create test objects
            TestObjects = new List<PocoObject>
                {
                    PocoObject.Random(1),
                    PocoObject.Random(2),
                    PocoObject.Random(3),
                    PocoObject.Random(4),
                    PocoObject.Random(5),
                    PocoObject.Random(6)
                };

            // set up test index


            // clear index
            IndexManager.Of(Index).DeleteAll();

            //write documents to test in
            IndexManager.Of(Index).Write(TestObjects.Select(MapPocoToDocument));
        }

        [TestMethod]
        public void TestMethod1()
        {

            foreach (var expected in TestObjects)
            {
                var query = new TermQuery(new Term("Id", expected.Id.ToString()));
                var results = IndexManager.Of(Index).Query<PocoObject>(query, 1).ToList();

                Assert.AreEqual(1, results.Count, "Returns only one object");

                var actual = results.SingleOrDefault();


                Assert.IsNotNull(actual, "Non-Null Result Returned");


                Assert.AreEqual(expected.Id, actual.Id, "Int32 Field");
                Assert.AreEqual(expected.PropId, actual.PropId, "Int32 Property");

                Assert.AreEqual(expected.NullId.GetValueOrDefault(), actual.NullId.GetValueOrDefault(), "Nullable Int32 Field");
                Assert.AreEqual(expected.PropNullId.GetValueOrDefault(), actual.PropNullId.GetValueOrDefault(), "Nullable Int32 Property");

                Assert.AreEqual(expected.Long, actual.Long, "Int64 Field");
                Assert.AreEqual(expected.PropLong, actual.PropLong, "Int64 Property");

                Assert.AreEqual(expected.NullLong.GetValueOrDefault(), actual.NullLong.GetValueOrDefault(), "Nullable Int64 Field");
                Assert.AreEqual(expected.PropNullLong.GetValueOrDefault(), actual.PropNullLong.GetValueOrDefault(), "Nullable Int64 Property");

                Assert.AreEqual(expected.DisplayText, actual.DisplayText, "String Field");
                Assert.AreEqual(expected.PropDisplayText, actual.PropDisplayText, "String Property");

                Assert.AreEqual(expected.Ch, actual.Ch, "Character Field");
                Assert.AreEqual(expected.PropCh, actual.PropCh, "Character Property");

                Assert.AreEqual(expected.Bl, actual.Bl, "Boolean Field");
                Assert.AreEqual(expected.PropBl, actual.PropBl,"Boolean Property");

                AssertDatesEqual(expected.Dt, actual.Dt, "DateTime Field");
                AssertDatesEqual(expected.PropDt, actual.PropDt, "DateTime Property");

                AssertDatesEqual(expected.NullDt.GetValueOrDefault(), actual.NullDt.GetValueOrDefault(), "Nullable DateTime");
                AssertDatesEqual(expected.NullPropDt.GetValueOrDefault(), actual.NullPropDt.GetValueOrDefault(), "Nullable DateTime Property");
            }
        }

        private void AssertDatesEqual(DateTime a, DateTime b, string message = null)
        {
            Assert.AreEqual(a.Year, b.Year, message);
            Assert.AreEqual(a.Month, b.Month, message);
            Assert.AreEqual(a.Day, b.Day, message);
            Assert.AreEqual(a.Hour, b.Hour, message);
            Assert.AreEqual(a.Minute, b.Minute, message);
            Assert.AreEqual(a.Second, b.Second, message);
        }

    }
}