using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
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
                    sb.Append(":" + f.Name + "/" + f.FieldType.FullName);
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
                    var ts = fullTypeName.Split("###");

                    var fs = ts[2].Split(":").Skip(1).Select(x =>
                    {
                        var xs = x.Split("/");
                        return (xs[0], Type.GetType(xs[1])!);
                    }).ToArray();

                    var t = TypeData.TypeDataManager.ReconstructType(ts[1].Replace("+", "__"), fs);
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
    }

    public static class JsonNetSerializer
    {
        public static object Serialize<T>(Expression<T> f)
        {
            var data = ExpressionData.Pack(f);

            var s = JsonConvert.SerializeObject(data, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Objects,
                SerializationBinder = new CustomSerializationBinder()
            });

            return s;
        }

        public static Expression<T> Deserialize<T>(object o) where T : Delegate
        {
            var data2 = JsonConvert.DeserializeObject<ExpressionData>((string)o, new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Objects,
                SerializationBinder = new CustomSerializationBinder() 
            });

            return (Expression<T>)data2!.Unpack();
        }
    }
}
