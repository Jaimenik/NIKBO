using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NikSBO.Json;

namespace NikSBO.Enums
{
    [JsonConverter(typeof(SapEnumConverter<UserFieldSubType>))]
    public enum UserFieldSubType
    {
        [EnumMember(Value = "st_None")]        None,
        [EnumMember(Value = "st_Address")]     Address,
        [EnumMember(Value = "st_Phone")]       Phone,
        [EnumMember(Value = "st_Time")]        Time,
        [EnumMember(Value = "st_Percentage")]  Percentage,
        [EnumMember(Value = "st_Measurement")] Measurement,
        [EnumMember(Value = "st_Link")]        Link,
        [EnumMember(Value = "st_Image")]       Image,
        [EnumMember(Value = "st_Price")]       Price,
        [EnumMember(Value = "st_Quantity")]    Quantity,
        [EnumMember(Value = "st_Sum")]         Sum,
        [EnumMember(Value = "st_Rate")]        Rate
    }
}
