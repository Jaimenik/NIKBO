using System.Globalization;
using System.Threading;
using NikSBO.http;

namespace NikSBO.Tests;

public class SqlParametersTests
{
    // ---- FormatSqlParam ----

    [Fact]
    public void Formatea_string_entre_comillas_simples()
    {
        Assert.Equal("'C001'", B1Client.FormatSqlParam("C001"));
    }

    [Fact]
    public void Escapa_apostrofo_duplicandolo()
    {
        Assert.Equal("'O''Brien'", B1Client.FormatSqlParam("O'Brien"));
    }

    [Fact]
    public void Formatea_int_sin_comillas()
    {
        Assert.Equal("42", B1Client.FormatSqlParam(42));
        Assert.Equal("0", B1Client.FormatSqlParam(0));
        Assert.Equal("-7", B1Client.FormatSqlParam(-7));
    }

    [Fact]
    public void Formatea_bool_como_true_o_false_minusculas()
    {
        Assert.Equal("true", B1Client.FormatSqlParam(true));
        Assert.Equal("false", B1Client.FormatSqlParam(false));
    }

    [Fact]
    public void Decimal_y_double_usan_punto_independientemente_de_la_cultura()
    {
        var cultura = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = new CultureInfo("es-ES");
        try
        {
            Assert.Equal("12.5", B1Client.FormatSqlParam(12.5));
            Assert.Equal("99.99", B1Client.FormatSqlParam(99.99m));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = cultura;
        }
    }

    [Fact]
    public void DateTime_se_formatea_como_ISO_8601_entre_comillas()
    {
        var dt = new DateTime(2024, 1, 15, 10, 30, 0);
        Assert.Equal("'2024-01-15T10:30:00'", B1Client.FormatSqlParam(dt));
    }

    [Fact]
    public void Null_se_formatea_como_null_literal()
    {
        Assert.Equal("null", B1Client.FormatSqlParam(null));
    }

    // ---- BuildSqlQueryString ----

    [Fact]
    public void BuildQueryString_un_solo_parametro_lleva_interrogante_inicial()
    {
        var url = B1Client.BuildSqlQueryString(new { code = "C001" });
        Assert.Equal("?code=%27C001%27", url);  // %27 es ' URL-encoded
    }

    [Fact]
    public void BuildQueryString_varios_parametros_separados_por_amp()
    {
        var url = B1Client.BuildSqlQueryString(new { code = "C001", type = "C" });
        Assert.Equal("?code=%27C001%27&type=%27C%27", url);
    }

    [Fact]
    public void BuildQueryString_acepta_diccionario_con_string_keys()
    {
        var dict = new Dictionary<string, object> { ["id"] = 42, ["active"] = true };
        var url = B1Client.BuildSqlQueryString(dict);
        // El orden del diccionario es determinístico en .NET moderno
        Assert.Contains("id=42", url);
        Assert.Contains("active=true", url);
    }

    [Fact]
    public void BuildQueryString_url_encodea_los_caracteres_especiales_del_valor()
    {
        // Espacios, caracteres no ASCII, etc.
        var url = B1Client.BuildSqlQueryString(new { name = "García & Cía" });
        // 'García & Cía' → URL encode → comillas simples como %27, & como %26, espacio como %20
        Assert.StartsWith("?name=%27", url);
        Assert.Contains("%20", url);   // espacio
        Assert.Contains("%26", url);   // ampersand del valor (escapado, no separador de parámetros)
    }
}
