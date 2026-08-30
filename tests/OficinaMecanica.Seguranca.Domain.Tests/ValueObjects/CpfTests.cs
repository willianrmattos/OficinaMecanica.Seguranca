using FluentAssertions;
using OficinaMecanica.Seguranca.Domain.Exceptions;
using OficinaMecanica.Seguranca.Domain.ValueObjects;

namespace OficinaMecanica.Seguranca.Domain.Tests.ValueObjects;

public class CpfTests
{
    [Theory]
    [InlineData("52998224725")]
    [InlineData("529.982.247-25")]
    public void Criar_ComCpfValido_DeveCriarCpf(string cpf)
    {
        var valueObject = Cpf.Criar(cpf);
        valueObject.Numero.Should().Be("52998224725");
    }

    [Theory]
    [InlineData("00000000000")]
    [InlineData("12345678900")]
    [InlineData("123")]
    [InlineData("")]
    public void Criar_ComCpfInvalido_DeveLancarExcecao(string cpf)
    {
        var act = () => Cpf.Criar(cpf);
        act.Should().Throw<DomainException>().WithMessage("*CPF inválido*");
    }

    [Fact]
    public void Formatado_DeveRetornarFormatado()
    {
        var cpf = Cpf.Criar("52998224725");
        cpf.Formatado.Should().Be("529.982.247-25");
    }

    [Fact]
    public void Equals_MesmoNumero_DeveSerIgual()
    {
        var cpf1 = Cpf.Criar("52998224725");
        var cpf2 = Cpf.Criar("529.982.247-25");
        cpf1.Should().Be(cpf2);
    }
}
