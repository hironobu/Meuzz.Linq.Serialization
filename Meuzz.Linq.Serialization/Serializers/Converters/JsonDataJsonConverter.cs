using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Meuzz.Linq.Serialization.Core;

namespace Meuzz.Linq.Serialization.Serializers.Converters
{
    public class JsonDataJsonConverter : JsonConverter<JsonData>
    {
        public override bool CanConvert(Type typeToConvert)
        {
            return typeof(JsonData).IsAssignableFrom(typeToConvert);
        }

        public override JsonData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.PropertyName || reader.GetString() != "$type")
            {
                throw new JsonException();
            }
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }
            var type = reader.GetString();

            var data = new Dictionary<string, object>();
            while (true)
            {
                if (!reader.Read())
                {
                    throw new JsonException();
                }

                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    break;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }
            }

            return new JsonData()
            {
                Type = type,
                Data = data,
            };
        }

        public override void Write(Utf8JsonWriter writer, JsonData value, JsonSerializerOptions options) => throw new NotImplementedException();
    }
}
