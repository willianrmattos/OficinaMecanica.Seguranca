using FluentAssertions;
using OficinaMecanica.Seguranca.Domain.Entities;
using OficinaMecanica.Seguranca.Domain.Enums;
using OficinaMecanica.Seguranca.Domain.Exceptions;
using OficinaMecanica.Seguranca.Domain.ValueObjects;
using OficinaMecanica.Seguranca.Tests.Common.Builders;

namespace OficinaMecanica.Seguranca.Domain.Tests.Entities;

public class UsuarioTests
{
    [Fact]
    public void Construtor_ComDadosValidos_DeveCriarUsuario()
    {
        var usuario = new UsuarioBuilder()
            .ComCpf("52998224725")
            .ComSenhaHash("hash-da-senha")
            .ComPerfil(PerfilUsuario.Admin)
            .Build();

        usuario.Cpf.Numero.Should().Be("52998224725");
        usuario.SenhaHash.Should().Be("hash-da-senha");
        usuario.Perfil.Should().Be(PerfilUsuario.Admin);
        usuario.Ativo.Should().BeTrue();
        usuario.DataCriacao.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Construtor_ComCpfInvalido_DeveLancarExcecao()
    {
        var act = () => new Usuario(Cpf.Criar("123"), "hash-da-senha", PerfilUsuario.Admin);
        act.Should().Throw<DomainException>().WithMessage("*CPF inválido*");
    }

    [Fact]
    public void Construtor_SemSenhaHash_DeveLancarExcecao()
    {
        var act = () => new Usuario(Cpf.Criar("52998224725"), "", PerfilUsuario.Admin);
        act.Should().Throw<DomainException>().WithMessage("*hash da senha*obrigatório*");
    }

    [Fact]
    public void AlterarSenha_ComHashValido_DeveAlterar()
    {
        var usuario = new UsuarioBuilder().Build();

        usuario.AlterarSenha("novo-hash");

        usuario.SenhaHash.Should().Be("novo-hash");
    }

    [Fact]
    public void AlterarSenha_SemHash_DeveLancarExcecao()
    {
        var usuario = new UsuarioBuilder().Build();

        var act = () => usuario.AlterarSenha("");

        act.Should().Throw<DomainException>().WithMessage("*hash da senha*obrigatório*");
    }

    [Fact]
    public void Desativar_UsuarioAtivo_DeveDesativar()
    {
        var usuario = new UsuarioBuilder().Build();

        usuario.Desativar();

        usuario.Ativo.Should().BeFalse();
    }

    [Fact]
    public void Desativar_UsuarioJaDesativado_DeveLancarExcecao()
    {
        var usuario = new UsuarioBuilder().Build();
        usuario.Desativar();

        var act = () => usuario.Desativar();

        act.Should().Throw<DomainException>().WithMessage("*já está desativado*");
    }
}
