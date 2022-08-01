using System;
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
        public CustomSerializationBinder(TypeDataManager typeDataManager)
        {
            _typeDataManager = typeDataManager;
        }

        public override void BindToName(
            Type serializedType, out string? assemblyName, out string? typeName)
        {
            if (typeof(ExpressionData).IsAssignableFrom(serializedType) || serializedType == typeof(MemberInfoData) || serializedType == typeof(MethodInfoData))
            {
                assemblyName = null;
                typeName = $"@{(int)serializedType.GetExpressionDataType()}";
                return;
            }
            else if (serializedType.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Any())
            {
                assemblyName = null;
                typeName = $"#{_typeDataManager.Pack(serializedType)}";

                return;
            }

            base.BindToName(serializedType, out assemblyName, out typeName);
        }

        public override Type BindToType(string? assemblyName, string fullTypeName)
        {
            try
            {
                switch (fullTypeName[0])
                {
                    case '@':
                        return ((TypeSerialization)int.Parse(fullTypeName.Substring(1)!)).GetTypeFromExpressionDataType();

                    case '#':
                        return _typeDataManager.UnpackFromName(fullTypeName.Substring(1));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            return base.BindToType(assemblyName, fullTypeName);
        }

        private TypeDataManager _typeDataManager;
    }


    public enum TypeSerialization
    {
        Default = 1,
        Lambda,
        Parameter,
        Binary,
        Member,
        Constant,
        MethodCall,
        New,
        NewArray,
        MemberInfo,
        MethodInfo,
    }

    public static class ExpressionDataTypesExtensions
    {
        public static TypeSerialization GetExpressionDataType(this Type self) => self.Name switch
        {
            "LambdaExpressionData" => TypeSerialization.Lambda,
            "ParameterExpressionData" => TypeSerialization.Parameter,
            "BinaryExpressionData" => TypeSerialization.Binary,
            "MemberExpressionData" => TypeSerialization.Member,
            "ConstantExpressionData" => TypeSerialization.Constant,
            "MethodCallExpressionData" => TypeSerialization.MethodCall,
            "NewExpressionData" => TypeSerialization.New,
            "NewArrayExpressionData" => TypeSerialization.NewArray,
            "MemberInfoData" => TypeSerialization.MemberInfo,
            "MethodInfoData" => TypeSerialization.MethodInfo,
            _ => throw new NotSupportedException()
        };

        public static Type GetTypeFromExpressionDataType(this TypeSerialization self) => self switch
        {
            TypeSerialization.Lambda => typeof(LambdaExpressionData),
            TypeSerialization.Parameter => typeof(ParameterExpressionData),
            TypeSerialization.Binary => typeof(BinaryExpressionData),
            TypeSerialization.Member => typeof(MemberExpressionData),
            TypeSerialization.Constant => typeof(ConstantExpressionData),
            TypeSerialization.MethodCall => typeof(MethodCallExpressionData),
            TypeSerialization.New => typeof(NewExpressionData),
            TypeSerialization.NewArray => typeof(NewArrayExpressionData),
            TypeSerialization.MemberInfo => typeof(MemberInfoData),
            TypeSerialization.MethodInfo => typeof(MethodInfoData),
            _ => throw new NotSupportedException()
        };
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
            var s2 = JsonConvert.SerializeObject(new ExpressionPacket(EncodeBase64(s), typeDataManager.Types), new JsonSerializerSettings());

            Debug.WriteLine($"2: {s2}");

            return s2;
        }

        public static T Deserialize<T>(object o) where T : Delegate
        {
            var typeDataManager = new TypeDataManager();
            var packet = JsonConvert.DeserializeObject<ExpressionPacket>((string)o, new JsonSerializerSettings());

            typeDataManager.LoadTypes(packet.Types);

            var data = JsonConvert.DeserializeObject<ExpressionData>(DecodeBase64(packet.Data), new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Objects,
                SerializationBinder = new CustomSerializationBinder(typeDataManager)
            });

            var t2 = (LambdaExpression)data.Unpack(typeDataManager);

            return (T)t2.Compile();
        }

        private static string EncodeBase64(string s)
        {
            var bb = Encoding.UTF8.GetBytes(s);
            return Convert.ToBase64String(bb);
        }

        private static string DecodeBase64(string s)
        {
            var dd = Convert.FromBase64String(s);
            return Encoding.UTF8.GetString(dd);
        }
    }
}
