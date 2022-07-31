using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Meuzz.Linq.Serialization.Core;
using Meuzz.Linq.Serialization.Expressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Meuzz.Linq.Serialization
{
    public class CustomSerializationBinder : DefaultSerializationBinder
    {
        public CustomSerializationBinder(TypeDataManager typeDataManager)
        {
            _typeDataManager = typeDataManager;
        }

        public override void BindToName(
            Type serializedType, out string? assemblyName, out string? typeName)
        {
            if (serializedType.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Any())
            {
                var fields = serializedType.GetFields();
                assemblyName = null;

                var sb = new StringBuilder();

                sb.Append("###" + serializedType.FullName + "###");

                foreach (var f in fields)
                {
                    sb.Append($":{f.Name}/{f.FieldType.AssemblyQualifiedName}");
                }

                typeName = sb.ToString();

                return;
            }
            base.BindToName(serializedType, out assemblyName, out typeName);
        }

        public override Type BindToType(string? assemblyName, string fullTypeName)
        {
            if (fullTypeName.StartsWith("###"))
            {
                try
                {
                    var ts = fullTypeName.Split("###").Where(t => !string.IsNullOrEmpty(t)).ToArray();

                    var fs = ts[1].Split(":").Skip(1).Select(x =>
                    {
                        var xs = x.Split("/");
                        return (xs[0], TypeHelper.GetTypeFromFullName(xs[1])!);
                    }).ToArray();

                    var t = _typeDataManager.ReconstructType(ts[0].Replace("+", "__"), fs);
                    if (t == null)
                    {
                        throw new NotImplementedException();
                    }
                    return t;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            return base.BindToType(assemblyName, fullTypeName);
        }

        private TypeDataManager _typeDataManager;
    }

    public static class JsonNetSerializer
    {
        public static object Serialize<T>(Expression<T> f)
        {
            var typeDataManager = new TypeDataManager();
            var data = ExpressionData.Pack(f, typeDataManager);

            var s = JsonConvert.SerializeObject(data, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Objects,
                SerializationBinder = new CustomSerializationBinder(typeDataManager)
            });

            Debug.WriteLine($"1: {s}");
            var s2 = JsonConvert.SerializeObject(new ExpressionPacket(s, typeDataManager.Types), new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Objects });

            Debug.WriteLine($"2: {s2}");

            return s2;
        }

        public static T Deserialize<T>(object o) where T : Delegate
        {
            var typeDataManager = new TypeDataManager();
            var packet = JsonConvert.DeserializeObject<ExpressionPacket>((string)o, new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Objects });

            typeDataManager.LoadTypes(packet.Types);

            var data = JsonConvert.DeserializeObject<ExpressionData>(packet.Data, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Objects,
                SerializationBinder = new CustomSerializationBinder(typeDataManager)
            });

            var t2 = (LambdaExpression)data.Unpack(typeDataManager);

            return (T)t2.Compile();
        }
    }
}
