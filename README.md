#LukeMapper

##Lucene.Net ORM inspired by [Dapper][0]

###Purpose:

The concept I am trying to achieve is something similar in spirit to [Dapper][0], except is meant to deal with mapping Lucene Documents to generic Objects, rather than Rows from a database.


The desired API is something like the following:

Given some generic class in .Net like as follows:

    class PocoClass
    {
        public int Id;
        public string Name;

        public int PropId { get; set; }
        public string PropName { get; set; }
    }

Whereas, if I wanted to run a query against an `IndexSearcher` in Lucene, and return the corresponding documents
mapped to a List<PocoClass>, I could do the following:

    IndexSearcher searcher;
    Query qry;
    int numberToReturn = 10;

    List<PocoClass> results = searcher.Query<PocoClass>(qry, numberToReturn);

Thus, the `.Query<T>(Query,int)` method is implemented as an extension method to an `IndexSearcher`, similar to 
how Dapper's `.Query<T>` method is implemented as an extension method to an `IDBConnection` object.

Similar to Dapper and other Micro-ORMs out there, the implementation of the mapping will be done
by generating a Deserializer method via IL-Generation and caching it.

The desired IL method generated should be semantically similar to the IL generated from the following method:

    public static PocoClass ExampleDeserializerMethod(Document document)
    {
        var poco = new PocoClass();

        poco.Id = Convert.ToInt32(document.Get("Id"));
        poco.Name = document.Get("Name");

        poco.PropId = Convert.ToInt32(document.Get("PropId"));
        poco.PropName = document.Get("PropName");

        return poco;
    }

Although, some error handling may need to be inserted, among other things to make the method a bit more robust.

As of now, the cacheing is done via a hashcode which should be unique to the declared fields in the `IndexSearcher`'s index, 
and the object type which it is being mapped to.


##Desired Data Types to support:

- Textual:
    - string
    - char

- Numeric:
    - int
    - long
    - byte

- Other:
    - bool
    - DateTime
    

###Notes:
In many ways this is not as practical as Dapper and is more of a specific application; Lucene is only meant to handle textual 
data and is schema-less, so mapping to objects of non-textual type with a specific schema is more error prone. The reality, though, is that
most Lucene indexes are implemented with a relatively uniform schema.









[0]: http://code.google.com/p/dapper-dot-net/