using System;
using System.Linq.Expressions;
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
            base.BindToName(serializedType, out assemblyName, out typeName);
        }

        public override Type BindToType(string? assemblyName, string fullTypeName)
        {
            if (fullTypeName.IndexOf("DisplayClass") != -1)
            {
                try
                {
                    var t = TypeData.TypeDataManager.ReconstructType(fullTypeName.Replace("+", "__"));
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
