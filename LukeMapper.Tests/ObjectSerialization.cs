using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LukeMapperTests
{
    [TestClass]
    public class ObjectSerialization
    {
        [TestMethod]
        public void DateTimeSerialization()
        {
            var date = DateTime.UtcNow;

            var longstring = LukeMapper.LukeMapper.ToDateString(date);

            long uttime;
            Assert.IsTrue(long.TryParse(longstring, out uttime));

            var ret = LukeMapper.LukeMapper.GetDateTime(longstring);

            AssertDatesEqual(date, ret);
        }

        private void AssertDatesEqual(DateTime a, DateTime b)
        {
            Assert.AreEqual(a.Year, b.Year);
            Assert.AreEqual(a.Month, b.Month);
            Assert.AreEqual(a.Day, b.Day);
            Assert.AreEqual(a.Hour, b.Hour);
            Assert.AreEqual(a.Minute, b.Minute);
            Assert.AreEqual(a.Second, b.Second);
        }

        [TestMethod]
        public void TrueSerialization()
        {
            var value = true;
            var serialized = value.ToString();
            var deserialized = LukeMapper.LukeMapper.GetBoolean(serialized);
            Assert.AreEqual(value,deserialized);
        }

        [TestMethod]
        public void FalseSerialization()
        {
            var value = true;
            var serialized = value.ToString();
            var deserialized = LukeMapper.LukeMapper.GetBoolean(serialized);
            Assert.AreEqual(value, deserialized);
        }
    }
}
