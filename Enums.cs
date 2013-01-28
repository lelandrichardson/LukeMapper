using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lucene.Net.Documents;

namespace LukeMapper
{
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

        public static System.Reflection.FieldInfo ToFieldInfo(this Store store)
        {
            switch (store)
            {
                case Store.NO: return typeof(Field.Store).GetField("NO");
                case Store.COMPRESS: return typeof(Field.Store).GetField("COMPRESS");
                default: return typeof(Field.Store).GetField("YES");
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

        public static System.Reflection.FieldInfo ToFieldInfo(this Index store)
        {
            switch (store)
            {
                case Index.NO: return typeof(Field.Index).GetField("NO");
                case Index.ANALYZED: return typeof(Field.Index).GetField("ANALYZED");
                case Index.ANALYZED_NO_NORMS: return typeof(Field.Index).GetField("ANALYZED_NO_NORMS");
                case Index.NOT_ANALYZED: return typeof(Field.Index).GetField("NOT_ANALYZED");
                default: return typeof(Field.Index).GetField("NOT_ANALYZED_NO_NORMS");
            }
        }
    }

}
