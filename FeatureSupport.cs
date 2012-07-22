using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace LukeMapper
{
    /// <summary>
    /// Handles variances in features per DBMS
    /// </summary>
    public class FeatureSupport
    {
        /// <summary>
        /// Dictionary of supported features index by connection type name
        /// </summary>
        private static readonly Dictionary<string, FeatureSupport> FeatureList = new Dictionary<string, FeatureSupport>() {
                                                                                                                              {"sqlserverconnection", new FeatureSupport { Arrays = false}},
                                                                                                                              {"npgsqlconnection", new FeatureSupport {Arrays = true}}
                                                                                                                          };

        /// <summary>
        /// Gets the featureset based on the passed connection
        /// </summary>
        public static FeatureSupport Get(IDbConnection connection)
        {
            string name = connection.GetType().Name.ToLower();
            FeatureSupport features;
            return FeatureList.TryGetValue(name, out features) ? features : FeatureList.Values.First();
        }

        /// <summary>
        /// True if the db supports array columns e.g. Postgresql
        /// </summary>
        public bool Arrays { get; set; }
    }
}