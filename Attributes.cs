using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;

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

        //[LukeSerializer]
        //private static Document SerializeToDocument(ExampleClass obj)
        //{
        //    var doc = new Document();


        //    return doc;
        //}

        //[LukeDeserializer]
        //private static ExampleClass DeserializeDocument(Document doc)
        //{
        //}

    }



    //[LukeMapper(IgnoreByDefault = true)]
    //public class ExampleClass
    //{
    //    // doesn't get indexed/stored
    //    [Luke(Store = Store.YES)]
    //    public int Id { get; set; }
        
    //    // doesn't get stored, but is indexed in "searchtext" field
    //    [Luke(Store = Store.NO, Index = Index.ANALYZED, FieldName = "searchtext")]
    //    public string Title { get; set; }

    //    // doesn't get stored, but is indexed in "searchtext" field
    //    [Luke(Store = Store.NO, Index = Index.ANALYZED, FieldName = "searchtext")]
    //    public string Body { get; set; }

    //    // doesn't get indexed/stored
    //    public int IgnoredProperty { get; set; }
    //}

    //[LukeMapper(DefaultIndex = Index.ANALYZED)]
    //public class ExampleClass
    //{
    //    // doesn't get indexed/stored
    //    [Luke(Index = Index.NOT_ANALYZED_NO_NORMS)]
    //    public int Id { get; set; }

    //    // get's analyzed, AND stored
    //    public string Title { get; set; }

    //    // get's analyzed, AND stored
    //    public string Body { get; set; }
    //}

    //public class ExampleClass
    //{
    //    // everything get's indexed and stored by default
    //    public int Id { get; set; }
    //    public string Title { get; set; }
    //    public string Body { get; set; }

    //    //opt-in ignored per property/field
    //    [Luke(Ignore=true)]
    //    public int Ignored { get; set; }
    //}

    //public class ExampleClass
    //{
    //    // everything get's indexed and stored by default
    //    public int Id { get; set; }
    //    public string Title { get; set; }
    //    public string Body { get; set; }

    //    //opt-in ignored per property/field
    //    public int Ignored { get; set; }
    //}

    //public class TestCustomSerializerClass
    //{
    //    public int Id { get; set; }

    //    //this list would typically be ignored
    //    public List<string> CustomList { get; set; }

    //    // if you specify a serializer, it will get serialized
    //    [LukeSerializer("CustomList")]
    //    public static string CustomListToString(List<string> list)
    //    {
    //        return string.Join(",", list);
    //    }

    //    // and similarly, deserialized
    //    [LukeDeserializer("CustomList")]
    //    public static List<string> StringToCustomList(string serialized)
    //    {
    //        return serialized.Split(',').ToList();
    //    }
    //}


    //public class TestCustomSerializerClass
    //{
    //    public int Id { get; set; }

    //    // maybe you just want to index the list for search, but don't need it on .Query()
    //    [Luke(Store = Store.NO,Index = Index.ANALYZED)]
    //    public List<string> CustomList { get; set; }

    //    // in this case, only a serializer is needed
    //    [LukeSerializer("CustomList")]
    //    public static string CustomListToString(List<string> list)
    //    {
    //        return string.Join(" ", list);
    //    }
    //}


}
