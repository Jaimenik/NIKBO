using System.Linq.Expressions;
using NikSBO.models;
using NikSBO.Query;

namespace NikSBO.Tests;

public class ODataExpressionTranslatorTests
{
    private readonly ODataExpressionTranslator _sut = new();

    [Fact]
    public void Igualdad_de_string_genera_eq_con_comillas()
    {
        Expression<Func<BusinessPartner, bool>> p = bp => bp.CardCode == "C001";

        var result = _sut.Translate(p.Body);

        Assert.Equal("(CardCode eq 'C001')", result);
    }

    [Fact]
    public void Desigualdad_genera_ne()
    {
        Expression<Func<BusinessPartner, bool>> p = bp => bp.CardType != "cCustomer";

        var result = _sut.Translate(p.Body);

        Assert.Equal("(CardType ne 'cCustomer')", result);
    }

    [Fact]
    public void Comparaciones_numericas_generan_operadores_correctos()
    {
        Expression<Func<Item, bool>> mayor = item => item.PriceList > 5;
        Expression<Func<Item, bool>> menorIgual = item => item.PriceList <= 10;

        Assert.Equal("(PriceList gt 5)", _sut.Translate(mayor.Body));
        Assert.Equal("(PriceList le 10)", _sut.Translate(menorIgual.Body));
    }

    [Fact]
    public void AndAlso_genera_and_entre_los_operandos()
    {
        Expression<Func<BusinessPartner, bool>> p =
            bp => bp.CardType == "cCustomer" && bp.CardCode == "C001";

        var result = _sut.Translate(p.Body);

        Assert.Equal("((CardType eq 'cCustomer') and (CardCode eq 'C001'))", result);
    }

    [Fact]
    public void OrElse_genera_or_entre_los_operandos()
    {
        Expression<Func<BusinessPartner, bool>> p =
            bp => bp.CardCode == "C001" || bp.CardCode == "C002";

        var result = _sut.Translate(p.Body);

        Assert.Equal("((CardCode eq 'C001') or (CardCode eq 'C002'))", result);
    }

    [Fact]
    public void Contains_genera_substringof_con_valor_primero()
    {
        Expression<Func<BusinessPartner, bool>> p = bp => bp.CardName.Contains("Pepe");

        var result = _sut.Translate(p.Body);

        Assert.Equal("substringof('Pepe', CardName)", result);
    }

    [Fact]
    public void StartsWith_y_EndsWith_generan_funciones_correspondientes()
    {
        Expression<Func<BusinessPartner, bool>> starts = bp => bp.CardCode.StartsWith("C");
        Expression<Func<BusinessPartner, bool>> ends = bp => bp.EmailAddress.EndsWith(".com");

        Assert.Equal("startswith(CardCode, 'C')", _sut.Translate(starts.Body));
        Assert.Equal("endswith(EmailAddress, '.com')", _sut.Translate(ends.Body));
    }

    [Fact]
    public void Variable_de_closure_se_resuelve_a_su_valor()
    {
        var nombre = "Pepe";
        Expression<Func<BusinessPartner, bool>> p = bp => bp.CardName == nombre;

        var result = _sut.Translate(p.Body);

        Assert.Equal("(CardName eq 'Pepe')", result);
    }

    [Fact]
    public void Apostrofo_en_string_se_escapa_duplicandolo()
    {
        Expression<Func<BusinessPartner, bool>> p = bp => bp.CardName == "O'Brien";

        var result = _sut.Translate(p.Body);

        Assert.Equal("(CardName eq 'O''Brien')", result);
    }

    [Fact]
    public void Decimal_usa_punto_no_coma_independientemente_de_la_cultura()
    {
        var cultura = System.Threading.Thread.CurrentThread.CurrentCulture;
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-ES");
        try
        {
            var precio = 12.5;
            Expression<Func<Item, bool>> p = item => precio > 10;

            var result = _sut.Translate(p.Body);

            Assert.Equal("(12.5 gt 10)", result);
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = cultura;
        }
    }
}
