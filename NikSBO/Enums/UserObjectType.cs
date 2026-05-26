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
    [JsonConverter(typeof(SapEnumConverter<UserObjectType>))]
    public enum UserObjectType
    {
        [EnumMember(Value = "boud_MasterData")] MasterData,
        [EnumMember(Value = "boud_Document")]   Document
    }
}
