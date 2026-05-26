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
    [JsonConverter(typeof(SapEnumConverter<UserTableType>))]
    public enum UserTableType
    {
        [EnumMember(Value = "bott_NoObject")]        NoObject,
        [EnumMember(Value = "bott_MasterData")]      MasterData,
        [EnumMember(Value = "bott_MasterDataLines")] MasterDataLines,
        [EnumMember(Value = "bott_Document")]        Document,
        [EnumMember(Value = "bott_DocumentLines")]   DocumentLines
    }
}
