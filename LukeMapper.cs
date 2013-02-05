using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using System.Collections.Concurrent;

namespace LukeMapper
{
    public static class LukeMapper
    {
        #region Constants + Defaults

        internal const Store DefaultStore = Store.YES;
        internal const Index DefaultIndex = Index.NOT_ANALYZED_NO_NORMS;

        private static readonly List<string> SupportedTypes = new List<string>
            {
                "System.String",
                "System.Int32",
                "System.Int64",
                "System.Boolean",
                "System.DateTime",
                "System.Char"
            };

        #endregion

        #region Cached Reflection References

        //Cached references to useful mappings
        private static readonly MethodInfo GetFieldValue = typeof(Document).GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
        private static readonly MethodInfo IntTryParse = typeof(Int32).GetMethod("TryParse", new[] { typeof(string), typeof(int).MakeByRefType() });
        private static readonly MethodInfo IntParse = typeof(Int32).GetMethod("Parse", new[] { typeof(string)});
        private static readonly MethodInfo LongTryParse = typeof(Int64).GetMethod("TryParse", new[] { typeof(string), typeof(long).MakeByRefType() });
        private static readonly MethodInfo IsNullOrEmpty = typeof(String).GetMethod("IsNullOrEmpty", new[] { typeof(string) });
        private static readonly MethodInfo LukeMapperGetDateTime = typeof(LukeMapper).GetMethod("GetDateTime");
        private static readonly MethodInfo LukeMapperGetBoolean = typeof(LukeMapper).GetMethod("GetBoolean");
        private static readonly MethodInfo StringGetChars = typeof(String).GetMethod("get_Chars");
        private static readonly MethodInfo StringSplit = typeof(string).GetMethod("Split", new[] { typeof(string[]), typeof(StringSplitOptions) });

        //Cached references useful for writes
        private static readonly Type DocumentType = typeof(Document);
        private static readonly ConstructorInfo DocumentCtor = typeof(Document).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
        private static readonly ConstructorInfo FieldCtor = typeof(Field).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string), typeof(string), typeof(Field.Store), typeof(Field.Index) }, null);
        private static readonly MethodInfo IntToString = typeof(Int32).GetMethod("ToString", Type.EmptyTypes);
        private static readonly MethodInfo ObjectToString = typeof(Object).GetMethod("ToString", Type.EmptyTypes);
        private static readonly MethodInfo LongToString = typeof(Int64).GetMethod("ToString", Type.EmptyTypes);
        private static readonly MethodInfo DocumentAddField = typeof(Document).GetMethod("Add", new[] { typeof(Fieldable) });
        private static readonly MethodInfo LukeMapperToDateString = typeof(LukeMapper).GetMethod("ToDateString");
        private static readonly MethodInfo StringGenericJoin = typeof (string)
            .GetMethods()
            .Where(m => m.Name == "Join")
            .Select(m => new
                {
                    Method = m,
                    Params = m.GetParameters(),
                    Args = m.GetGenericArguments()
                })
            .Where(x => x.Params.Length == 2
                        && x.Args.Length == 1)
            .Select(x => x.Method)
            .First();
        #endregion

        #region Query and Write Caching

        /// <summary>
        /// Called if the query cache is purged via PurgeQueryCache
        /// </summary>
        public static event EventHandler QueryCachePurged;
        private static void OnQueryCachePurged()
        {
            var handler = QueryCachePurged;
            if (handler != null) handler(null, EventArgs.Empty);
        }

        static readonly ConcurrentDictionary<Identity, DeserializerCacheInfo> _queryCache = new ConcurrentDictionary<Identity, DeserializerCacheInfo>();
        static readonly ConcurrentDictionary<Identity, object> _writeCache = new ConcurrentDictionary<Identity, object>();
        private static void SetQueryCache(Identity key, DeserializerCacheInfo value)
        {
            if (Interlocked.Increment(ref collect) == COLLECT_PER_ITEMS)
            {
                CollectCacheGarbage();
            }
            _queryCache[key] = value;
        }
        private static void SetWriteCache<T>(Identity key, SerializerCacheInfo<T> value)
        {
            if (Interlocked.Increment(ref collect) == COLLECT_PER_ITEMS)
            {
                CollectCacheGarbage();
            }
            _writeCache[key] = value;
        }

        private static void CollectCacheGarbage()
        {
            //TODO: add write cache here
            try
            {
                foreach (var pair in _queryCache)
                {
                    if (pair.Value.GetHitCount() <= COLLECT_HIT_COUNT_MIN)
                    {
                        DeserializerCacheInfo cache;
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

        private static bool TryGetQueryCache(Identity key, out DeserializerCacheInfo value)
        {
            if (_queryCache.TryGetValue(key, out value))
            {
                value.RecordHit();
                return true;
            }
            value = null;
            return false;
        }

        private static bool TryGetWriteCache<T>(Identity key, out SerializerCacheInfo<T> value)
        {
            object uncasted;
            if (_writeCache.TryGetValue(key, out uncasted))
            {
                value = (SerializerCacheInfo<T>)uncasted;
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
            //TODO: do for write cache as well
            _queryCache.Clear();
            OnQueryCachePurged();
        }


        class DeserializerCacheInfo
        {
            public Func<Document, object> Deserializer { get; set; }
            private int hitCount;
            public int GetHitCount() { return Interlocked.CompareExchange(ref hitCount, 0, 0); }
            public void RecordHit() { Interlocked.Increment(ref hitCount); }
        }

        class SerializerCacheInfo<T>
        {
            public Func<T, Document> Serializer { get; set; }
            private int hitCount;
            public int GetHitCount() { return Interlocked.CompareExchange(ref hitCount, 0, 0); }
            public void RecordHit() { Interlocked.Increment(ref hitCount); }
        }

        private static DeserializerCacheInfo GetDeserializerCacheInfo(Identity identity)
        {
            DeserializerCacheInfo info;
            if (!TryGetQueryCache(identity, out info))
            {
                info = new DeserializerCacheInfo();
                SetQueryCache(identity, info);
            }
            return info;
        }

        private static SerializerCacheInfo<T> GetSerializerCacheInfo<T>(Identity identity)
        {
            SerializerCacheInfo<T> info;
            if (!TryGetWriteCache(identity, out info))
            {
                info = new SerializerCacheInfo<T>();
                SetWriteCache(identity, info);
            }
            return info;
        }

        #endregion

        #region Deserialization

        private static Func<Document, object> GetDeserializer(Type type, IndexSearcher searcher)
        {
            // dynamic is passed in as Object ... by c# design
            if (type == typeof(object) || type == typeof(FastExpando))
            {
                return GetDynamicDeserializer(searcher);
            }
            return GetTypeDeserializer(type, searcher);
            //return GetStructDeserializer(type, underlyingType ?? type, startBound);
        }

        /// <summary>
        /// This deserializes into a dynamic object (essentially a dictionary)
        /// This is much less useful than in the equivalent dynamic object for RDBMS querying, 
        /// since Lucene stores everything as strings.
        /// 
        /// I've simply kept this here as an API since I think it has a convenient and clean syntax
        /// over Lucene's document.Get("") etc.
        /// </summary>
        private static Func<Document, object> GetDynamicDeserializer(IndexSearcher searcher)
        {
            var names = searcher.GetIndexReader().GetFieldNames(IndexReader.FieldOption.ALL).ToList();
            var fieldCount = names.Count;

            return
                d =>
                {
                    IDictionary<string, object> row = new Dictionary<string, object>(fieldCount);
                    foreach (var name in names)
                    {
                        var tmp = d.Get(name);
                        if(!string.IsNullOrEmpty(tmp))
                        {
                            row[name] = tmp;
                        }
                    }
                    //we know this is an object so it will not box
                    return FastExpando.Attach(row);
                };
        }

        /// <summary>
        /// Here is where most of the magic happens.
        /// 
        /// Given an IndexSearcher and a Type to map the index to, it will create and return a
        /// function mapping a document to the specified Type
        /// </summary>
        /// <param name="type">Type to return</param>
        /// <param name="searcher">IndexSearcher containing the serialized data</param>
        /// <returns></returns>
        private static Func<Document, object> GetTypeDeserializer(Type type, IndexSearcher searcher)
        {
            //debug only
            //var assemblyName = new AssemblyName("SomeName");
            //var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            //var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");

            //TypeBuilder builder = moduleBuilder.DefineType("Test", TypeAttributes.Public);
            //var dm = builder.DefineMethod(string.Format("Deserialize{0}", Guid.NewGuid()), MethodAttributes.Public, typeof(object), new[] { typeof(Document) });
            //debug only


            var dm = new DynamicMethod(string.Format("Deserialize{0}", Guid.NewGuid()), type, new[] { typeof(Document) }, true);

            var il = dm.GetILGenerator();

            var classAttr = type.GetCustomAttributes(typeof(LukeMapperAttribute), true).FirstOrDefault() as LukeMapperAttribute ??
                new LukeMapperAttribute();

            var properties = GetSettableProps(type);
            var fields = GetSettableFields(type);

            var names = searcher.GetIndexReader().GetFieldNames(IndexReader.FieldOption.ALL);

            var ctor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (ctor == null)
            {
                throw new InvalidOperationException("A parameterless default constructor is required to allow for LukeMapper materialization");
            }
            il.DeclareLocal(type);
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Stloc_0);

            var deserializers =
                type.GetMethods()
                    .Select(method => new
                    {
                        Method = method,
                        DeserializerAttribute = (LukeDeserializerAttribute)method.GetCustomAttributes(typeof(LukeDeserializerAttribute), true).FirstOrDefault()
                    })
                    .Where(s => s.DeserializerAttribute != null)
                    .ToList();

            foreach (var property in properties)
            {
                var lukeAttr =
                        property.PropertyInfo.GetCustomAttributes(typeof(LukeAttribute), true).FirstOrDefault() as LukeAttribute;

                string luceneFieldName = null;

                if (lukeAttr != null && lukeAttr.FieldName != null)
                {
                    luceneFieldName = lukeAttr.FieldName;
                }
                else
                {
                    luceneFieldName = names.FirstOrDefault(s => s.Equals(property.Name, StringComparison.Ordinal)) ??
                                      names.FirstOrDefault(s => s.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
                }
                
                if (luceneFieldName == null)
                {
                    continue; // field does not exist in lucene index
                }

                var customSerializer = deserializers.FirstOrDefault(ser => ser.DeserializerAttribute.FieldName == property.Name);
                if (customSerializer != null)
                {
                    EmitCustomDeserializedTypeProp(il, property, customSerializer.Method, classAttr);
                }
                else
                {

                    var memberType = property.Type;

                    var nullableType = Nullable.GetUnderlyingType(memberType);

                    if (nullableType != null)
                    {
                        var localString = il.DeclareLocal(typeof(string));

                        var breakoutLabel = il.DefineLabel();

                        il.Emit(OpCodes.Ldarg_0); // [document]
                        il.Emit(OpCodes.Ldstr, luceneFieldName); // [document] [field name]
                        il.Emit(OpCodes.Callvirt, GetFieldValue); // get field value. //stack is now [value]

                        il.Emit(OpCodes.Stloc, localString);
                        il.Emit(OpCodes.Ldloc, localString);
                        il.Emit(OpCodes.Call, IsNullOrEmpty); // value is not set if true

                        il.Emit(OpCodes.Brtrue_S, breakoutLabel);


                        EmitNullType(il, nullableType, localString, breakoutLabel);

                        var nullCtor = memberType.GetConstructor(new[] { nullableType });
                        il.Emit(OpCodes.Newobj, nullCtor);
                        il.Emit(OpCodes.Callvirt, property.Setter);


                        il.MarkLabel(breakoutLabel);
                    }
                    else
                    {
                        EmitProp(il, luceneFieldName, property);
                    }
                }

            }

            foreach (var field in fields)
            {
                var lukeAttr =
                        field.GetCustomAttributes(typeof(LukeAttribute), true).FirstOrDefault() as LukeAttribute;

                string luceneFieldName = null;

                if (lukeAttr != null && lukeAttr.FieldName != null)
                {
                    luceneFieldName = lukeAttr.FieldName;
                }
                else
                {
                    luceneFieldName = names.FirstOrDefault(s => s.Equals(field.Name, StringComparison.Ordinal)) ??
                                      names.FirstOrDefault(s => s.Equals(field.Name, StringComparison.OrdinalIgnoreCase));
                }

                if (luceneFieldName == null)
                {
                    continue; // field does not exist in lucene index
                }

                var memberType = field.FieldType;

                var nullableType = Nullable.GetUnderlyingType(memberType);

                if (nullableType != null)
                {
                    var localString = il.DeclareLocal(typeof(string));

                    var breakoutLabel = il.DefineLabel();

                    il.Emit(OpCodes.Ldarg_0); // [document]
                    il.Emit(OpCodes.Ldstr, luceneFieldName); // [document] [field name]
                    il.Emit(OpCodes.Callvirt, GetFieldValue); // get field value. //stack is now [value]

                    il.Emit(OpCodes.Stloc, localString);
                    il.Emit(OpCodes.Ldloc, localString);
                    il.Emit(OpCodes.Call, IsNullOrEmpty); // value is not set if true

                    il.Emit(OpCodes.Brtrue_S, breakoutLabel);


                    EmitNullType(il, nullableType, localString, breakoutLabel);

                    var nullCtor = memberType.GetConstructor(new[] { nullableType });
                    il.Emit(OpCodes.Newobj, nullCtor);
                    il.Emit(OpCodes.Stfld, field);


                    il.MarkLabel(breakoutLabel);
                }
                else
                {
                    EmitField(il, luceneFieldName, field);
                }
            }

            il.Emit(OpCodes.Ldloc_0); // stack is [rval]
            il.Emit(OpCodes.Ret);

            //debug only
            //var t = builder.CreateType();
            //assemblyBuilder.Save(assemblyName.Name + ".dll");
            //debug only


            return (Func<Document, object>)dm.CreateDelegate(typeof(Func<Document, object>));
            //return null;
        }

        private static void EmitDelimitedPropOrField(ILGenerator il, LukeDelimitedAttribute delimitedAttr, LukeMapperAttribute classAttr, PropInfo prop = null, System.Reflection.FieldInfo field = null)
        {
            #region Validate Arguments

            var type = prop != null ? prop.PropertyInfo.PropertyType : field != null ? field.FieldType : null;
            if (type == null)
            {
                // no prop OR null specified
                throw new Exception("argument 'prop' or 'field' is missing");
            }


            var enumerableType = type.GetInterfaces()
                                     .Where(t => t.IsGenericType)
                                     .FirstOrDefault(t => t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerableType == null)
            {
                // needs to implement IEnumerable<T>;
                throw new Exception("The `LukeDelimited` attribute must be applied to a field or property which implements `IEnumerable<T>`");
            }

            var innerType = enumerableType.GetGenericArguments()[0];

            if (!SupportedTypes.Contains(innerType.FullName))
            {

                // innerType is not an allowed 
                throw new Exception(string.Format("The type `{0}` is not supported for Delimited lists. please use a `CustomDeserializer` method instead.", innerType.FullName));
            }

            //get LukeAttribute
            LukeAttribute lukeAttr;
            if (prop != null)
            {
                lukeAttr =
                    prop.PropertyInfo.GetCustomAttributes(typeof(LukeAttribute), true).FirstOrDefault() as
                    LukeAttribute;
            }
            else
            {
                lukeAttr =
                    field.GetCustomAttributes(typeof(LukeAttribute), true).FirstOrDefault() as
                    LukeAttribute;
            }
            if ((lukeAttr != null && lukeAttr.Ignore) || (classAttr.IgnoreByDefault && lukeAttr == null))
            {
                // field/prop should be ignored
                return;
            }

            #endregion

            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, lukeAttr != null && lukeAttr.FieldName != null ? lukeAttr.FieldName : prop.Name);
            il.Emit(OpCodes.Callvirt, GetFieldValue);

            var local = il.DeclareLocal(typeof (string[]));

            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Newarr, typeof (string));

            il.Emit(OpCodes.Stloc_S, local);
            il.Emit(OpCodes.Ldloc_S, local);
            il.Emit(OpCodes.Ldc_I4_0);

            il.Emit(OpCodes.Ldstr, delimitedAttr.Delimeter);

            il.Emit(OpCodes.Stelem_Ref);
            il.Emit(OpCodes.Ldloc_S, local);
            
            il.Emit(OpCodes.Ldc_I4_0); // this is StringSplitOptions.None i believe

            il.Emit(OpCodes.Callvirt, StringSplit);

            il.Emit(OpCodes.Ldnull);

            il.Emit(OpCodes.Ldftn, IntParse);

            var ctor =
                typeof (Func<,>)
                    .MakeGenericType(new[] { typeof (string), innerType })
                    .GetConstructor(new[] { typeof(object), typeof(IntPtr) });
            if (ctor == null)
            {
                throw new Exception("Cannot find the proper constructor");
            }

            il.Emit(OpCodes.Newobj, ctor);
            
            //TODO: call tolist etc...
            
            if (prop != null)
            {
                il.Emit(OpCodes.Callvirt, prop.Setter);
            }
            else
            {
                il.Emit(OpCodes.Stfld, field);
            }
        }

        /// <summary>
        /// Emits an Nullable type T?
        /// </summary>
        /// <param name="il">IL Generator</param>
        /// <param name="type">The Non-Nullable type to wrap with the Nullable interface</param>
        /// <param name="stringValue">The serialized string value</param>
        /// <param name="breakoutLabel"></param>
        private static void EmitNullType(ILGenerator il, Type type, LocalBuilder stringValue, Label breakoutLabel)
        {
            switch (type.FullName)
            {
                case "System.DateTime":
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldloc_S, stringValue);
                    il.Emit(OpCodes.Call, LukeMapperGetDateTime);
                    break;

                case "System.Int32":
                    var lb = il.DeclareLocal(typeof(int)); // temp int

                    il.Emit(OpCodes.Ldloc_S, stringValue);
                    il.Emit(OpCodes.Ldloca_S, lb);
                    il.Emit(OpCodes.Call, IntTryParse);

                    il.Emit(OpCodes.Brfalse_S, breakoutLabel);

                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldloc_S, lb);
                    break;

                case "System.Int64":
                    var lb64 = il.DeclareLocal(typeof(long)); // temp int

                    il.Emit(OpCodes.Ldloc_S, stringValue);
                    il.Emit(OpCodes.Ldloca_S, lb64);
                    il.Emit(OpCodes.Call, LongTryParse);

                    il.Emit(OpCodes.Brfalse_S, breakoutLabel);

                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldloc_S, lb64);
                    break;

                case "System.Boolean":
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldloc_S, stringValue);
                    il.Emit(OpCodes.Call, LukeMapperGetBoolean);
                    break;

                case "System.Char":
                    //TODO:
                    break;
            }
            //stack to be returned: [0] [underlying nullable type]
            //next IL called will be the Nullable<T> constructor.
        }

        private static void EmitCustomDeserializedTypeProp(ILGenerator il, PropInfo prop, MethodInfo deserializer, LukeMapperAttribute classAttr)
        {

            var lukeAttr = prop.PropertyInfo.GetCustomAttributes(typeof(LukeAttribute), true).FirstOrDefault() as LukeAttribute;

            if ((lukeAttr != null && lukeAttr.Ignore) || (classAttr.IgnoreByDefault && lukeAttr == null))
            {
                return;
            }

            il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldstr, lukeAttr != null && lukeAttr.FieldName != null ? lukeAttr.FieldName : prop.Name);


            il.Emit(OpCodes.Callvirt, GetFieldValue);
            il.Emit(OpCodes.Call, deserializer);

            il.Emit(OpCodes.Callvirt, prop.Setter);

        }

        private static void EmitField(ILGenerator il, string name, System.Reflection.FieldInfo field)
        {
            switch (field.FieldType.FullName)
            {
                case "System.String":
                    il.Emit(OpCodes.Ldloc_0);// [target]
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, name); // [target] [string]
                    il.Emit(OpCodes.Callvirt, GetFieldValue);
                    il.Emit(OpCodes.Stfld, field);
                    break;

                case "System.Int32":
                    //int.TryParse
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, name); // [target] [string]
                    il.Emit(OpCodes.Callvirt, GetFieldValue);
                    il.Emit(OpCodes.Ldloc_0); // [target]
                    il.Emit(OpCodes.Ldflda, field);
                    il.Emit(OpCodes.Call, IntTryParse);
                    il.Emit(OpCodes.Pop);
                    break;

                case "System.Int64":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, name); // [target] [string]
                    il.Emit(OpCodes.Callvirt, GetFieldValue);
                    il.Emit(OpCodes.Ldloc_0); // [target]
                    il.Emit(OpCodes.Ldflda, field);
                    il.Emit(OpCodes.Call, LongTryParse);
                    il.Emit(OpCodes.Pop);
                    break;

                case "System.Boolean":
                    il.Emit(OpCodes.Ldloc_0);// [target]
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, name); // [target] [string]
                    il.Emit(OpCodes.Callvirt, GetFieldValue);
                    il.Emit(OpCodes.Call, LukeMapperGetBoolean);
                    il.Emit(OpCodes.Stfld, field);
                    break;

                case "System.DateTime":
                    il.Emit(OpCodes.Ldloc_0);// [target]
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, name); // [target] [string]
                    il.Emit(OpCodes.Callvirt, GetFieldValue);
                    il.Emit(OpCodes.Call, LukeMapperGetDateTime);
                    il.Emit(OpCodes.Stfld, field);
                    break;

                case "System.Char":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, name);
                    il.Emit(OpCodes.Callvirt, GetFieldValue);

                    var s = il.DeclareLocal(typeof (string));

                    il.Emit(OpCodes.Stloc, s);
                    il.Emit(OpCodes.Ldloc, s);//
                    il.Emit(OpCodes.Call,IsNullOrEmpty);

                    var next = il.DefineLabel();

                    il.Emit(OpCodes.Brtrue_S, next);
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldloc, s);//
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Call, StringGetChars);
                    il.Emit(OpCodes.Stfld, field);
                    
                    il.MarkLabel(next);
                    break;
                default:
                    return;
            }

            
        }
        
        private static void EmitProp(ILGenerator il, string name, PropInfo prop)
        {

            switch (prop.Type.FullName)
            {
                case "System.String":
                    il.Emit(OpCodes.Ldloc_0);// [target]
                    il.Emit(OpCodes.Ldarg_0);

                    il.Emit(OpCodes.Ldstr, name); // [target] [string]
                    il.Emit(OpCodes.Callvirt, GetFieldValue);
                    il.Emit(OpCodes.Callvirt, prop.Setter);
                    break;
                case "System.Int32":
                    var lb = il.DeclareLocal(typeof (int));
                    
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, prop.Name);
                    il.Emit(OpCodes.Callvirt, GetFieldValue);
                    il.Emit(OpCodes.Ldloca_S, lb);
                    il.Emit(OpCodes.Call, IntTryParse);
                    il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldloc_S, lb);
                    il.Emit(OpCodes.Callvirt, prop.Setter);

                    break;
                case "System.Int64":
                    var lb64 = il.DeclareLocal(typeof (long));
                    
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, prop.Name);
                    il.Emit(OpCodes.Callvirt, GetFieldValue);
                    il.Emit(OpCodes.Ldloca_S, lb64);
                    il.Emit(OpCodes.Call, LongTryParse);
                    il.Emit(OpCodes.Pop);
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldloc_S, lb64);
                    il.Emit(OpCodes.Callvirt, prop.Setter);

                    break;
                case "System.Boolean":
                    il.Emit(OpCodes.Ldloc_0);// [target]
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, name); // [target] [string]
                    il.Emit(OpCodes.Callvirt, GetFieldValue);
                    il.Emit(OpCodes.Call, LukeMapperGetBoolean);
                    il.Emit(OpCodes.Callvirt, prop.Setter);
                    break;

                case "System.DateTime":
                    il.Emit(OpCodes.Ldloc_0);// [target]
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, name); // [target] [string]
                    il.Emit(OpCodes.Callvirt, GetFieldValue);
                    il.Emit(OpCodes.Call, LukeMapperGetDateTime);
                    il.Emit(OpCodes.Callvirt, prop.Setter);
                    break;

                case "System.Char":
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldstr, name);
                    il.Emit(OpCodes.Callvirt, GetFieldValue);

                    var s = il.DeclareLocal(typeof(string));

                    il.Emit(OpCodes.Stloc, s);
                    il.Emit(OpCodes.Ldloc, s);//
                    il.Emit(OpCodes.Call, IsNullOrEmpty);

                    var next = il.DefineLabel();

                    il.Emit(OpCodes.Brtrue_S, next);
                    il.Emit(OpCodes.Ldloc_0);
                    il.Emit(OpCodes.Ldloc, s);//
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Call, StringGetChars);
                    il.Emit(OpCodes.Callvirt, prop.Setter);

                    il.MarkLabel(next);
                    break;
                default:
                    return;
            }
        }

        #endregion

        #region Serialization

        private static Func<T, Document> GetSerializer<T>(Type type)
        {
            return GetTypeSerializer<T>(type);
            //return GetStructDeserializer(type, underlyingType ?? type, startBound);
        }

        // make delegate
        private static Func<T, Document> GetTypeSerializer<T>(Type type)
        {
            //debug only
            //var assemblyName = new AssemblyName("SomeName");
            //var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            //var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");

            //TypeBuilder builder = moduleBuilder.DefineType("Test", TypeAttributes.Public);
            //var dm = builder.DefineMethod(string.Format("Serialize{0}", Guid.NewGuid()), MethodAttributes.Public | MethodAttributes.Static, DocumentType, new[] { type });
            //debug only


            var dm = new DynamicMethod(
                string.Format("Serialize{0}", Guid.NewGuid()),
                DocumentType,
                new[] { type },
                true);

            var il = dm.GetILGenerator();

            var properties = GetSettableProps(type);
            var fields = GetSettableFields(type);

            var classAttr = type.GetCustomAttributes(typeof(LukeMapperAttribute), true).FirstOrDefault() as LukeMapperAttribute ??
                            new LukeMapperAttribute();



            il.DeclareLocal(DocumentType);
            il.Emit(OpCodes.Newobj, DocumentCtor);
            il.Emit(OpCodes.Stloc_0); //stack is [document]


            var serializers =
                type.GetMethods()
                    .Select(method => new
                    {
                        Method = method,
                        SerializerAttribute = (LukeSerializerAttribute)method.GetCustomAttributes(typeof(LukeSerializerAttribute), true).FirstOrDefault()
                    })
                    .Where(s => s.SerializerAttribute != null)
                    .ToList();



            foreach (var prop in properties)
            {
                var delimitedAttr =
                    (LukeDelimitedAttribute)prop.PropertyInfo.GetCustomAttributes(typeof (LukeDelimitedAttribute), true).FirstOrDefault();
                if (delimitedAttr != null)
                {
                    EmitDelimitedPropOrFieldToDocument(il, delimitedAttr, classAttr, prop: prop);
                    continue;
                } 

                var customSerializer = serializers.FirstOrDefault(ser => ser.SerializerAttribute.FieldName == prop.Name);
                if (customSerializer != null)
                {
                    EmitCustomSerializedTypeProp(il, prop, customSerializer.Method, classAttr);
                    continue;
                }

                EmitPropToDocument(il, prop, classAttr);
            }
            foreach (var field in fields)
            {
                var delimitedAttr =
                    (LukeDelimitedAttribute)field.GetCustomAttributes(typeof(LukeDelimitedAttribute), true).FirstOrDefault();
                if (delimitedAttr != null)
                {
                    //TODO:
                    //EmitDelimitedPropOrFieldToDocument(il, delimitedAttr, classAttr, field: field);
                    continue;
                }

                var customSerializer = serializers.FirstOrDefault(ser => ser.SerializerAttribute.FieldName == field.Name);
                if (customSerializer != null)
                {
                    //TODO: custom prop
                    //EmitCustomSerializedTypeProp(il, prop, customSerializer.Method, classAttr);
                    continue;
                }

                EmitFieldToDocument(il, field, classAttr);
            }

            il.Emit(OpCodes.Ldloc_0); // stack is [document]
            il.Emit(OpCodes.Ret);

            //debug only
            //var t = builder.CreateType();
            //assemblyBuilder.Save(assemblyName.Name + ".dll");
            //debug only


            return (Func<T, Document>)dm.CreateDelegate(typeof(Func<T, Document>));
            //return null;
        }

        // make assembly
        private static Func<T, Document> xxGetTypeSerializer<T>(Type type)
        {
            //debug only
            var assemblyName = new AssemblyName("SomeName");
            var assemblyBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.RunAndSave);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName.Name, assemblyName.Name + ".dll");

            TypeBuilder builder = moduleBuilder.DefineType("Test", TypeAttributes.Public);
            var dm = builder.DefineMethod(string.Format("Serialize{0}", Guid.NewGuid()), MethodAttributes.Public | MethodAttributes.Static, DocumentType, new[] { type });
            //debug only


            //var dm = new DynamicMethod(
            //    string.Format("Serialize{0}", Guid.NewGuid()),
            //    DocumentType,
            //    new[] { type },
            //    true);

            var il = dm.GetILGenerator();

            //TODO: maybe change to allow ALL properties?
            var properties = GetSettableProps(type);
            var fields = GetSettableFields(type);

            var classAttr = type.GetCustomAttributes(typeof(LukeMapperAttribute), true).FirstOrDefault() as LukeMapperAttribute ??
                new LukeMapperAttribute();

            il.DeclareLocal(DocumentType);
            il.Emit(OpCodes.Newobj, DocumentCtor);
            il.Emit(OpCodes.Stloc_0); //stack is [document]


            var serializers =
                type.GetMethods()
                    .Select(method => new
                    {
                        Method = method,
                        SerializerAttribute = (LukeSerializerAttribute)method.GetCustomAttributes(typeof(LukeSerializerAttribute), true).FirstOrDefault()
                    })
                    .Where(s => s.SerializerAttribute != null)
                    .ToList();



            foreach (var prop in properties)
            {
                var delimitedAttr =
                    (LukeDelimitedAttribute)prop.PropertyInfo.GetCustomAttributes(typeof(LukeDelimitedAttribute), true).FirstOrDefault();
                if (delimitedAttr != null)
                {
                    EmitDelimitedPropOrFieldToDocument(il, delimitedAttr, classAttr, prop: prop);
                    continue;
                }

                var customSerializer = serializers.FirstOrDefault(ser => ser.SerializerAttribute.FieldName == prop.Name);
                if (customSerializer != null)
                {
                    EmitCustomSerializedTypeProp(il, prop, customSerializer.Method, classAttr);
                    continue;
                }

                EmitPropToDocument(il, prop, classAttr);
            }
            foreach (var field in fields)
            {
                var delimitedAttr =
                    (LukeDelimitedAttribute)field.GetCustomAttributes(typeof(LukeDelimitedAttribute), true).FirstOrDefault();
                if (delimitedAttr != null)
                {
                    //TODO:
                    //EmitDelimitedPropOrFieldToDocument(il, delimitedAttr, classAttr, field: field);
                    continue;
                }

                var customSerializer = serializers.FirstOrDefault(ser => ser.SerializerAttribute.FieldName == field.Name);
                if (customSerializer != null)
                {
                    //TODO: custom prop
                    //EmitCustomSerializedTypeProp(il, prop, customSerializer.Method, classAttr);
                    continue;
                }

                EmitFieldToDocument(il, field, classAttr);
            }


            il.Emit(OpCodes.Ldloc_0); // stack is [document]
            il.Emit(OpCodes.Ret);

            //debug only
            var t = builder.CreateType();
            assemblyBuilder.Save(assemblyName.Name + ".dll");
            //debug only


            //return (Func<T, Document>)dm.CreateDelegate(typeof(Func<T, Document>));
            return null;
        }

        private static void EmitDelimitedPropOrFieldToDocument(ILGenerator il, LukeDelimitedAttribute delimitedAttr, LukeMapperAttribute classAttr, PropInfo prop = null, System.Reflection.FieldInfo field = null)
        {
            #region Validate Arguments

            var type = prop != null ? prop.PropertyInfo.PropertyType : field != null ? field.FieldType : null;
            if (type == null)
            {

                // no prop OR null specified
                throw new Exception("argument 'prop' or 'field' is missing");
            }


            var enumerableType = type.GetInterfaces()
                                     .Where(t => t.IsGenericType)
                                     .FirstOrDefault(t => t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

            if (enumerableType == null)
            {
                // needs to implement IEnumerable<T>;
                throw new Exception("The `LukeDelimited` attribute must be applied to a field or property which implements `IEnumerable<T>`");
            }

            var innerType = enumerableType.GetGenericArguments()[0];

            if (!SupportedTypes.Contains(innerType.FullName))
            {

                // innerType is not an allowed 
                throw new Exception(string.Format("The type `{0}` is not supported for Delimited lists. please use a `CustomDeserializer` method instead.", innerType.FullName));
            }

            //get LukeAttribute
            LukeAttribute lukeAttr;
            if (prop != null)
            {
                lukeAttr =
                    prop.PropertyInfo.GetCustomAttributes(typeof(LukeAttribute), true).FirstOrDefault() as
                    LukeAttribute;
            }
            else
            {
                lukeAttr =
                    field.GetCustomAttributes(typeof(LukeAttribute), true).FirstOrDefault() as
                    LukeAttribute;
            }
            if ((lukeAttr != null && lukeAttr.Ignore) || (classAttr.IgnoreByDefault && lukeAttr == null))
            {
                // field/prop should be ignored
                return;
            }

            #endregion

            il.Emit(OpCodes.Ldloc_0); // [document]
            il.Emit(OpCodes.Ldstr, lukeAttr != null && lukeAttr.FieldName != null ? lukeAttr.FieldName : prop.Name);
            il.Emit(OpCodes.Ldstr, delimitedAttr.Delimeter);// [document] [field name] [delimiter]
            il.Emit(OpCodes.Ldarg_0); // [document] [field name] [delimiter] [object]

            if (prop != null)
            {
                //case 1 : PROPERTY
                il.Emit(OpCodes.Callvirt, prop.Getter); // [document] [field name] [delimiter] [enumerable type]
            }
            else
            {
                il.Emit(OpCodes.Ldfld, field); // [document] [field name] [delimiter] [enumerable type]
            }

            if (innerType.FullName == "System.String")
            {
                il.Emit(OpCodes.Call,
                        typeof (string).GetMethod("Join", new[] {typeof (string), typeof (IEnumerable<string>)}));
                    // [document] [field name] [field string value]    
            }
            else
            {
                il.Emit(OpCodes.Call, StringGenericJoin.MakeGenericMethod(new[]{innerType})); // [document] [field name] [field string value]
            }
            

            il.Emit(OpCodes.Ldsfld, (lukeAttr == null ? classAttr.DefaultStore : lukeAttr.Store).ToFieldInfo()); // [document] [field name] [field string value] [Field.Store]
            il.Emit(OpCodes.Ldsfld, (lukeAttr == null ? classAttr.DefaultIndex : lukeAttr.Index).ToFieldInfo()); // [document] [field name] [field string value] [Field.Store] [Field.Index]

            il.Emit(OpCodes.Newobj, FieldCtor); // [document] [Field]
            il.Emit(OpCodes.Callvirt, DocumentAddField); // [document]
        }

        private static void EmitCustomSerializedTypeProp(ILGenerator il, PropInfo prop, MethodInfo serializer, LukeMapperAttribute classAttr)
        {

            var lukeAttr = prop.PropertyInfo.GetCustomAttributes(typeof(LukeAttribute), true).FirstOrDefault() as LukeAttribute;

            if ((lukeAttr != null && lukeAttr.Ignore) || (classAttr.IgnoreByDefault && lukeAttr == null))
            {
                return;
            }

            il.Emit(OpCodes.Ldloc_0); // [document]
            il.Emit(OpCodes.Ldstr, lukeAttr != null && lukeAttr.FieldName != null ? lukeAttr.FieldName : prop.Name);
            il.Emit(OpCodes.Ldarg_0); // [document] [field name] [object]




            il.Emit(OpCodes.Callvirt, prop.Getter);
            il.Emit(OpCodes.Call, serializer);




            il.Emit(OpCodes.Ldsfld, (lukeAttr == null ? classAttr.DefaultStore : lukeAttr.Store).ToFieldInfo()); // [document] [field name] [field string value] [Field.Store]
            il.Emit(OpCodes.Ldsfld, (lukeAttr == null ? classAttr.DefaultIndex : lukeAttr.Index).ToFieldInfo()); // [document] [field name] [field string value] [Field.Store] [Field.Index]

            il.Emit(OpCodes.Newobj, FieldCtor); // [document] [Field]
            il.Emit(OpCodes.Callvirt, DocumentAddField); // [document]

        }

        private static void EmitPropToDocument(ILGenerator il, PropInfo prop, LukeMapperAttribute classAttr)
        {

            var nullableType = Nullable.GetUnderlyingType(prop.Type);
            var IsNullableType = (nullableType != null);
            var type = IsNullableType ? nullableType : prop.Type;

            if (!SupportedTypes.Contains(type.FullName))
            {
                return;
            }

            var lukeAttr = prop.PropertyInfo.GetCustomAttributes(typeof(LukeAttribute), true).FirstOrDefault() as LukeAttribute;

            if ((lukeAttr != null && lukeAttr.Ignore) || (classAttr.IgnoreByDefault && lukeAttr == null))
            {
                return;
            }


            il.Emit(OpCodes.Ldloc_0); // [document]
            il.Emit(OpCodes.Ldstr, lukeAttr != null && lukeAttr.FieldName != null ? lukeAttr.FieldName : prop.Name);
            il.Emit(OpCodes.Ldarg_0); // [document] [field name] [object]

            switch (type.FullName)
            {
                case "System.String":
                    il.Emit(OpCodes.Callvirt, prop.Getter); // [document] [field name] [field value]
                    break;

                case "System.Int32":
                    var lb = il.DeclareLocal(prop.Type);

                    il.Emit(OpCodes.Callvirt, prop.Getter);
                    il.Emit(OpCodes.Stloc_S, lb);
                    il.Emit(OpCodes.Ldloca_S, lb);// [document] [field name] [int]
                    if (IsNullableType)
                    {
                        il.Emit(OpCodes.Constrained, prop.Type);
                        il.Emit(OpCodes.Callvirt, ObjectToString); // [document] [field name] [field string value]
                    }
                    else
                    {
                        il.Emit(OpCodes.Call, IntToString); // [document] [field name] [field string value]
                    }
                    break;

                case "System.Int64":
                    var lb2 = il.DeclareLocal(prop.Type);

                    il.Emit(OpCodes.Callvirt, prop.Getter);
                    il.Emit(OpCodes.Stloc_S, lb2);
                    il.Emit(OpCodes.Ldloca_S, lb2);// [document] [field name] [int]
                    if (IsNullableType)
                    {
                        il.Emit(OpCodes.Constrained, prop.Type);
                        il.Emit(OpCodes.Callvirt, ObjectToString); // [document] [field name] [field string value]
                    }
                    else
                    {
                        il.Emit(OpCodes.Call, LongToString); // [document] [field name] [field string value]
                    }
                    break;

                case "System.Boolean":
                    var lb5 = il.DeclareLocal(prop.Type);

                    il.Emit(OpCodes.Callvirt, prop.Getter);
                    il.Emit(OpCodes.Stloc_S, lb5);
                    il.Emit(OpCodes.Ldloca_S, lb5);// [document] [field name] [int]
                    if (IsNullableType)
                    {
                        il.Emit(OpCodes.Constrained, prop.Type);
                        il.Emit(OpCodes.Callvirt, ObjectToString); // [document] [field name] [field string value]
                    }
                    else
                    {
                        il.Emit(OpCodes.Call, typeof(bool).GetMethod("ToString", Type.EmptyTypes)); // [document] [field name] [field string value]
                    }
                    break;

                case "System.DateTime":
                    if (IsNullableType)
                    {
                        var breakoutLabel = il.DefineLabel();
                        var hasValueLabel = il.DefineLabel();
                        var lb3 = il.DeclareLocal(prop.Type);

                        il.Emit(OpCodes.Callvirt, prop.Getter);
                        il.Emit(OpCodes.Stloc_S, lb3);
                        il.Emit(OpCodes.Ldloca_S, lb3);

                        il.Emit(OpCodes.Call, prop.Type.GetProperty("HasValue").GetGetMethod());
                        il.Emit(OpCodes.Brtrue_S, hasValueLabel);

                        il.Emit(OpCodes.Ldstr, "");

                        il.Emit(OpCodes.Br_S, breakoutLabel);

                        il.MarkLabel(hasValueLabel);

                        il.Emit(OpCodes.Ldarg_0);

                        il.Emit(OpCodes.Callvirt, prop.Getter);
                        il.Emit(OpCodes.Stloc_S, lb3);
                        il.Emit(OpCodes.Ldloca_S, lb3);

                        il.Emit(OpCodes.Call, prop.Type.GetProperty("Value").GetGetMethod());
                        il.Emit(OpCodes.Call, LukeMapperToDateString);

                        il.MarkLabel(breakoutLabel);
                    }
                    else
                    {
                        il.Emit(OpCodes.Callvirt, prop.Getter);
                        il.Emit(OpCodes.Call, LukeMapperToDateString);
                    }
                    break;

                case "System.Char":
                    var lb4 = il.DeclareLocal(prop.Type);

                    il.Emit(OpCodes.Callvirt, prop.Getter);
                    il.Emit(OpCodes.Stloc_S, lb4);
                    il.Emit(OpCodes.Ldloca_S, lb4);// [document] [field name] [char]

                    if (IsNullableType)
                    {
                        //TODO: no idea why this doesn't work...
                        il.Emit(OpCodes.Constrained, prop.Type);
                        il.Emit(OpCodes.Callvirt, ObjectToString); // [document] [field name] [char string value]
                    }
                    else
                    {
                        il.Emit(OpCodes.Call, prop.Type.GetMethod("ToString", Type.EmptyTypes)); // [document] [field name] [char string value]
                    }
                    break;
            }


            il.Emit(OpCodes.Ldsfld, (lukeAttr == null ? classAttr.DefaultStore : lukeAttr.Store).ToFieldInfo()); // [document] [field name] [field string value] [Field.Store]
            il.Emit(OpCodes.Ldsfld, (lukeAttr == null ? classAttr.DefaultIndex : lukeAttr.Index).ToFieldInfo()); // [document] [field name] [field string value] [Field.Store] [Field.Index]

            il.Emit(OpCodes.Newobj, FieldCtor); // [document] [Field]
            il.Emit(OpCodes.Callvirt, DocumentAddField); // [document]
        }

        private static void EmitFieldToDocument(ILGenerator il, System.Reflection.FieldInfo field, LukeMapperAttribute classAttr)
        {
            var nullableType = Nullable.GetUnderlyingType(field.FieldType);
            var IsNullableType = (nullableType != null);
            var type = IsNullableType ? nullableType : field.FieldType;

            if (!SupportedTypes.Contains(type.FullName))
            {
                return;
            }

            var lukeAttr = field.GetCustomAttributes(typeof(LukeAttribute), true).FirstOrDefault() as LukeAttribute;

            if ((lukeAttr != null && lukeAttr.Ignore) || (classAttr.IgnoreByDefault && lukeAttr == null))
            {
                return;
            }


            il.Emit(OpCodes.Ldloc_0); // [document]
            il.Emit(OpCodes.Ldstr, lukeAttr != null && lukeAttr.FieldName != null ? lukeAttr.FieldName : field.Name);
            il.Emit(OpCodes.Ldarg_0); // [document] [field name] [object]

            switch (type.FullName)
            {
                case "System.String":
                    il.Emit(OpCodes.Ldfld, field); // [document] [field name] [field value]
                    break;

                case "System.Int32":
                    il.Emit(OpCodes.Ldflda, field); // [document] [field name] [field value]
                    if (IsNullableType)
                    {
                        il.Emit(OpCodes.Constrained, field.FieldType);
                        il.Emit(OpCodes.Callvirt, ObjectToString); // [document] [field name] [field string value]
                    }
                    else
                    {
                        il.Emit(OpCodes.Call, IntToString); // [document] [field name] [field string value]
                    }
                    break;

                case "System.Int64":
                    il.Emit(OpCodes.Ldflda, field); // [document] [field name] [field value]
                    if (IsNullableType)
                    {
                        il.Emit(OpCodes.Constrained, field.FieldType);
                        il.Emit(OpCodes.Callvirt, ObjectToString); // [document] [field name] [field string value]
                    }
                    else
                    {
                        il.Emit(OpCodes.Call, LongToString); // [document] [field name] [field string value]
                    }
                    break;

                case "System.Boolean":
                    il.Emit(OpCodes.Ldflda, field); // [document] [field name] [field value]
                    if (IsNullableType)
                    {
                        il.Emit(OpCodes.Constrained, field.FieldType);
                        il.Emit(OpCodes.Callvirt, ObjectToString); // [document] [field name] [field string value]
                    }
                    else
                    {
                        il.Emit(OpCodes.Call, typeof(bool).GetMethod("ToString", Type.EmptyTypes)); // [document] [field name] [field string value]
                    }
                    break;

                case "System.DateTime":
                    if (IsNullableType)
                    {
                        var breakoutLabel = il.DefineLabel();
                        var hasValueLabel = il.DefineLabel();

                        il.Emit(OpCodes.Ldflda, field);
                        il.Emit(OpCodes.Call, field.FieldType.GetProperty("HasValue").GetGetMethod());
                        il.Emit(OpCodes.Brtrue_S, hasValueLabel);

                        il.Emit(OpCodes.Ldstr, "");

                        il.Emit(OpCodes.Br_S, breakoutLabel);

                        il.MarkLabel(hasValueLabel);

                        il.Emit(OpCodes.Ldarg_0);
                        il.Emit(OpCodes.Ldflda, field);
                        il.Emit(OpCodes.Call, field.FieldType.GetProperty("Value").GetGetMethod());
                        il.Emit(OpCodes.Call, LukeMapperToDateString);

                        il.MarkLabel(breakoutLabel);
                    }
                    else
                    {
                        il.Emit(OpCodes.Ldfld, field);
                        il.Emit(OpCodes.Call, LukeMapperToDateString);
                    }
                    break;

                case "System.Char":
                    il.Emit(OpCodes.Ldflda, field);
                    if (IsNullableType)
                    {
                        break;
                        //TODO: no idea why this doesn't work...
                        il.Emit(OpCodes.Constrained, field.FieldType);
                        il.Emit(OpCodes.Callvirt, ObjectToString); // [document] [field name] [field string value]
                    }
                    else
                    {
                        il.Emit(OpCodes.Call, field.FieldType.GetMethod("ToString", Type.EmptyTypes));
                    }
                    break;
            }

            il.Emit(OpCodes.Ldsfld, (lukeAttr == null ? classAttr.DefaultStore : lukeAttr.Store).ToFieldInfo()); // [document] [field name] [field string value] [Field.Store]
            il.Emit(OpCodes.Ldsfld, (lukeAttr == null ? classAttr.DefaultIndex : lukeAttr.Index).ToFieldInfo()); // [document] [field name] [field string value] [Field.Store] [Field.Index]

            il.Emit(OpCodes.Newobj, FieldCtor); // [document] [Field]
            il.Emit(OpCodes.Callvirt, DocumentAddField); // [document]
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Deserializer function for DateTime.  This can be reimplemented/changed 
        /// if you would like DateTimes to be stored a different way in lucene.
        /// 
        /// 
        /// Right now, assumes that Dates are stored in UnixTime format.
        /// 
        /// //TODO: create API for user to override this function
        /// </summary>
        public static DateTime GetDateTime(string val)
        {
            long ret;
            return long.TryParse(val, out ret) ? EpochDate.AddSeconds(ret) : DateTime.Now;
        }

        private static readonly DateTime EpochDate = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        //Takes a date and returns a UnixTime Timestamp string
        public static string ToDateString(DateTime val)
        {
            return ((long)((val.ToUniversalTime() - EpochDate).TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }
        
        
        /// <summary>
        /// Right now I this is implemented by simply checking if the string is a
        /// "truthy" string.  Seems simple enough.
        /// </summary>
        public static bool GetBoolean(string val)
        {
            //falsy: "0", "false", "False", "", null
            //truthy: "1", "true", "True"

            return TruthyStrings.Contains(val);
        }
        private static readonly string[] TruthyStrings = new[] { "True", "1", "true" };


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




        #endregion

        #region Reflection

        class PropInfo
        {
            public string Name { get; set; }
            public MethodInfo Setter { get; set; }
            public MethodInfo Getter { get; set; }
            public Type Type { get; set; }
            public PropertyInfo PropertyInfo { get; set; }
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
                      Type = p.PropertyType,
                      Getter = p.GetGetMethod(true),
                      PropertyInfo = p
                  })
                  .Where(info => info.Setter != null && info.Getter != null)
                  .ToList();
        }

        static List<System.Reflection.FieldInfo> GetSettableFields(Type t)
        {
            return t
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(f=>!f.Name.Contains("__BackingField")) //prevents backing fields from being included
                .ToList();
        }

        #endregion

        #region Public Endpoints

        public static IEnumerable<T> Query<T>(
            this IndexSearcher searcher, 
            Query query, 
            int n /*, Sort sort*/)
        {
            var identity = new Identity(searcher,query,typeof(T));
            var info = GetDeserializerCacheInfo(identity);


            //****: create lambda to generate deserializer method, then cache it
            //****: we do this here in case the underlying schema has changed we can regenerate...
            TopDocs td = searcher.Search(query, n);

            if (td.TotalHits == 0)
            {
                yield break;
            }

            Func<Func<Document, object>> cacheDeserializer = () =>
                    {
                        info.Deserializer = GetDeserializer(typeof(T), searcher);
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

        public static IEnumerable<dynamic> Query(this IndexSearcher searcher, Query query, int n)
        {
            return searcher.Query<FastExpando>(query, n);
        }


        public static void Write<T>(this IndexWriter writer, IEnumerable<T> entities, Analyzer analyzer)
        {
            var identity = new Identity(typeof(T));
            var info = GetSerializerCacheInfo<T>(identity);


            //****: create lambda to generate deserializer method, then cache it
            //****: we do this here in case the underlying schema has changed we can regenerate...

            Func<Func<T, Document>> cacheSerializer = () =>
            {
                info.Serializer = GetSerializer<T>(typeof(T));
                SetWriteCache(identity, info);
                return info.Serializer;
            };

            //****: check info for serializer, if null => run it.

            if (info.Serializer == null)
            {
                cacheSerializer();
            }

            var serializer = info.Serializer;
            foreach (var entity in entities)
            {
                writer.AddDocument(serializer(entity));
            }

        }


        #endregion
    }
}