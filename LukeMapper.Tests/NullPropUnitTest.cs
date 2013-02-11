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
    public class NullPropUnitTest
    {
        private const string Index = "test-index";

        [TestInitialize]
        public void Init()
        {
            // clear index
            IndexManager.Of(Index).DeleteAll();
        }

        [TestMethod]
        public void TestNullProp()
        {
            TestPropNullInt(12);
            TestPropNullInt(0);
            TestPropNullInt(-14);
            TestPropNullInt((int?)null);
        }

        public static void TestPropNullInt(int? nullInt)
        {
            var obj = new NullPropClass {PropNullInt = nullInt};

            var guid = Guid.NewGuid().ToString();

            var doc = new Document();

            doc.Add(new Field("Identifier", guid, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
            doc.Add(new Field("PropNullInt", obj.PropNullInt.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

            IndexManager.Of(Index).Write(new List<Document>{doc});

            var query = new TermQuery(new Term("Identifier", guid));
            var actual = IndexManager.Of(Index).Query<NullPropClass>(query, 1).SingleOrDefault();

            Assert.IsNotNull(actual, "Non-Null Result Returned");

            Assert.AreEqual(actual.PropNullInt.GetValueOrDefault(),obj.PropNullInt.GetValueOrDefault());

            Assert.AreEqual(actual.PropNullInt.HasValue, obj.PropNullInt.HasValue);
        }
    }

    public class NullPropClass
    {
        public int? PropNullInt { get; set; }
    }
}