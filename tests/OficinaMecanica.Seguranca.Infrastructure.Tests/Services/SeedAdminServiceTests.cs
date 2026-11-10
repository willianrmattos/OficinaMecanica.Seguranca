using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using OficinaMecanica.Seguranca.Application.Interfaces;
using OficinaMecanica.Seguranca.Domain.Entities;
using OficinaMecanica.Seguranca.Domain.Enums;
using OficinaMecanica.Seguranca.Domain.Exceptions;
using OficinaMecanica.Seguranca.Domain.Interfaces;
using OficinaMecanica.Seguranca.Infrastructure.Services;

namespace OficinaMecanica.Seguranca.Infrastructure.Tests.Services;

public class SeedAdminServiceTests
{
    private readonly Mock<IUsuarioRepository> _repositorio = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IPasswordHasher> _hasher = new();

    private SeedAdminService CriarServico(IDictionary<string, string?> configuracao)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(configuracao).Build();
        return new SeedAdminService(_repositorio.Object, _unitOfWork.Object, _hasher.Object, config);
    }

    private static Dictionary<string, string?> ConfiguracaoCompleta() => new()
    {
        ["SeedAdmin:Cpf"] = "52998224725",
        ["SeedAdmin:SenhaInicial"] = "senha-inicial"
    };

    [Fact]
    public async Task SeedAsync_QuandoJaExisteUsuario_NaoDeveCriarNada()
    {
        _repositorio.Setup(r => r.ExisteAlgumAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var servico = CriarServico(ConfiguracaoCompleta());

        await servico.SeedAsync();

        _repositorio.Verify(r => r.AdicionarAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SeedAsync_SemUsuarios_DeveCriarOAdminComSenhaHasheadaEPersistir()
    {
        _repositorio.Setup(r => r.ExisteAlgumAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _hasher.Setup(h => h.Hash("senha-inicial")).Returns("hash-da-senha-inicial");
        Usuario? criado = null;
        _repositorio
            .Setup(r => r.AdicionarAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()))
            .Callback<Usuario, CancellationToken>((u, _) => criado = u)
            .Returns(Task.CompletedTask);
        var servico = CriarServico(ConfiguracaoCompleta());

        await servico.SeedAsync();

        criado.Should().NotBeNull();
        criado!.Cpf.Numero.Should().Be("52998224725");
        criado.SenhaHash.Should().Be("hash-da-senha-inicial");
        criado.Perfil.Should().Be(PerfilUsuario.Admin);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null, "senha-inicial")]
    [InlineData("52998224725", null)]
    [InlineData("  ", "senha-inicial")]
    [InlineData("52998224725", "")]
    public async Task SeedAsync_ComConfiguracaoIncompleta_DeveLancarEMandarConfigurarAsChaves(string? cpf, string? senha)
    {
        _repositorio.Setup(r => r.ExisteAlgumAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var servico = CriarServico(new Dictionary<string, string?>
        {
            ["SeedAdmin:Cpf"] = cpf,
            ["SeedAdmin:SenhaInicial"] = senha
        });

        var act = () => servico.SeedAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*SeedAdmin:Cpf*SeedAdmin:SenhaInicial*");
        _repositorio.Verify(r => r.AdicionarAsync(It.IsAny<Usuario>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SeedAsync_ComCpfInvalido_DevePropagarAExcecaoDeDominioSemPersistir()
    {
        _repositorio.Setup(r => r.ExisteAlgumAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var servico = CriarServico(new Dictionary<string, string?>
        {
            ["SeedAdmin:Cpf"] = "123",
            ["SeedAdmin:SenhaInicial"] = "senha-inicial"
        });

        var act = () => servico.SeedAsync();

        await act.Should().ThrowAsync<DomainException>();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
