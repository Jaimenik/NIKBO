using NikSBO.Enums;
using NikSBO.models;
using NikSBO.Query;

namespace NikSBO.Tests;

public class B1QueryTests
{
    private static B1Query<T> NewQuery<T>() => new(b1: null!, endpoint: "b1s/v1/BusinessPartners");

    [Fact]
    public void Sin_operadores_devuelve_endpoint_sin_querystring()
    {
        var url = NewQuery<BusinessPartner>().BuildUrl();

        Assert.Equal("b1s/v1/BusinessPartners", url);
    }

    [Fact]
    public void Top_anyade_parametro_top()
    {
        var url = NewQuery<BusinessPartner>().Top(50).BuildUrl();

        Assert.Equal("b1s/v1/BusinessPartners?$top=50", url);
    }

    [Fact]
    public void Where_lambda_genera_filter_url_encoded()
    {
        var url = NewQuery<BusinessPartner>()
            .Where(bp => bp.CardType == "cCustomer")
            .BuildUrl();

        Assert.Contains("$filter=", url);
        Assert.Contains("CardType", url);
        Assert.Contains("eq", url);
        Assert.Contains("cCustomer", url);
    }

    [Fact]
    public void Varias_clausulas_Where_se_unen_con_and()
    {
        var query = NewQuery<BusinessPartner>()
            .Where("CardType", BOCondition.Equals, "cCustomer")
            .Where("CardCode", BOCondition.Equals, "C001");

        var url = Uri.UnescapeDataString(query.BuildUrl());

        Assert.Contains("CardType eq 'cCustomer' and CardCode eq 'C001'", url);
    }

    [Fact]
    public void Select_con_strings_genera_select_separado_por_comas()
    {
        var query = NewQuery<BusinessPartner>().Select("CardCode", "CardName");

        var url = Uri.UnescapeDataString(query.BuildUrl());

        Assert.Contains("$select=CardCode,CardName", url);
    }

    [Fact]
    public void Select_con_lambda_extrae_el_nombre_de_la_propiedad()
    {
        var query = NewQuery<BusinessPartner>()
            .Select(bp => bp.CardCode, bp => bp.CardName);

        var url = Uri.UnescapeDataString(query.BuildUrl());

        Assert.Contains("$select=CardCode,CardName", url);
    }

    [Fact]
    public void Select_con_tipo_anonimo_extrae_todos_los_nombres()
    {
        var query = NewQuery<BusinessPartner>()
            .Select(bp => new { bp.CardCode, bp.CardName, bp.EmailAddress });

        var url = Uri.UnescapeDataString(query.BuildUrl());

        Assert.Contains("$select=CardCode,CardName,EmailAddress", url);
    }

    [Fact]
    public void OrderBy_genera_orderby_ascendente()
    {
        var url = Uri.UnescapeDataString(NewQuery<BusinessPartner>().OrderBy("CardCode").BuildUrl());

        Assert.Contains("$orderby=CardCode asc", url);
    }

    [Fact]
    public void OrderByDesc_genera_orderby_descendente()
    {
        var url = Uri.UnescapeDataString(NewQuery<BusinessPartner>().OrderByDesc("CardCode").BuildUrl());

        Assert.Contains("$orderby=CardCode desc", url);
    }

    [Fact]
    public void Count_aplica_sufijo_count_al_endpoint()
    {
        var url = NewQuery<BusinessPartner>().BuildUrl(count: true);

        Assert.Equal("b1s/v1/BusinessPartners/$count", url);
    }

    [Fact]
    public void Combinacion_completa_incluye_todos_los_parametros()
    {
        var query = NewQuery<BusinessPartner>()
            .Where(bp => bp.CardType == "cCustomer")
            .Select(bp => new { bp.CardCode, bp.CardName })
            .OrderBy("CardCode")
            .Top(50);

        var url = Uri.UnescapeDataString(query.BuildUrl());

        Assert.Contains("$filter=", url);
        Assert.Contains("$select=CardCode,CardName", url);
        Assert.Contains("$orderby=CardCode asc", url);
        Assert.Contains("$top=50", url);
    }
}
