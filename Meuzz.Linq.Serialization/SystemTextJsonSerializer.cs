using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Meuzz.Linq.Serialization.Core;
using Meuzz.Linq.Serialization.Expressions;

namespace Meuzz.Linq.Serialization
{
    public static class SystemTextJsonSerializer
    {
        public static object Serialize<T>(Expression<T> f)
        {
            // using var stream = new MemoryStream();

            var data = ExpressionData.Pack(f);

            var options = new JsonSerializerOptions();
            var typeDataManager = new TypeDataManager();
            options.Converters.Add(new ExpressionDataJsonConverter(typeDataManager));
            options.Converters.Add(new MemberInfoDataJsonConverter(typeDataManager));
            options.Converters.Add(new MethodInfoDataJsonConverter(typeDataManager));

            var s = JsonSerializer.Serialize(data, data.GetType(), options);
            var s2 = JsonSerializer.Serialize(typeDataManager.TypeNameTable, options);
            Debug.WriteLine($"serialized: {s}");
            Debug.WriteLine($"serialized: {s2}");

            return s;
        }

        public static T Deserialize<T>(object obj) where T : Delegate
        {
            var options = new JsonSerializerOptions();
            var typeDataManager = new TypeDataManager();
            options.Converters.Add(new ExpressionDataJsonConverter(typeDataManager));
            options.Converters.Add(new MemberInfoDataJsonConverter(typeDataManager));
            options.Converters.Add(new MethodInfoDataJsonConverter(typeDataManager));

            var data2 = (ExpressionData)JsonSerializer.Deserialize((string)obj, typeof(ExpressionData), options);

            var t2 = (LambdaExpression)data2.Unpack(typeDataManager);

            return (T)t2.Compile();
        }
    }
}
