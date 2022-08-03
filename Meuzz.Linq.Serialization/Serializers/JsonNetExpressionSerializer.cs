using System;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;
using Meuzz.Linq.Serialization.Core;
using Meuzz.Linq.Serialization.Core.Expressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Meuzz.Linq.Serialization.Serializers
{
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

    static class ExpressionDataTypesExtensions
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

    /// <summary>
    ///   Json.NETを使用してシリアライズを行うくらす。
    /// </summary>
    class JsonNetSerializer : ExpressionSerializer
    {
        /// <inheritdoc/>
        public override string Serialize<T>(Expression<T> f)
        {
            var typeDataManager = new TypeDataManager();
            var data = ExpressionData.Pack(f, typeDataManager);

            var s = JsonConvert.SerializeObject(data, new JsonSerializerSettings()
            {
                // ContractResolver = new PrivateContractResolver(),
                TypeNameHandling = TypeNameHandling.Objects,
                SerializationBinder = new CustomSerializationBinder(typeDataManager)
            });

            //Debug.WriteLine($"1: {s}");
            var s2 = JsonConvert.SerializeObject(new ExpressionPacket(EncodeBase64(s), typeDataManager.Types), new JsonSerializerSettings());

            //Debug.WriteLine($"2: {s2}");
            return s2;
        }

        /// <inheritdoc/>
        /// <exception cref="NotImplementedException"></exception>
        public override T Deserialize<T>(string s)
        {
            var typeDataManager = new TypeDataManager();
            var packet = JsonConvert.DeserializeObject<ExpressionPacket>(s, new JsonSerializerSettings());
            if (packet == null)
            {
                throw new NotImplementedException();
            }

            typeDataManager.LoadTypes(packet.Types);

            var data = JsonConvert.DeserializeObject<ExpressionData>(DecodeBase64(packet.Data), new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Objects,
                SerializationBinder = new CustomSerializationBinder(typeDataManager)
            });

            if (data == null)
            {
                throw new NotImplementedException();
            }

            var t2 = (LambdaExpression)data.Unpack(typeDataManager);

            return (T)t2.Compile();
        }

        /// <summary>
        ///   base64形式のエンコーディングを行う。
        /// </summary>
        /// <param name="s">文字列。</param>
        /// <returns>base64エンコードされた文字列データ。</returns>
        private static string EncodeBase64(string s)
        {
            var bb = Encoding.UTF8.GetBytes(s);
            return Convert.ToBase64String(bb);
        }

        /// <summary>
        ///   base64形式のデコーディングを行う。
        /// </summary>
        /// <param name="s">base64エンコードされた文字列型データ。</param>
        /// <returns>デコードされた文字列型データ。</returns>
        private static string DecodeBase64(string s)
        {
            var dd = Convert.FromBase64String(s);
            return Encoding.UTF8.GetString(dd);
        }

#if false
        class PrivateContractResolver : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                MemberInfo[] fields = objectType.GetFields(flags);
                return fields
                    .Concat(objectType.GetProperties(flags).Where(propInfo => propInfo.CanWrite))
                    .ToList();
            }

            protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
            {
                return base.CreateProperties(type, MemberSerialization.Fields);
            }
        }
#endif

        /// <summary>
        ///   Json.NETにおけるJSONエンコーディングのカスタマイズを行うクラス。
        /// </summary>
        class CustomSerializationBinder : DefaultSerializationBinder
        {
            public CustomSerializationBinder(TypeDataManager typeDataManager)
            {
                _typeDataManager = typeDataManager;
            }

            /// <inheritdoc/>
            public override void BindToName(Type serializedType, out string? assemblyName, out string? typeName)
            {
                if (typeof(ExpressionData).IsAssignableFrom(serializedType) || serializedType == typeof(MemberInfoData) || serializedType == typeof(MethodInfoData))
                {
                    assemblyName = null;
                    typeName = $"@{(int)serializedType.GetExpressionDataType()}";
                    return;
                }
                else if (_typeDataManager.IsUsingFieldSpecs(serializedType) || serializedType.FullName?.Contains('+') == true)
                {
                    assemblyName = null;
                    typeName = $"#{_typeDataManager.Pack(serializedType, true)}";
                    return;
                }

                base.BindToName(serializedType, out assemblyName, out typeName);
            }

            /// <inheritdoc/>
            public override Type BindToType(string? assemblyName, string fullTypeName)
            {
                try
                {
                    switch (fullTypeName[0])
                    {
                        case '@':
                            return ((TypeSerialization)int.Parse(fullTypeName.Substring(1))).GetTypeFromExpressionDataType();

                        case '#':
                            return _typeDataManager.UnpackFromKey(fullTypeName.Substring(1));
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
    }
}
