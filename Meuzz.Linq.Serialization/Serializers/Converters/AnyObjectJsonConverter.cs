using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meuzz.Linq.Serialization.Serializers.Converters
{
    public class AnyObjectJsonConverter<T> : JsonConverter<T> where T : notnull
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(T).IsAssignableFrom(typeToConvert);
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("$type", value.GetType().FullName);
            foreach (var f in value.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic))
            {
                writer.WritePropertyName(f.Name);
                var v = f.GetValue(value);
                if (v == null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    switch (v)
                    {
                        case int n:
                            writer.WriteNumberValue(n);
                            break;
                        case long l:
                            writer.WriteNumberValue(l);
                            break;
                        case DateTime dt:
                            writer.WriteStringValue(dt);
                            break;
                        case string s:
                            writer.WriteStringValue(s);
                            break;
                        default:
                            JsonSerializer.Serialize(v, v.GetType(), options);
                            break;
                    }
                }
            }
            writer.WriteEndObject();
        }
    }
}
