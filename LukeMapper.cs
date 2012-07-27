using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using SomeOtherNamespace;
using FieldInfo = Lucene.Net.Index.FieldInfo;

namespace LukeMapper
{
    public static class LukeMapper
    {
        #region QueryCaching

        /// <summary>
        /// Called if the query cache is purged via PurgeQueryCache
        /// </summary>
        public static event EventHandler QueryCachePurged;
        private static void OnQueryCachePurged()
        {
            var handler = QueryCachePurged;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        static readonly System.Collections.Concurrent.ConcurrentDictionary<Identity, CacheInfo> _queryCache = new System.Collections.Concurrent.ConcurrentDictionary<Identity, CacheInfo>();
        private static void SetQueryCache(Identity key, CacheInfo value)
        {
            if (Interlocked.Increment(ref collect) == COLLECT_PER_ITEMS)
            {
                CollectCacheGarbage();
            }
            _queryCache[key] = value;
        }

        private static void CollectCacheGarbage()
        {
            try
            {
                foreach (var pair in _queryCache)
                {
                    if (pair.Value.GetHitCount() <= COLLECT_HIT_COUNT_MIN)
                    {
                        CacheInfo cache;
                        _queryCache.TryRemove(pair.Key, out cache);
                    }
                }
            }

            finally
            {
                Interlocked.Exchange(ref collect, 0);
            }
        }

        private const int COLLECT_PER_ITEMS = 1000, COLLECT_HIT_COUNT_MIN = 0;
        private static int collect;
        private static bool TryGetQueryCache(Identity key, out CacheInfo value)
        {
            if (_queryCache.TryGetValue(key, out value))
            {
                value.RecordHit();
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Purge the query cache 
        /// </summary>
        public static void PurgeQueryCache()
        {
            _queryCache.Clear();
            OnQueryCachePurged();
        }


        class CacheInfo
        {
            public Func<Document, object> Deserializer { get; set; }
            public Func<Document, object>[] OtherDeserializers { get; set; }
            public Action<Document, object> ParamReader { get; set; }
            private int hitCount;
            public int GetHitCount() { return Interlocked.CompareExchange(ref hitCount, 0, 0); }
            public void RecordHit() { Interlocked.Increment(ref hitCount); }
        }
        private static CacheInfo GetCacheInfo(Identity identity)
        {
            CacheInfo info;
            if (!TryGetQueryCache(identity, out info))
            {
                info = new CacheInfo();
                SetQueryCache(identity, info);
            }
            return info;
        }

        #endregion

        #region Deserialization

        private const string LinqBinary = "System.Data.Linq.Binary";

        //private static Func<Document, object> GetDeserializer(Type type, Document document, int startBound, int length, bool returnNullIfFirstMissing)
        //{
        //    // dynamic is passed in as Object ... by c# design
        //    if (type == typeof(object) || type == typeof(FastExpando))
        //    {
        //        return GetDynamicDeserializer(document, startBound, length, returnNullIfFirstMissing);
        //    }
        //    Type underlyingType = null;
        //    if (
        //        !(typeMap.ContainsKey(type) || 
        //        type.IsEnum || 
        //        type.FullName == LinqBinary ||
        //        (
        //            type.IsValueType && (underlyingType = Nullable.GetUnderlyingType(type)) != null && underlyingType.IsEnum)))
        //    {
        //        return GetTypeDeserializer(type, document, startBound, length, returnNullIfFirstMissing);
        //    }
        //    return GetStructDeserializer(type, underlyingType ?? type, startBound);

        //}


        private static Func<Document, object> GetDumbDeserializer(Type type, IndexSearcher searcher, int startBound, int length, bool returnNullIfFirstMissing)
        {
            //debug only
            //var assemblyName = new AssemblyName("SomeName");
            //var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            //var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");

            //TypeBuilder builder = moduleBuilder.DefineType("Test", TypeAttributes.Public);
            //var dm = builder.DefineMethod(string.Format("Deserialize{0}", Guid.NewGuid()), MethodAttributes.Public, typeof(object), new[] { typeof(Document) });
            //debug only


            var dm = new DynamicMethod(string.Format("Deserialize{0}", Guid.NewGuid()), typeof(object), new[] { typeof(Document) }, true);

            var il = dm.GetILGenerator();
            //il.DeclareLocal(typeof(int));
            //il.DeclareLocal(type);
            //bool haveEnumLocal = false;
            //il.Emit(OpCodes.Ldc_I4_0);
            //il.Emit(OpCodes.Stloc_0);


            var properties = GetSettableProps(type);
            var fields = GetSettableFields(type);

            var names = searcher.GetIndexReader().GetFieldNames(IndexReader.FieldOption.ALL);

            var setters = (
                            from n in names
                            let prop = properties.FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.Ordinal)) // property case sensitive first
                                  ?? properties.FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase))// property case insensitive second
                                  ?? properties.FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase)) // property case insensitive without "_" third
                            let field = prop != null ? null : (fields.FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.Ordinal)) // field case sensitive fourth
                                ?? fields.FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase))// field case insensitive fifth
                                ?? fields.FirstOrDefault(p => string.Equals(p.Name, n, StringComparison.OrdinalIgnoreCase))) // field case insensitive without "_" sixth
                            select new { Name = n, Property = prop, Field = field }
                          ).ToList();

            
            var ctor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (ctor == null)
            {
                throw new InvalidOperationException("A parameterless default constructor is required to allow for dapper materialization");
            }
            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Stloc_0);


            //il.BeginExceptionBlock();

            Label returnLabel = il.DefineLabel();

            //stack is [target]
            
            var getFieldValue = typeof (Document).GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);

            foreach (var setter in setters)
            {
                il.Emit(OpCodes.Ldloc_0);// [target]

                il.Emit(OpCodes.Ldarg_0);

                il.Emit(OpCodes.Ldstr, setter.Name);
                il.Emit(OpCodes.Callvirt, getFieldValue);

                if(setter.Field != null)
                {
                    il.Emit(OpCodes.Stfld, setter.Field);
                }

                if(setter.Property != null)
                {
                    il.Emit(OpCodes.Callvirt, setter.Property.Setter); // stack is now [target]
                }
            }

            il.Emit(OpCodes.Nop);
            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Stloc_1); // stack is empty

            //il.BeginCatchBlock(typeof(Exception)); // stack is Exception
            //il.Emit(OpCodes.Ldloc_0); // stack is Exception, index
            //il.Emit(OpCodes.Ldarg_0); // stack is Exception, index, reader
            //il.EmitCall(OpCodes.Call, typeof(LukeMapper).GetMethod("ThrowDataException"), null);
            //il.EndExceptionBlock();
            il.Emit(OpCodes.Br_S, returnLabel);
            il.MarkLabel(returnLabel);
            il.Emit(OpCodes.Ldloc_1); // stack is [rval]
            il.Emit(OpCodes.Ret);

            //debug only
            //var t = builder.CreateType();
            //assemblyBuilder.Save(assemblyName.Name + ".dll");
            //debug only


            return (Func<Document, object>)dm.CreateDelegate(typeof(Func<Document, object>));
            //return null;
        }


        /// <summary>
        /// Throws a data exception, only used internally
        /// </summary>
        public static void ThrowDataException(Exception ex, string field, Document document)
        {
            if (document != null && document.GetField(field) != null)
            {
                throw new DataException(string.Format("Error parsing Field {0} (\"{1}\")", field, document.GetField(field).StringValue()),ex);    
            }
            else if(document == null)
            {
                throw new DataException("Document is null", ex);    
            }
            else
            {
                throw new DataException(string.Format("Error parsing Field {0} ([null])", field), ex);    
            }
        }

        //private static Func<Document, object> GetDynamicDeserializer(Document document, int startBound, int length, bool returnNullIfFirstMissing)
        //{
        //    var fields = document.GetFields();
        //    int fieldCount = fields.Count;
        //    if (length == -1)
        //    {
        //        length = fieldCount - startBound; //LMR: what is startbound used for?
        //    }

        //    if (fieldCount <= startBound)
        //    {
        //        throw new ArgumentException("When using the multi-mapping APIs ensure you set the splitOn param if you have keys other than Id", "splitOn");
        //    }

        //    return
        //         r =>
        //         {
        //             IDictionary<string, object> row = new Dictionary<string, object>(length);
        //             var flds = document.GetFields();
        //             foreach (Field field in flds)
        //             {
        //                 row[field.Name()] = field.StringValue();
        //             }

        //             //we know this is an object so it will not box
        //             return FastExpando.Attach(row);
        //         };
        //}


//        private static Func<Document, object> GetTypeDeserializer(
//            Type type, 
//            Document document,
//            int startBound = 0, 
//            int length = -1, 
//            bool returnNullIfFirstMissing = false)
//        {
//            var dm = new DynamicMethod(
//                string.Format("Deserialize{0}", Guid.NewGuid()), 
//                typeof(object), 
//                new[] { typeof(Document) }, 
//                true);

//            //TODO: emit IL code

//            return (Func<Document, object>)dm.CreateDelegate(typeof(Func<Document, object>));
//        }

//        private static Func<Document, object> GetStructDeserializer(Type type, Type effectiveType, int index)
//        {
//            // no point using special per-type handling here; it boils down to the same, plus not all are supported anyway (see: SqlDataReader.GetChar - not supported!)
//#pragma warning disable 618
//            if (type == typeof(char))
//            { // this *does* need special handling, though
//                return r => ReadChar(r.Get(index));
//            }
//            if (type == typeof(char?))
//            {
//                return r => ReadNullableChar(r.Get(index));
//            }
//            if (type.FullName == LinqBinary)
//            {
//                return r => Activator.CreateInstance(type, r.Get(index));
//            }
//#pragma warning restore 618

//            if (effectiveType.IsEnum)
//            {   // assume the value is returned as the correct type (int/byte/etc), but box back to the typed enum
//                return r =>
//                {
//                    var val = r.Get(index);
//                    return val is DBNull ? null : Enum.ToObject(effectiveType, val);
//                };
//            }
//            return r =>
//            {
//                var val = r.Get(index);
//                return val is DBNull ? null : val;
//            };
//        }
        



        /// <summary>
        /// Internal use only
        /// </summary>
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("This method is for internal usage only", false)]
        public static char ReadChar(object value)
        {
            if (value == null || value is DBNull) throw new ArgumentNullException("value");
            string s = value as string;
            if (s == null || s.Length != 1) throw new ArgumentException("A single-character was expected", "value");
            return s[0];
        }

        /// <summary>
        /// Internal use only
        /// </summary>
        [Browsable(false), EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("This method is for internal usage only", false)]
        public static char? ReadNullableChar(object value)
        {
            if (value == null || value is DBNull) return null;
            string s = value as string;
            if (s == null || s.Length != 1) throw new ArgumentException("A single-character was expected", "value");
            return s[0];
        }


        class PropInfo
        {
            public string Name { get; set; }
            public MethodInfo Setter { get; set; }
            public Type Type { get; set; }
        }

        static List<PropInfo> GetSettableProps(Type t)
        {
            return t
                  .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                  .Select(p => new PropInfo
                  {
                      Name = p.Name,
                      Setter = p.DeclaringType == t ?
                        p.GetSetMethod(true) :
                        p.DeclaringType.GetProperty(p.Name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetSetMethod(true),
                      Type = p.PropertyType
                  })
                  .Where(info => info.Setter != null)
                  .ToList();
        }

        static List<System.Reflection.FieldInfo> GetSettableFields(Type t)
        {
            return t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).ToList();
        }

        #endregion


        public static IEnumerable<T> Query<T>(
            this IndexSearcher searcher, 
            Query query, 
            int n/*, Sort sort*/)
        {
            var identity = new Identity(searcher,query,typeof(T));
            var info = GetCacheInfo(identity);


            //****: create lambda to generate deserializer method, then cache it
            //****: we do this here in case the underlying schema has changed we can regenerate...
            TopDocs td = searcher.Search(query, n);

            if (td.TotalHits == 0)
            {
                yield break;
            }
            //var firstDocument = searcher.Doc(td.ScoreDocs[0].doc);

            //LMR: could potentially make this a (document)=>func(document,object) instead for the try catch statement below
            Func<Func<Document, object>> cacheDeserializer = () =>
                    {
                        info.Deserializer = GetDumbDeserializer(typeof(T), searcher, 0, -1, false);
                        SetQueryCache(identity, info);
                        return info.Deserializer;
                    };

            //****: check info for deserializer, if null => run it.

            if (info.Deserializer == null)
            {
                cacheDeserializer();
            }

            //yield break;

            var deserializer = info.Deserializer;

            foreach(var document in td.ScoreDocs.Select(sd=>searcher.Doc(sd.doc)))
            {
                object next;
                try
                {
                next = deserializer(document);
                }
                catch (DataException)
                {
                    // give it another shot, in case the underlying schema changed
                    deserializer = cacheDeserializer();
                    next = deserializer(document);
                }
                yield return (T)next;
            }
        }

        public static IEnumerable<dynamic> Query(this IndexSearcher searcher, Query query)
        {
            return new List<dynamic>();
        }
    }
}