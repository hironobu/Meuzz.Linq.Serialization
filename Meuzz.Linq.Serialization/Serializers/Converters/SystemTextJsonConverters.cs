﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Meuzz.Linq.Serialization.Core;
using Meuzz.Linq.Serialization.Core.Expressions;

namespace Meuzz.Linq.Serialization.Serializers.Converters
{
    // NOTICE:
    //
    // System.Text.Json版シリアライザーの実装に先行して用意したJsonConverterクラス。
    // 現時点(バージョン0系)では使用していない。

    public static class TypeHelper
    {
        public static Type? GetTypeFromFullName(string fullTypeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullTypeName);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }
    }

    public class TypeDataJsonConverter : JsonConverter<TypeData>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(TypeData).IsAssignableFrom(typeToConvert);
        }

        public override TypeData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Key")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }
            var key = reader.GetString();

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Type")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            var type = reader.GetString();

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Fields")
            {
                throw new JsonException();
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            var specs = new List<FieldData>();
            while (true)
            {
                if (reader.Read() && reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }

                var k = reader.GetString();
                if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException();
                }

                var fieldType = reader.GetString();
                var value = reader.GetString(); //  TypeHelper.GetTypeFromFullName(fieldType);
                if (value == null)
                {
                    throw new NotImplementedException();
                }
                specs.Add(new FieldData() { Name = k, TypeKey = value });
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            {
                throw new JsonException();
            }

            return new TypeData()
            {
                Key = key,
                FullName = type,
                Fields = specs.ToArray(),
            };
        }

        public override void Write(Utf8JsonWriter writer, TypeData value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("Key", value.Key);
            writer.WriteString("Type", value.FullName);

            writer.WritePropertyName("Fields");
            writer.WriteStartObject();
            foreach (var spec in value.Fields)
            {
                var type = spec.TypeKey;

                writer.WriteString(spec.Name, type);
            }
            writer.WriteEndObject();

            writer.WriteEndObject();
        }
    }

    public class PacketJsonConverter : JsonConverter<ExpressionPacket>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(ExpressionPacket).IsAssignableFrom(typeToConvert);
        }

        public override ExpressionPacket Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Type")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException();
            }

            var typeDatas = new List<TypeData>();
            while (true)
            {
                if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                var t = (TypeData)JsonSerializer.Deserialize(ref reader, typeof(TypeData), options);
                typeDatas.Add(t);
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Data")
            {
                throw new JsonException();
            }
            // var data = JsonSerializer.Deserialize(ref reader, typeof(ExpressionData), options);
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }
            var data = reader.GetString();

            if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            {
                throw new JsonException();
            }

            return new ExpressionPacket(data, typeDatas);
        }

        public override void Write(Utf8JsonWriter writer, ExpressionPacket value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("Type");
            writer.WriteStartArray();
            if (value.Types != null)
            {
                foreach (var x in value.Types)
                {
                    JsonSerializer.Serialize(writer, x, options);
                }
            }
            writer.WriteEndArray();

            writer.WriteString("Data", value.Data);
            //JsonSerializer.Serialize(writer, value.Data, options);
            writer.WriteEndObject();
        }
    }

    public class MemberInfoDataJsonConverter : JsonConverter<MemberInfoData>
    {
        public MemberInfoDataJsonConverter(TypeDataManager typeDataManager)
        {
            _typeDataManager = typeDataManager;
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(MemberInfoData).IsAssignableFrom(typeToConvert);
        }

        public override MemberInfoData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Name")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }
            var name = reader.GetString();

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "DeclaringType")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }
            var declaringType = reader.GetString();

            if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            {
                throw new JsonException();
            }

            return new MemberInfoData()
            {
                MemberString = name,
                DeclaringType = declaringType, //  new TypeData() { FullQualifiedTypeString = declaringType },
            };
        }

        public override void Write(Utf8JsonWriter writer, MemberInfoData value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("Name", value.MemberString);
            writer.WriteString("DeclaringType", value.DeclaringType);
            writer.WriteEndObject();
        }

        private TypeDataManager _typeDataManager;
    }

    public class MethodInfoDataJsonConverter : JsonConverter<MethodInfoData>
    {
        public MethodInfoDataJsonConverter(TypeDataManager typeDataManager)
        {
            _typeDataManager = typeDataManager;
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(MethodInfoData).IsAssignableFrom(typeToConvert);
        }

        public override MethodInfoData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Name")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }
            var name = reader.GetString();

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "DeclaringType")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }
            var declaringType = reader.GetString();

            int? genericParameterCount = null;
            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "GenericParameterCount")
            {
                throw new JsonException();
            }
            if (!reader.Read())
            {
                throw new JsonException();
            }
            if (reader.TokenType == JsonTokenType.Number)
            {
                genericParameterCount = reader.GetInt32();
            }
            else if (reader.TokenType != JsonTokenType.Null)
            {
                throw new JsonException();
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "GenericParameterTypes")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException();
            }

            var genericParameterTypes = new List<string>();
            while (true)
            {
                if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException();
                }

                var t = reader.GetString();
                genericParameterTypes.Add(t);
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Type")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException();
            }

            var types = new List<string>();
            while (true)
            {
                if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException();
                }

                var t = reader.GetString();
                types.Add(t);
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            {
                throw new JsonException();
            }

            return new MethodInfoData()
            {
                Name = name,
                DeclaringType = declaringType,
                GenericParameterCount = genericParameterCount,
                GenericParameterTypes = genericParameterTypes.ToArray(),
                Types = types,
            };
        }

        public override void Write(Utf8JsonWriter writer, MethodInfoData value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("Name", value.Name);
            writer.WriteString("DeclaringType", value.DeclaringType);
            writer.WritePropertyName("GenericParameterCount");
            if (value.GenericParameterCount != null)
            {
                writer.WriteNumberValue((int)value.GenericParameterCount);
            }
            else
            {
                writer.WriteNullValue();
            }
            writer.WritePropertyName("GenericParameterTypes");
            writer.WriteStartArray();
            if (value.GenericParameterTypes != null)
            {
                foreach (var x in value.GenericParameterTypes)
                {
                    writer.WriteStringValue(x);
                }
            }
            writer.WriteEndArray();
            writer.WritePropertyName("Type");
            writer.WriteStartArray();
            if (value.GenericParameterTypes != null)
            {
                foreach (var x in value.Types)
                {
                    writer.WriteStringValue(x);
                }
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        private TypeDataManager _typeDataManager;
    }

    public class ExpressionDataJsonConverter : JsonConverter<ExpressionData>
    {
        public ExpressionDataJsonConverter(TypeDataManager typeDataManager)
        {
            _typeDataManager = typeDataManager;
        }

        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(ExpressionData).IsAssignableFrom(typeToConvert);
        }

        public override ExpressionData Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            ExpressionData retval;

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "NodeType")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
            {
                throw new JsonException();
            }
            var nodeType = (ExpressionType)reader.GetInt32();

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "CanReduce")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.True && reader.TokenType != JsonTokenType.False)
            {
                throw new JsonException();
            }
            var canReduce = reader.GetBoolean();

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Type")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }
            var type = reader.GetString();
            // var type = (TypeData)JsonSerializer.Deserialize(ref reader, typeof(TypeData), options);

            switch (nodeType)
            {
                case ExpressionType.Lambda:
                    {
                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Parameters")
                        {
                            throw new JsonException();
                        }
                        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
                        {
                            throw new JsonException();
                        }

                        var parameters = new List<ParameterExpressionData>();
                        while (true)
                        {
                            if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray)
                            {
                                break;
                            }

                            parameters.Add((ParameterExpressionData)JsonSerializer.Deserialize(ref reader, typeof(ParameterExpressionData), options));
                        }

                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Body")
                        {
                            throw new JsonException();
                        }
                        var body = (ExpressionData)JsonSerializer.Deserialize(ref reader, typeof(ExpressionData), options);

                        var name = string.Empty;
                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Name")
                        {
                            throw new JsonException();
                        }
                        if (!reader.Read() || reader.TokenType == JsonTokenType.String)
                        {
                            name = reader.GetString();
                        }
                        else if (reader.TokenType != JsonTokenType.Null)
                        {
                            throw new JsonException();
                        }

                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "TailCall")
                        {
                            throw new JsonException();
                        }
                        if (!reader.Read() || reader.TokenType != JsonTokenType.True && reader.TokenType != JsonTokenType.False)
                        {
                            throw new JsonException();
                        }
                        var tailCall = reader.GetBoolean();

                        retval = new LambdaExpressionData()
                        {
                            NodeType = nodeType,
                            Type = type,
                            CanReduce = canReduce,
                            Parameters = parameters,
                            Body = body,
                            Name = name,
                            TailCall = tailCall
                        };
                    }
                    break;

                case ExpressionType.Parameter:
                    {
                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "IsByRef")
                        {
                            throw new JsonException();
                        }
                        if (!reader.Read() || reader.TokenType != JsonTokenType.True && reader.TokenType != JsonTokenType.False)
                        {
                            throw new JsonException();
                        }
                        var isByRef = reader.GetBoolean();

                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Name")
                        {
                            throw new JsonException();
                        }
                        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                        {
                            throw new JsonException();
                        }
                        var name = reader.GetString();

                        retval = new ParameterExpressionData()
                        {
                            NodeType = nodeType,
                            Type = type,
                            CanReduce = canReduce,
                            IsByRef = isByRef,
                            Name = name
                        };
                    }
                    break;

                case ExpressionType.AndAlso:
                case ExpressionType.Equal:
                case ExpressionType.GreaterThan:
                case ExpressionType.GreaterThanOrEqual:
                case ExpressionType.LessThan:
                case ExpressionType.LessThanOrEqual:
                case ExpressionType.NotEqual:
                case ExpressionType.OrElse:
                    {
                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Conversion")
                        {
                            throw new JsonException();
                        }
                        var conversion = (LambdaExpressionData)JsonSerializer.Deserialize(ref reader, typeof(LambdaExpressionData), options);

                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "IsLifted")
                        {
                            throw new JsonException();
                        }
                        if (!reader.Read() || reader.TokenType != JsonTokenType.True && reader.TokenType != JsonTokenType.False)
                        {
                            throw new JsonException();
                        }
                        var isLifted = reader.GetBoolean();

                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "IsLiftedToNull")
                        {
                            throw new JsonException();
                        }
                        if (!reader.Read() || reader.TokenType != JsonTokenType.True && reader.TokenType != JsonTokenType.False)
                        {
                            throw new JsonException();
                        }
                        var isLiftedToNull = reader.GetBoolean();

                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Left")
                        {
                            throw new JsonException();
                        }
                        var left = (ExpressionData)JsonSerializer.Deserialize(ref reader, typeof(ExpressionData), options);

                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Right")
                        {
                            throw new JsonException();
                        }
                        var right = (ExpressionData)JsonSerializer.Deserialize(ref reader, typeof(ExpressionData), options);

                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Method")
                        {
                            throw new JsonException();
                        }
                        var method = (MethodInfoData)JsonSerializer.Deserialize(ref reader, typeof(MethodInfoData), options);

                        retval = new BinaryExpressionData()
                        {
                            NodeType = nodeType,
                            Type = type,
                            CanReduce = canReduce,
                            Conversion = conversion!,
                            IsLifted = isLifted,
                            IsLiftedToNull = isLiftedToNull,
                            Left = left,
                            Right = right,
                            Method = method
                        };
                    }
                    break;

                case ExpressionType.MemberAccess:
                    {
                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Expression")
                        {
                            throw new JsonException();
                        }
                        var expression = (ExpressionData)JsonSerializer.Deserialize(ref reader, typeof(ExpressionData), options);

                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Member")
                        {
                            throw new JsonException();
                        }
                        var member = (MemberInfoData)JsonSerializer.Deserialize(ref reader, typeof(MemberInfoData), options);

                        retval = new MemberExpressionData()
                        {
                            NodeType = nodeType,
                            Type = type,
                            CanReduce = canReduce,
                            Expression = expression,
                            Member = member
                        };
                    }
                    break;

                case ExpressionType.Constant:
                    {
                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Value")
                        {
                            throw new JsonException();
                        }
                        if (!reader.Read())
                        {
                            throw new JsonException();
                        }

                        object? value = null;
                        switch (reader.TokenType)
                        {
                            case JsonTokenType.String:
                                value = reader.GetString();
                                break;

                            case JsonTokenType.Number:
                                value = reader.GetInt64();
                                break;

                            default:
                                /*{
                                    if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
                                    {
                                        throw new NotImplementedException();
                                    }
                                }*/
                                var t = _typeDataManager.UnpackFromKey(type);
                                value = JsonSerializer.Deserialize(ref reader, t, options);
                                break;
                        }

                        retval = new ConstantExpressionData()
                        {
                            NodeType = nodeType,
                            Type = type,
                            CanReduce = canReduce,
                            Value = value
                        };
                    }
                    break;

                case ExpressionType.Call:
                    {
                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Arguments")
                        {
                            throw new JsonException();
                        }
                        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
                        {
                            throw new JsonException();
                        }

                        var arguments = new List<ExpressionData>();
                        while (true)
                        {
                            if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray)
                            {
                                break;
                            }

                            arguments.Add((ExpressionData)JsonSerializer.Deserialize(ref reader, typeof(ExpressionData), options));
                        }

                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Method")
                        {
                            throw new JsonException();
                        }
                        var method = (MethodInfoData)JsonSerializer.Deserialize(ref reader, typeof(MethodInfoData), options);

                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Object")
                        {
                            throw new JsonException();
                        }
                        var callerObject = (ExpressionData)JsonSerializer.Deserialize(ref reader, typeof(ExpressionData), options);

                        retval = new MethodCallExpressionData()
                        {
                            NodeType = nodeType,
                            Type = type,
                            CanReduce = canReduce,
                            Arguments = arguments.ToArray(),
                            Method = method,
                            Object = callerObject,
                        };
                    }
                    break;

                case ExpressionType.NewArrayInit:
                    {
                        if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "Expressions")
                        {
                            throw new JsonException();
                        }
                        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
                        {
                            throw new JsonException();
                        }

                        var arguments = new List<ExpressionData>();
                        while (true)
                        {
                            if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray)
                            {
                                break;
                            }

                            arguments.Add((ExpressionData)JsonSerializer.Deserialize(ref reader, typeof(ExpressionData), options));
                        }

                        retval = new NewArrayExpressionData()
                        {
                            NodeType = nodeType,
                            Type = type,
                            CanReduce = canReduce,
                            Expressions = arguments.ToArray(),
                        };
                    }
                    break;

                default:
                    throw new NotSupportedException();
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.EndObject)
            {
                throw new JsonException();
            }

            return retval;
        }

        public override void Write(
            Utf8JsonWriter writer,
            ExpressionData value,
            JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            if (value is ExpressionData e)
            {
                writer.WriteNumber("NodeType", (int)e.NodeType);
                writer.WriteBoolean("CanReduce", e.CanReduce);
#if true
                writer.WriteString("Type", e.Type);
#else
                writer.WritePropertyName("Type");
                JsonSerializer.Serialize(writer, e.Type, options);
#endif

                switch (e)
                {
                    case LambdaExpressionData le:
                        writer.WritePropertyName("Parameters");
                        writer.WriteStartArray();
                        foreach (var x in le.Parameters)
                        {
                            JsonSerializer.Serialize(writer, x, options);
                        }
                        writer.WriteEndArray();
                        writer.WritePropertyName("Body");
                        JsonSerializer.Serialize(writer, le.Body, options);
                        writer.WriteString("Name", le.Name);
                        writer.WriteBoolean("TailCall", le.TailCall);
                        break;

                    case ParameterExpressionData pe:
                        writer.WriteBoolean("IsByRef", pe.IsByRef);
                        writer.WriteString("Name", pe.Name);
                        break;

                    case BinaryExpressionData bine:
                        writer.WritePropertyName("Conversion");
                        JsonSerializer.Serialize(writer, bine.Conversion, options);
                        writer.WriteBoolean("IsLifted", bine.IsLifted);
                        writer.WriteBoolean("IsLiftedToNull", bine.IsLiftedToNull);
                        writer.WritePropertyName("Left");
                        JsonSerializer.Serialize(writer, bine.Left, options);
                        writer.WritePropertyName("Right");
                        JsonSerializer.Serialize(writer, bine.Right, options);
                        writer.WritePropertyName("Method");
                        JsonSerializer.Serialize(writer, bine.Method, options);
                        break;

                    case MemberExpressionData membe:
                        writer.WritePropertyName("Expression");
                        JsonSerializer.Serialize(writer, membe.Expression, options);
                        writer.WritePropertyName("Member");
                        JsonSerializer.Serialize(writer, membe.Member, options);
                        break;

                    case ConstantExpressionData ce:
                        writer.WritePropertyName("Value");
                        var t = ce.Value?.GetType() ?? typeof(object);
                        if (_typeDataManager.IsUsingFieldSpecs(t))
                        {
                            writer.WriteStartObject();
                            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                            {
                                writer.WritePropertyName(f.Name);
                                var v = f.GetValue(ce.Value);
                                JsonSerializer.Serialize(writer, v, options);
                            }
                            writer.WriteEndObject();
                        }
                        else
                        {
                            JsonSerializer.Serialize(writer, ce.Value, t);
                        }
                        break;

                    case MethodCallExpressionData mce:
                        writer.WritePropertyName("Arguments");
                        writer.WriteStartArray();
                        if (mce.Arguments != null)
                        {
                            foreach (var x in mce.Arguments)
                            {
                                JsonSerializer.Serialize(writer, x, options);
                            }
                        }
                        writer.WriteEndArray();
                        writer.WritePropertyName("Method");
                        JsonSerializer.Serialize(writer, mce.Method, options);
                        writer.WritePropertyName("Object");
                        JsonSerializer.Serialize(writer, mce.Object, options);
                        break;

                    case NewExpressionData ne:
                        writer.WritePropertyName("Arguments");
                        writer.WriteStartArray();
                        if (ne.Arguments != null)
                        {
                            foreach (var x in ne.Arguments)
                            {
                                JsonSerializer.Serialize(writer, x, options);
                            }
                        }
                        writer.WriteEndArray();
                        writer.WritePropertyName("ConstructorInfo");
                        JsonSerializer.Serialize(writer, ne.ConstructorInfo, options);
                        break;

                    case NewArrayExpressionData nae:
                        writer.WritePropertyName("Expressions");
                        writer.WriteStartArray();
                        if (nae.Expressions != null)
                        {
                            foreach (var x in nae.Expressions)
                            {
                                JsonSerializer.Serialize(writer, x, options);
                            }
                        }
                        writer.WriteEndArray();
                        break;

                    default:
                        throw new NotSupportedException();
                }
            }
            else
            {
                throw new NotSupportedException();
            }

            writer.WriteEndObject();
        }

        private TypeDataManager _typeDataManager;
    }
}
