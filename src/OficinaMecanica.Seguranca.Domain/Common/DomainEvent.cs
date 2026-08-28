using MediatR;

namespace OficinaMecanica.Seguranca.Domain.Common;

public abstract class DomainEvent : INotification
{
    public DateTime OcorridoEm { get; }

    protected DomainEvent()
    {
        OcorridoEm = DateTime.UtcNow;
    }
}
