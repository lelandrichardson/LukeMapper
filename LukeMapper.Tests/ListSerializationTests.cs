using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Index;
using Lucene.Net.Search;
using LukeMapper;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LukeMapperTests
{
    [TestClass]
    public class ListSerializationTests
    {
        private const string Index = "test-index";

        [TestInitialize]
        public void Init()
        {
            // clear index
            IndexManager.Of(Index).DeleteAll();
        }

        [TestMethod]
        public void WriteDelimited()
        {
            var expected = new TestListClass
                {
                    Int = 222,
                    //IntList = new List<int> {1, 2, 3, 4, 5, 11231241},
                    PropIntList = new List<int> {1, 2, 3, 4, 5, 6, 36254564},
                    //StringList = new List<string> {"abc", "def", "ghiqwdqwd,qwd"},
                    PropStringList = new List<string> {"abc", "def", "ghawdawd,aw,daw,dawd,awd,awd"}
                };

            IndexManager.Of(Index).Write(new List<TestListClass> { expected });

            var query = new TermQuery(new Term("Int", expected.Int.ToString()));
            var actual = IndexManager.Of(Index).Query<TestListClass>(query, 1).SingleOrDefault();

            Assert.IsNotNull(actual, "Returns only one object");

            Assert.AreEqual(actual.Int, expected.Int);

            //AssertListsEqual(expected.IntList, actual.IntList);
            AssertListsEqual(expected.PropIntList, actual.PropIntList);
            //AssertListsEqual(expected.StringList, actual.StringList);
            AssertListsEqual(expected.PropStringList, actual.PropStringList);
        }

        private void AssertListsEqual<T>(List<T> expected, List<T> actual )
        {
            Assert.AreEqual(expected.Count, actual.Count);
            if (expected.Count != actual.Count) return;
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i],actual[i]);
            }
        }

        [TestMethod]
        public void GetGenericMethodTests()
        {
            Assert.IsNotNull(
                typeof(string)
                         .GetMethods()
                         .Where(m => m.Name == "Join")
                         .Select(m => new {
                                              Method = m,
                                              Params = m.GetParameters(),
                                              Args = m.GetGenericArguments()
                                          })
                         .Where(x => x.Params.Length == 2
                                     && x.Args.Length == 1)
                         .Select(x => x.Method)
                         .First()
                );

            var ctors = typeof (Func<,>).MakeGenericType(new[]{typeof(string),typeof(int)})
                .GetConstructors();

            Assert.IsNotNull(
                ctors
                );

            //var selectMethods = typeof(Enumerable)
            //    .GetMethods()
            //    .Where(m => m.Name == "Select")
            //    .Select(m=>m.MakeGenericMethod(new[] { typeof(string), typeof(int) }));

            MethodInfo selMeth;
            ParameterInfo[] parameter;
            LukeMapper.LukeMapper.FindMethod(typeof(Enumerable), "ToList", new[] { typeof(int) },
                                             new[] { typeof(IEnumerable<>) }, out selMeth, out parameter);

            LukeMapper.LukeMapper.FindMethod(typeof (Enumerable), "Select", new[] {typeof (string), typeof (int)},
                                             new[] {typeof (IEnumerable<>), typeof (Func<,>)}, out selMeth, out parameter);

            Assert.IsNotNull(selMeth);

        }
    }

    public class TestListClass
    {
        public int Int;

        //[LukeDelimited('\t')]
        //public List<string> StringList; 

        [LukeDelimited(",")]
        public List<string> PropStringList { get; set; }

        //[LukeDelimited(',')]
        //public List<int> IntList;

        [LukeDelimited(",")]
        public List<int> PropIntList { get; set; }
 

    }



}
