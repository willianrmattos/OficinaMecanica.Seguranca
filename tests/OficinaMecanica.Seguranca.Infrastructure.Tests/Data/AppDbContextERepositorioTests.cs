using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using OficinaMecanica.Seguranca.Domain.Common;
using OficinaMecanica.Seguranca.Infrastructure.Data;
using OficinaMecanica.Seguranca.Infrastructure.Repositories;
using OficinaMecanica.Seguranca.Tests.Common.Builders;

namespace OficinaMecanica.Seguranca.Infrastructure.Tests.Data;

public sealed class AppDbContextERepositorioTests : IDisposable
{
    private sealed class EventoDeTeste : DomainEvent { }

    private readonly Mock<IMediator> _mediator = new();
    private readonly DbContextOptions<AppDbContext> _opcoes =
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private AppDbContext NovoContexto() => new(_opcoes, _mediator.Object);

    public void Dispose()
    {
        using var contexto = NovoContexto();
        contexto.Database.EnsureDeleted();
    }

    [Fact]
    public async Task ExisteAlgumAsync_ComBancoVazio_DeveRetornarFalso()
    {
        using var contexto = NovoContexto();

        (await new UsuarioRepository(contexto).ExisteAlgumAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task AdicionarAsync_DepoisDeSalvar_DevePermitirObterPorCpfEConfirmarQueExisteAlgum()
    {
        var usuario = new UsuarioBuilder().ComCpf("52998224725").Build();
        using (var contexto = NovoContexto())
        {
            await new UsuarioRepository(contexto).AdicionarAsync(usuario);
            await contexto.SaveChangesAsync();
        }

        using var leitura = NovoContexto();
        var repositorio = new UsuarioRepository(leitura);

        (await repositorio.ExisteAlgumAsync()).Should().BeTrue();
        var encontrado = await repositorio.ObterPorCpfAsync("52998224725");
        encontrado.Should().NotBeNull();
        encontrado!.Id.Should().Be(usuario.Id);
    }

    [Fact]
    public async Task ObterPorCpfAsync_ComCpfInexistente_DeveRetornarNulo()
    {
        using var contexto = NovoContexto();

        (await new UsuarioRepository(contexto).ObterPorCpfAsync("52998224725")).Should().BeNull();
    }

    [Fact]
    public async Task SaveChangesAsync_AoAlterarUmAggregate_DeveIncrementarAVersao()
    {
        var usuario = new UsuarioBuilder().Build();
        using (var contexto = NovoContexto())
        {
            await contexto.Usuarios.AddAsync(usuario);
            await contexto.SaveChangesAsync();
        }

        using var edicao = NovoContexto();
        var carregado = await edicao.Usuarios.SingleAsync();
        var versaoAntes = carregado.Version;
        carregado.AlterarSenha("novo-hash");

        await edicao.SaveChangesAsync();

        carregado.Version.Should().Be(versaoAntes + 1);
    }

    [Fact]
    public async Task SaveChangesAsync_AoCriarUmAggregate_NaoDeveIncrementarAVersao()
    {
        var usuario = new UsuarioBuilder().Build();
        using var contexto = NovoContexto();
        await contexto.Usuarios.AddAsync(usuario);

        await contexto.SaveChangesAsync();

        usuario.Version.Should().Be(0u);
    }

    [Fact]
    public async Task SaveChangesAsync_ComEventosDeDominio_DevePublicarDepoisDeSalvarELimparOsEventos()
    {
        var usuario = new UsuarioBuilder().Build();
        var evento = new EventoDeTeste();
        usuario.AddDomainEvent(evento);

        // Optei por ler o banco por um contexto separado no instante do Publish: so assim o teste prova a ordem
        // "grava e depois publica" (um handler do evento precisa enxergar o dado ja persistido).
        var usuarioJaPersistidoNoPublish = false;
        _mediator
            .Setup(m => m.Publish(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                using var leitura = NovoContexto();
                usuarioJaPersistidoNoPublish = leitura.Usuarios.Any();
            })
            .Returns(Task.CompletedTask);
        using var contexto = NovoContexto();
        await contexto.Usuarios.AddAsync(usuario);

        await contexto.SaveChangesAsync();

        // O contexto publica pelo tipo estatico DomainEvent, entao o Moq precisa casar o Publish<DomainEvent>, nao o do tipo concreto.
        _mediator.Verify(m => m.Publish(It.Is<DomainEvent>(e => ReferenceEquals(e, evento)), It.IsAny<CancellationToken>()), Times.Once);
        usuarioJaPersistidoNoPublish.Should().BeTrue();
        usuario.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveChangesAsync_SemEventosDeDominio_NaoDevePublicarNada()
    {
        using var contexto = NovoContexto();
        await contexto.Usuarios.AddAsync(new UsuarioBuilder().Build());

        await contexto.SaveChangesAsync();

        _mediator.Verify(m => m.Publish(It.IsAny<DomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
