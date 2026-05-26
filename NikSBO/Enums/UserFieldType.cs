using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using NikSBO.Json;
namespace NikSBO.Enums
{
    // Se pasa el tipo cerrado porque en C# se necesitan el tipo en concreto en tiempo de compilación porque sino se raya mazo bro
    [JsonConverter(typeof(SapEnumConverter<UserFieldType>))]
    public enum UserFieldType
    {
        [EnumMember(Value = "db_Alpha")] Alphanumeric,
        [EnumMember(Value = "db_Numeric")] Numeric,
        [EnumMember(Value = "db_Date")] Date,
        [EnumMember(Value = "db_Float")] Float,
        [EnumMember(Value = "db_Memo")] Memo

    }
}
