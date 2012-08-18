using System;
using System.Data;
using Lucene.Net.Index;
using Lucene.Net.Search;
using LukeMapper;

/// <summary>
/// Identity of a cached query in Dapper, used for extensability
/// </summary>
public class Identity : IEquatable<Identity>
{
    internal Identity(IndexSearcher searcher, Query query, Type type/*, Type[] otherTypes (for MultiMap)*/)
    {
        var reader = searcher.GetIndexReader();
        var fieldNames = reader.GetFieldNames(IndexReader.FieldOption.ALL);
        this.type = type;

        unchecked
        {
            hashCode = 17; // we *know* we are using this in a dictionary, so pre-compute this
            hashCode = hashCode * 23 + (type == null ? 0 : type.GetHashCode());
        }

        foreach (var fieldName in fieldNames)
        {
            unchecked
            {
                hashCode = hashCode*23 + fieldName.GetHashCode();
            }
        }

    }

    internal Identity(Type type)
    {
        var fieldNames = new[] {"one", "two", "three"};
        unchecked
        {
            hashCode = 17; // we *know* we are using this in a dictionary, so pre-compute this
            hashCode = hashCode * 23 + (type == null ? 0 : type.GetHashCode());
        }

        foreach (var fieldName in fieldNames)
        {
            unchecked
            {
                hashCode = hashCode * 23 + fieldName.GetHashCode();
            }
        }

    }

    public override bool Equals(object obj)
    {
        return Equals(obj as Identity);
    }

    public readonly int hashCode;
    private readonly Type type;

    public override int GetHashCode()
    {
        return hashCode;
    }

    /// <summary>
    /// Compare 2 Identity objects
    /// </summary>
    public bool Equals(Identity other)
    {
        return
            other != null &&
            GetHashCode() == other.GetHashCode();
    }
}