using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Index;
using Lucene.Net.Search;
using LukeMapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LukeMapperTests
{
    [TestClass]
    public class WriteTests
    {
        private const string Index = "test-index";

        [TestInitialize]
        public void Init()
        {
            // clear index
            IndexManager.Of(Index).DeleteAll();
        }

        [TestMethod]
        public void WriteNoAttributes()
        {
            var expected = new TestWriteClass
                {
                    Int = 23,
                    PropInt = 32,
                    NullInt = 22,
                    PropNullInt = 33,
                    String = "testing 123",
                    PropString = "testing yet again",
                    Char = 'a',
                    PropChar = 'b',
                    //NullChar = 'c',
                    //PropNullChar = 'd',
                    Dt = DateTime.UtcNow.AddDays(-31),
                    PropDt = DateTime.UtcNow.AddYears(-2).AddDays(-7),
                    NullDt = DateTime.UtcNow.AddDays(-31),
                    PropNullDt = DateTime.UtcNow.AddHours(-22),
                    Bl = true,
                    PropBl = true,
                    PropNullBl = true,
                    NullBl = true
                };

            IndexManager.Of(Index).Write(new List<TestWriteClass>{expected});

            var query = new TermQuery(new Term("Int", expected.Int.ToString()));
            var actual = IndexManager.Of(Index).Query<TestWriteClass>(query, 1).SingleOrDefault();

            Assert.IsNotNull(actual, "Returns only one object");

            Assert.AreEqual(actual.Int, expected.Int);
            Assert.AreEqual(actual.PropInt, expected.PropInt);
            Assert.AreEqual(actual.NullInt.GetValueOrDefault(), expected.NullInt.GetValueOrDefault());
            Assert.AreEqual(actual.PropNullInt.GetValueOrDefault(), expected.PropNullInt.GetValueOrDefault());
            Assert.AreEqual(actual.NullInt.HasValue, expected.NullInt.HasValue);
            Assert.AreEqual(actual.PropNullInt.HasValue, expected.PropNullInt.HasValue);
            Assert.AreEqual(actual.PropString, expected.PropString);
            Assert.AreEqual(actual.String, expected.String);

            Assert.AreEqual(actual.NullDt.HasValue, expected.NullDt.HasValue);
            Assert.AreEqual(actual.PropNullDt.HasValue, expected.PropNullDt.HasValue);

            Assert.AreEqual(actual.PropNullDt.Value.Day, expected.PropNullDt.Value.Day);


        }

        [TestMethod]
        public void WriteWithCustomSerializer()
        {

            IndexManager.Of(Index).DeleteAll();

            var expected = new TestCustomSerializerClass
                {
                    Id = 23,
                    CustomList = new List<string> {"abc", "def", "ghi"}
                };

            IndexManager.Of(Index).Write(new List<TestCustomSerializerClass> { expected });

            var query = new TermQuery(new Term("Id", expected.Id.ToString()));
            var actual = IndexManager.Of(Index).Query<TestCustomSerializerClass>(query, 1).SingleOrDefault();

            Assert.IsNotNull(actual, "Returns only one object");
            
            Assert.IsNotNull(actual.CustomList);

            Assert.AreEqual(expected.CustomList.Count, actual.CustomList.Count);

        }
    }

    public class TestCustomSerializerClass
    {
        public int Id { get; set; }


        public List<string> CustomList { get; set; } 

        [LukeSerializer("CustomList")]
        public static string CustomListToString(List<string> list)
        {
            return string.Join(",", list);
        }
        [LukeDeserializer("CustomList")]
        public static List<string> StringToCustomList(string serialized)
        {
            return serialized.Split(',').ToList();
        }
    }

    public class TestWriteClass
    {
        public int Int;
        public int PropInt { get; set; }

        public int? NullInt;
        public int? PropNullInt { get; set; }

        [Luke(FieldName = "_Different")]
        public string String;
        public string PropString { get; set; }

        public char Char;
        public char PropChar { get; set; }

        //public char? NullChar;
        //public char? PropNullChar { get; set; }

        public DateTime Dt;
        public DateTime PropDt { get; set; }

        public bool Bl;
        public bool PropBl { get; set; }

        public bool? NullBl;
        public bool? PropNullBl { get; set; }

        public DateTime? NullDt;
        public DateTime? PropNullDt { get; set; }
    }
}
