using FluentAssertions;
using OficinaMecanica.Seguranca.Domain.Common;

namespace OficinaMecanica.Seguranca.Domain.Tests.Common;

public class EntityTests
{
    private sealed class EntidadeA : Entity
    {
        public void DefinirId(Guid id) => Id = id;
    }

    private sealed class EntidadeB : Entity
    {
        public void DefinirId(Guid id) => Id = id;
    }

    private sealed class EventoDeTeste : DomainEvent { }

    [Fact]
    public void Construtor_DeveGerarIdNaoVazio_ESemEventos()
    {
        var entidade = new EntidadeA();

        entidade.Id.Should().NotBeEmpty();
        entidade.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void AddDomainEvent_DeveRegistrarOEvento()
    {
        var entidade = new EntidadeA();
        var evento = new EventoDeTeste();

        entidade.AddDomainEvent(evento);

        entidade.DomainEvents.Should().ContainSingle().Which.Should().BeSameAs(evento);
    }

    [Fact]
    public void ClearDomainEvents_DeveLimparOsEventos()
    {
        var entidade = new EntidadeA();
        entidade.AddDomainEvent(new EventoDeTeste());
        entidade.AddDomainEvent(new EventoDeTeste());

        entidade.ClearDomainEvents();

        entidade.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Equals_ComMesmaReferencia_DeveSerIgual()
    {
        var entidade = new EntidadeA();

        entidade.Equals(entidade).Should().BeTrue();
    }

    [Fact]
    public void Equals_ComMesmoTipoEMesmoId_DeveSerIgual()
    {
        var id = Guid.NewGuid();
        var a = new EntidadeA();
        var b = new EntidadeA();
        a.DefinirId(id);
        b.DefinirId(id);

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_ComMesmoTipoEIdsDiferentes_NaoDeveSerIgual()
    {
        new EntidadeA().Equals(new EntidadeA()).Should().BeFalse();
    }

    [Fact]
    public void Equals_ComTiposDiferentesEMesmoId_NaoDeveSerIgual()
    {
        var id = Guid.NewGuid();
        var a = new EntidadeA();
        var b = new EntidadeB();
        a.DefinirId(id);
        b.DefinirId(id);

        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void Equals_ComObjetoQueNaoEEntidade_NaoDeveSerIgual()
    {
        new EntidadeA().Equals("nao-sou-entidade").Should().BeFalse();
    }

    [Fact]
    public void Equals_ComNulo_NaoDeveSerIgual()
    {
        new EntidadeA().Equals(null).Should().BeFalse();
    }
}
