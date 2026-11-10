using FluentAssertions;
using OficinaMecanica.Seguranca.Domain.Common;

namespace OficinaMecanica.Seguranca.Domain.Tests.Common;

public class ValueObjectTests
{
    private sealed class Dinheiro : ValueObject
    {
        public decimal Valor { get; }
        public string Moeda { get; }

        public Dinheiro(decimal valor, string moeda)
        {
            Valor = valor;
            Moeda = moeda;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Valor;
            yield return Moeda;
        }
    }

    private sealed class Peso : ValueObject
    {
        public decimal Valor { get; }

        public Peso(decimal valor) => Valor = valor;

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Valor;
        }
    }

    [Fact]
    public void Equals_ComMesmosComponentes_DeveSerIgual()
    {
        var a = new Dinheiro(10m, "BRL");
        var b = new Dinheiro(10m, "BRL");

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_ComComponentesDiferentes_NaoDeveSerIgual()
    {
        new Dinheiro(10m, "BRL").Equals(new Dinheiro(10m, "USD")).Should().BeFalse();
    }

    [Fact]
    public void Equals_ComTiposDiferentes_NaoDeveSerIgual()
    {
        new Dinheiro(10m, "BRL").Equals(new Peso(10m)).Should().BeFalse();
    }

    [Fact]
    public void Equals_ComNulo_NaoDeveSerIgual()
    {
        new Dinheiro(10m, "BRL").Equals(null).Should().BeFalse();
    }

    [Fact]
    public void OperadorIgualdade_ComDoisNulos_DeveSerVerdadeiro()
    {
        Dinheiro? a = null;
        Dinheiro? b = null;

        (a == b).Should().BeTrue();
    }

    [Fact]
    public void OperadorIgualdade_ComUmNulo_DeveSerFalso()
    {
        Dinheiro? nulo = null;
        var valor = new Dinheiro(10m, "BRL");

        (nulo == valor).Should().BeFalse();
        (valor == nulo).Should().BeFalse();
    }

    [Fact]
    public void OperadorIgualdade_ComMesmosComponentes_DeveSerVerdadeiro()
    {
        (new Dinheiro(10m, "BRL") == new Dinheiro(10m, "BRL")).Should().BeTrue();
    }

    [Fact]
    public void OperadorDiferenca_DeveSerOInversoDaIgualdade()
    {
        (new Dinheiro(10m, "BRL") != new Dinheiro(10m, "BRL")).Should().BeFalse();
        (new Dinheiro(10m, "BRL") != new Dinheiro(20m, "BRL")).Should().BeTrue();
    }
}
