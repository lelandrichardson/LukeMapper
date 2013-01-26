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
        public bool IgnoreByDefault = true;
        public Store DefaultStore = Store.YES;
        public Index DefaultIndex = Index.NOT_ANALYZED_NO_NORMS;

    }


    public enum Store
    {
        YES,
        NO,
        COMPRESS
    }
    public enum Index
    {
        ANALYZED,
        ANALYZED_NO_NORMS,
        NO,
        NOT_ANALYZED,
        NOT_ANALYZED_NO_NORMS
    }

    public static class EnumExtensions
    {
        public static Field.Store ToFieldStore(this Store store)
        {
            switch (store)
            {
                case Store.NO: return Field.Store.NO;
                case Store.COMPRESS: return Field.Store.COMPRESS;
                default: return Field.Store.YES;
            }
        }

        public static Field.Index ToFieldIndex(this Index store)
        {
            switch (store)
            {
                case Index.NO: return Field.Index.NO;
                case Index.ANALYZED: return Field.Index.ANALYZED;
                case Index.ANALYZED_NO_NORMS: return Field.Index.ANALYZED_NO_NORMS;
                case Index.NOT_ANALYZED: return Field.Index.NOT_ANALYZED;
                default: return Field.Index.NOT_ANALYZED_NO_NORMS;
            }
        }
    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class LukeAttribute : Attribute
    {
        /// <summary>
        /// How will this member be stored in the Lucene Index
        /// </summary>
        public Store Store = Store.YES;

        /// <summary>
        /// How will this member be indexed in the Lucene Index
        /// </summary>
        public Index Index = Index.NOT_ANALYZED_NO_NORMS;

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
        
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class LukeDeserializerAttribute : Attribute
    {
        
    }

    //TODO: numeric fields...
    //TODO: term vectors...

    /// <summary>
    /// Expected a method with parameters of type Document, and expected to properly
    /// deserialize + set the corresponding member from the document to the current instance.
    /// </summary>
    public class LukeMemberDeserializer : Attribute
    {
        public string Member;

        public LukeMemberDeserializer(string member)
        {
            Member = member;
        }
    }




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
