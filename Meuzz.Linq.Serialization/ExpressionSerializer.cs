using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace Meuzz.Linq.Serialization
{
    public abstract class ExpressionSerializer
    {
        public abstract object Serialize<T>(Expression<T> f);

        public abstract T Deserialize<T>(object o) where T : Delegate;
    }
}
