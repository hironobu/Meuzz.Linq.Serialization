using System;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.Serialization.Json;
using System.Text.Json;
using Meuzz.Linq.Serialization.Expressions;

namespace Meuzz.Linq.Serialization
{
    public class ExpressionSerializer
    {
        public static Delegate DoSerialize(Expression f)
        {
            using var stream = new MemoryStream();

            var data = ExpressionData.Pack(f);


            //var data2 = JsonSerializer.Deserialize(s, data.GetType());
            var data2 = data;

            var t2 = (LambdaExpression)data2.Unpack();

            return t2.Compile();
        }
    }
}
