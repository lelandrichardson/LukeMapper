#LukeMapper

##Lucene.Net ORM inspired by [Dapper][0]

###Purpose:

The concept I am trying to achieve is something similar in spirit to [Dapper][0], except is meant to deal with mapping Lucene Documents to generic Objects, rather than Rows from a database.


The desired API is something like the following:

Given some generic class in .Net like as follows:

```csharp
class PocoClass
{
    public int Id;
    public string Name;

    public int PropId { get; set; }
    public string PropName { get; set; }
}
```

##Read Operations

If I wanted to run a query against an `IndexSearcher` in Lucene, and return the corresponding documents
mapped to a List<PocoClass>, I could do the following:

```csharp
IndexSearcher searcher;
Query qry;
int numberToReturn = 10;

List<PocoClass> results = searcher.Query<PocoClass>(qry, numberToReturn);
```

Thus, the `.Query<T>(Query,int)` method is implemented as an extension method to an `IndexSearcher`, similar to 
how Dapper's `.Query<T>` method is implemented as an extension method to an `IDBConnection` object.

##Write Operations

```csharp
IndexWriter writer;
IEnumerable<PocoClass> objects;

// insert objects into index
writer.Write(objects)
```

And similarly, an update operation:
	
```csharp
IndexWriter writer;
IEnumerable<PocoClass> objects;
//method to find the corresponding document to update
Func<PocoClass, Query> identifyingQuery = o => new TermQuery(new Term("Id",o.Id.ToString()));

// update objects in index
writer.Update(objects, identifyingQuery);
```	

Similar to Dapper and [other Micro-ORMs out there](https://github.com/sapiens/SqlFu), the implementation of the **mapping will be done by generating a Deserializer/Serializer method via IL-Generation and caching it**.

For the `.Query()` operation, the desired IL method generated should be semantically similar to the IL generated from the following method:

```csharp
public static PocoClass ExampleDeserializerMethod(Document document)
{
    var poco = new PocoClass();

    poco.Id = Convert.ToInt32(document.Get("Id"));
    poco.Name = document.Get("Name");

    poco.PropId = Convert.ToInt32(document.Get("PropId"));
    poco.PropName = document.Get("PropName");

    return poco;
}
```

Similarly, for the `.Write()` and `Update()` methods, the Serializer methods will be semantically similar to the IL generated from the following method:

```csharp
public static Document ExampleSerializerMethod(PocoClass obj)
{
    var doc = new Document();

    doc.Add(new Field("Id", obj.Id.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
	doc.Add(new Field("Name", obj.Name, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
	doc.Add(new Field("PropId", obj.PropId.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));
	doc.Add(new Field("PropName", obj.PropName, Field.Store.YES, Field.Index.NOT_ANALYZED_NO_NORMS));

    return doc;
}
```

Although, some error handling may need to be inserted, among other things to make the method a bit more robust.

##Enhancing / Customizing with Attributes

Although basic functionality works essentially out of the box, with no attributes needed, further flexibility is garnered by the use of various Attributes.

```csharp
[LukeMapper(IgnoreByDefault = true)]
public class ExampleClass
{
    // doesn't get indexed/stored
    [Luke(Store = Store.YES)]
    public int Id { get; set; }
    
    // doesn't get stored, but is indexed in "searchtext" field
    [Luke(Store = Store.NO, Index = Index.ANALYZED, FieldName = "searchtext")]
    public string Title { get; set; }

    // doesn't get stored, but is indexed in "searchtext" field
    [Luke(Store = Store.NO, Index = Index.ANALYZED, FieldName = "searchtext")]
    public string Body { get; set; }

    // doesn't get indexed/stored
    public int IgnoredProperty { get; set; }
}

[LukeMapper(DefaultIndex = Index.ANALYZED)]
public class ExampleClass
{
    // doesn't get indexed/stored
    [Luke(Index = Index.NOT_ANALYZED_NO_NORMS)]
    public int Id { get; set; }

    // get's analyzed, AND stored
    public string Title { get; set; }

    // get's analyzed, AND stored
    public string Body { get; set; }
}

public class ExampleClass
{
    // everything get's indexed and stored by default
    public int Id { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }

    //opt-in ignored per property/field
    [Luke(Ignore=true)]
    public int Ignored { get; set; }
}

public class ExampleClass
{
    // everything get's indexed and stored by default
    public int Id { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }

    //opt-in ignored per property/field
    public int Ignored { get; set; }
}
```

##Custom Serialization/Deserialization

You can override the serialization of certain properties, even more complex ones which are not supported, if it is needed for your application.

For instance, a common example might be that I have a list or array of something that I would like to serialize/deserialize into the document.

In this case, you can simply specify a static method to use for the serialization (and deserialization) using the `LukeSerializerAttribute` and `LukeDeserializerAttribute`.


```csharp
public class TestCustomSerializerClass
{
    public int Id { get; set; }

    //this list would typically be ignored
    public List<string> CustomList { get; set; }

    // if you specify a serializer, it will get serialized
    [LukeSerializer("CustomList")]
    public static string CustomListToString(List<string> list)
    {
        return string.Join(",", list);
    }

    // and similarly, deserialized
    [LukeDeserializer("CustomList")]
    public static List<string> StringToCustomList(string serialized)
    {
        return serialized.Split(',').ToList();
    }
}


public class TestCustomSerializerClass
{
    public int Id { get; set; }

    // maybe you just want to index the list for search, but don't need it on .Query()
    [Luke(Store = Store.NO,Index = Index.ANALYZED)]
    public List<string> CustomList { get; set; }

    // in this case, only a serializer is needed
    [LukeSerializer("CustomList")]
    public static string CustomListToString(List<string> list)
    {
        return string.Join(" ", list);
    }
}
```

As of now, the cacheing is done via a hashcode which should be unique to the declared fields in the `IndexSearcher`'s index, 
and the object type which it is being mapped to.


##Data Types supported:

- Textual:
    - string
    - char

- Numeric:
    - int
	- int?
    - long
	- long?

- Other:
    - bool
	- bool?
    - DateTime
	- DateTime?

- In Progress (Not Yet Supported):
	- char?
	- byte
	- byte?

###Notes:
In many ways this is not as practical as Dapper and is more of a specific application; Lucene is only meant to handle textual data and is schema-less, so mapping to objects of non-textual type with a specific schema is more error prone. The reality, though, is that most Lucene indexes are implemented with a relatively uniform schema.


###Current Status:

I have started working on this project more and think it has promise and will likely use it in some projects of my own.  If anyone is interested in helping out, I would certainly love the help.  On the other hand, if anyone has any suggestions or feature requests, bring them on.

Right now, I am focussing on the following:

- Improve the error handling / feedback currently
- Build in some support for NumericFields
- Attribute to specify the "Identifier" of an object, and auto-generate the "identifyingQuery" needed for the `Update()` method.
- Attribute to utilize term vectors usefully
- Build in some automatic support for handling lists in typical fashion (ie csv, json-encoding, etc)
- get `char`'s and `byte`'s working (seriously, why are they so difficult?)

Any comments, feel free to hit me up here or on [twitter](http://twitter.com/intelligibabble)

###License

Copyright 2013 Leland M. Richardson

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

   [http://www.apache.org/licenses/LICENSE-2.0](http://www.apache.org/licenses/LICENSE-2.0)

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.


[0]: http://code.google.com/p/dapper-dot-net/
