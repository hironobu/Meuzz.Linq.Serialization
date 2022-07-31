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
            var typeDataManager = new TypeDataManager();

            var data = ExpressionData.Pack(f, typeDataManager);

            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExpressionDataJsonConverter(typeDataManager));
            options.Converters.Add(new MemberInfoDataJsonConverter(typeDataManager));
            options.Converters.Add(new MethodInfoDataJsonConverter(typeDataManager));
            options.Converters.Add(new TypeDataJsonConverter());
            options.Converters.Add(new PacketJsonConverter());

            var s = JsonSerializer.Serialize(data, typeof(ExpressionData), options);
            var s2 = JsonSerializer.Serialize(new ExpressionPacket(s, typeDataManager.Types), typeof(ExpressionPacket), options);

            Debug.WriteLine($"serialized: {s2}");

            return s2;
        }

        public static T Deserialize<T>(object obj) where T : Delegate
        {
            var typeDataManager = new TypeDataManager();

            var options = new JsonSerializerOptions();
            options.Converters.Add(new ExpressionDataJsonConverter(typeDataManager));
            options.Converters.Add(new MemberInfoDataJsonConverter(typeDataManager));
            options.Converters.Add(new MethodInfoDataJsonConverter(typeDataManager));
            options.Converters.Add(new TypeDataJsonConverter());
            options.Converters.Add(new PacketJsonConverter());

            var packet = (ExpressionPacket)JsonSerializer.Deserialize((string)obj, typeof(ExpressionPacket), options);

            typeDataManager.LoadTypes(packet.Types);

            var data = (ExpressionData)JsonSerializer.Deserialize(packet.Data, typeof(ExpressionData), options);

            var t2 = (LambdaExpression)data.Unpack(typeDataManager);

            return (T)t2.Compile();
        }
    }
}
