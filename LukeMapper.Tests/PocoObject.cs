using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Documents;

namespace LukeMapperTests
{
    public class PocoObject
    {

        // creates a generally random PocoObject, with specified id
        public static PocoObject Random(int id)
        {
            var random = new Random();

            return new PocoObject
                {
                    Id = id,
                    PropId = random.Next(),
                    NullId = random.Next(2) == 1 ? (int?)null : (int?) random.Next(20),
                    PropNullId = random.Next(2) == 1 ? (int?)null : (int?)random.Next(20),

                    Long = id,
                    PropLong = random.Next(),
                    NullLong = random.Next(2) == 1 ? (long?)null : (long?)random.Next(20),
                    PropNullLong = random.Next(2) == 1 ? (long?)null : (long?)random.Next(20),

                    DisplayText = Path.GetRandomFileName(),
                    PropDisplayText = Path.GetRandomFileName(),

                    Ch = (char) random.Next(65, 122),
                    PropCh = (char) random.Next(65, 122),

                    Bl = random.Next(2) == 1,
                    PropBl = random.Next(2) == 1,
                    NullBl = random.Next(2) == 1 ? random.Next(2) == 1 : (bool?)null,
                    PropNullBl = random.Next(2) == 1 ? random.Next(2) == 1 : (bool?)null,

                    Dt = DateTime.UtcNow.AddSeconds(-1*random.Next(999999)),
                    PropDt = DateTime.UtcNow.AddSeconds(-1*random.Next(999999)),
                    NullDt = random.Next(2) == 1 ? (DateTime?) DateTime.UtcNow.AddSeconds(-1*random.Next(999999)) : null,
                    NullPropDt =
                        random.Next(2) == 1 ? (DateTime?) DateTime.UtcNow.AddSeconds(-1*random.Next(999999)) : null
                };
        }

        public int Id;
        public int PropId { get; set; }

        public int? NullId;
        public int? PropNullId { get; set; }

        public long Long;
        public long PropLong { get; set; }

        public long? NullLong;
        public long? PropNullLong { get; set; }

        public string DisplayText;
        public string PropDisplayText { get; set; }

        public char Ch;
        public char PropCh { get; set; }

        public bool Bl;
        public bool PropBl { get; set; }

        public bool? NullBl;
        public bool? PropNullBl { get; set; }

        public DateTime Dt;
        public DateTime PropDt { get; set; }

        public DateTime? NullDt;
        public DateTime? NullPropDt { get; set; }

    }
}
