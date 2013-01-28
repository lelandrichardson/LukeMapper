using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lucene.Net.Documents;

namespace LukeMapper
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    class LukeMapperAttribute : Attribute
    {
        /// <summary>
        /// If True, all members of the decorated class will be added into the lucene document
        /// with the options [Field.Index.NOT_ANALYZED_NO_NORMS, Field.Store.YES] provided no
        /// LukeAttribute is applied to that member
        /// </summary>
        public bool IgnoreByDefault = false;
        public Store DefaultStore = LukeMapper.DefaultStore;
        public Index DefaultIndex = LukeMapper.DefaultIndex;
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class LukeAttribute : Attribute
    {
        /// <summary>
        /// How will this member be stored in the Lucene Index
        /// </summary>
        public Store Store = LukeMapper.DefaultStore;

        /// <summary>
        /// How will this member be indexed in the Lucene Index
        /// </summary>
        public Index Index = LukeMapper.DefaultIndex;

        /// <summary>
        /// If true, this member will not be added to the lucene document
        /// </summary>
        public bool Ignore = false;

        /// <summary>
        /// Custom Field Name to store/index this member as in the Lucene Index
        /// </summary>
        public string FieldName = null;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class LukeSerializerAttribute : Attribute
    {
        public string FieldName { get; set; }
        public LukeSerializerAttribute(string fieldName = null)
        {
            FieldName = fieldName;
        }
    }

    /// <summary>
    /// Expected a method with parameters of type Document, and expected to properly
    /// deserialize + set the corresponding member from the document to the current instance.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class LukeDeserializerAttribute : Attribute
    {
        public string FieldName { get; set; }
        public LukeDeserializerAttribute(string fieldName = null)
        {
            FieldName = fieldName;
        }
    }

    //TODO: numeric fields...
    //TODO: term vectors...


    [LukeMapper]
    public class ExampleClass
    {
        public int Id { get; set; }

        public string Title { get; set; }

        [Luke(Store = Store.YES, Index = Index.ANALYZED, FieldName = "_Body")]
        public string Body { get; set; }

        [LukeSerializer]
        private static Document SerializeToDocument(ExampleClass obj)
        {
            var doc = new Document();


            return doc;
        }

        [LukeDeserializer]
        private static ExampleClass DeserializeDocument(Document doc)
        {
            return new ExampleClass();
        }

    }
}
