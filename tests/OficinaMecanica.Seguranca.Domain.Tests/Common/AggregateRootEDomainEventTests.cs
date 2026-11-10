using FluentAssertions;
using OficinaMecanica.Seguranca.Domain.Common;

namespace OficinaMecanica.Seguranca.Domain.Tests.Common;

public class AggregateRootEDomainEventTests
{
    private sealed class RaizDeTeste : AggregateRoot { }

    private sealed class EventoDeTeste : DomainEvent { }

    [Fact]
    public void AggregateRoot_NovaInstancia_DeveComecarNaVersaoZero()
    {
        new RaizDeTeste().Version.Should().Be(0u);
    }

    [Fact]
    public void IncrementVersion_DeveSomarUmACadaChamada()
    {
        var raiz = new RaizDeTeste();

        raiz.IncrementVersion();
        raiz.IncrementVersion();

        raiz.Version.Should().Be(2u);
    }

    [Fact]
    public void DomainEvent_DeveRegistrarOInstanteEmUtc()
    {
        var antes = DateTime.UtcNow;

        var evento = new EventoDeTeste();

        evento.OcorridoEm.Should().BeOnOrAfter(antes).And.BeOnOrBefore(DateTime.UtcNow);
        evento.OcorridoEm.Kind.Should().Be(DateTimeKind.Utc);
    }
}
