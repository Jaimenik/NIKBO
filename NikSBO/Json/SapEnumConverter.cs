using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NikSBO.Json
{
    internal class SapEnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        private static readonly Dictionary<T, string> _toSap;
        private static readonly Dictionary<string, T> _fromSap;

        static SapEnumConverter()
        {
            _toSap = new Dictionary<T, string>();
            _fromSap = new Dictionary<string, T>();

            foreach (var value in (T[])Enum.GetValues(typeof(T)))
            {
                string name = value.ToString();
                FieldInfo? field = typeof(T).GetField(name);
                EnumMemberAttribute? attr = field?.GetCustomAttribute<EnumMemberAttribute>();

                if (attr == null || string.IsNullOrEmpty(attr.Value)) {
                    throw new InvalidOperationException($"El valor '{value}' de {typeof(T).Name} no tiene [EnumMember(Value=...)]");
                }

                _toSap[value] = attr.Value;
                _fromSap[attr.Value] = value;
            }

        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var sap = reader.GetString();
            if (!_fromSap.TryGetValue(sap, out var value))
                throw new JsonException($"Valor '{sap}' no es válido para {typeof(T).Name}");

            return value;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var sap = _toSap[value];           
            writer.WriteStringValue(sap);   
        }
    }
}