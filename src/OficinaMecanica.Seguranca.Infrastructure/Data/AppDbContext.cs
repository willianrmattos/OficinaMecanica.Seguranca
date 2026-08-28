using OficinaMecanica.Seguranca.Domain.Common;
using OficinaMecanica.Seguranca.Domain.Entities;
using OficinaMecanica.Seguranca.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace OficinaMecanica.Seguranca.Infrastructure.Data;

public class AppDbContext : DbContext, IUnitOfWork
{
    private readonly IMediator _mediator;

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public AppDbContext(DbContextOptions<AppDbContext> options, IMediator mediator) : base(options)
    {
        _mediator = mediator;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.State == EntityState.Modified)
            .ToList())
        {
            entry.Entity.IncrementVersion();
        }

        var domainEntities = ChangeTracker.Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = domainEntities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        domainEntities.ForEach(e => e.ClearDomainEvents());

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }
}
