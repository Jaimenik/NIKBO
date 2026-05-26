using NikSBO.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NikSBO.Tests
{
    public class MetadataTests
    {
        [Fact]
        public void UserFieldType_Alphanumeric_serializa_como_db_Alpha()
        {
            var json = JsonSerializer.Serialize(UserFieldType.Alphanumeric);
            Assert.Equal("\"db_Alpha\"", json);
        }

        [Fact]
        public void String_db_Alpha_deserializa_a_Alphanumeric()
        {
            var value = JsonSerializer.Deserialize<UserFieldType>("\"db_Alpha\"");
            Assert.Equal(UserFieldType.Alphanumeric, value);
        }

        [Fact]
        public void UserTableType_MasterData_serializa_como_bott_MasterData()
    => Assert.Equal("\"bott_MasterData\"", JsonSerializer.Serialize(UserTableType.MasterData));

        [Fact]
        public void UserFieldSubType_Address_serializa_como_st_Address()
            => Assert.Equal("\"st_Address\"", JsonSerializer.Serialize(UserFieldSubType.Address));

        [Fact]
        public void UserObjectType_Document_serializa_como_boud_Document()
            => Assert.Equal("\"boud_Document\"", JsonSerializer.Serialize(UserObjectType.Document));
    }
}
