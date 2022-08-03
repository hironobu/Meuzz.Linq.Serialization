using System;
using System.Linq.Expressions;
using Meuzz.Linq.Serialization.Serializers;

namespace Meuzz.Linq.Serialization
{
    public abstract class ExpressionSerializer
    {
        public abstract string Serialize<T>(Expression<T> f) where T : Delegate;

        public abstract T Deserialize<T>(string s) where T : Delegate;

        public static ExpressionSerializer CreateInstance()
        {
            return new JsonNetSerializer();
        }
    }
}
