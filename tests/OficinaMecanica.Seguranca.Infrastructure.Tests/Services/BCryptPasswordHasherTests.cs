using FluentAssertions;
using OficinaMecanica.Seguranca.Infrastructure.Services;

namespace OficinaMecanica.Seguranca.Infrastructure.Tests.Services;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_NaoDeveDevolverASenhaEmTextoPuro()
    {
        var hash = _hasher.Hash("senha-forte-123");

        hash.Should().NotBe("senha-forte-123");
        hash.Should().StartWith("$2");
    }

    [Fact]
    public void Hash_DuasChamadasComAMesmaSenha_DevemGerarHashesDiferentes()
    {
        _hasher.Hash("senha-forte-123").Should().NotBe(_hasher.Hash("senha-forte-123"));
    }

    [Fact]
    public void Verificar_ComASenhaCorreta_DeveRetornarVerdadeiro()
    {
        var hash = _hasher.Hash("senha-forte-123");

        _hasher.Verificar("senha-forte-123", hash).Should().BeTrue();
    }

    [Fact]
    public void Verificar_ComSenhaErrada_DeveRetornarFalso()
    {
        var hash = _hasher.Hash("senha-forte-123");

        _hasher.Verificar("outra-senha", hash).Should().BeFalse();
    }

    [Theory]
    [InlineData("isto-nao-e-um-hash-bcrypt")]
    [InlineData("")]
    public void Verificar_ComHashMalformado_DeveTratarComoSenhaIncorretaSemLancar(string hashInvalido)
    {
        var act = () => _hasher.Verificar("qualquer", hashInvalido);

        act.Should().NotThrow();
        _hasher.Verificar("qualquer", hashInvalido).Should().BeFalse();
    }
}
