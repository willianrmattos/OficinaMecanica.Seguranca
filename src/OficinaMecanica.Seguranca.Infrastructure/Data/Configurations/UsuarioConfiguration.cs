using OficinaMecanica.Seguranca.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace OficinaMecanica.Seguranca.Infrastructure.Data.Configurations;

public class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("Usuarios");
        builder.HasKey(u => u.Id);

        builder.OwnsOne(u => u.Cpf, c =>
        {
            c.Property(p => p.Numero).HasColumnName("Cpf").IsRequired().HasMaxLength(11);
            c.HasIndex(p => p.Numero).IsUnique();
        });

        builder.Property(u => u.SenhaHash)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Perfil).IsRequired();
        builder.Property(u => u.Ativo).IsRequired();
        builder.Property(u => u.DataCriacao).IsRequired();

        builder.Property(u => u.Version).IsConcurrencyToken();

        builder.Ignore(u => u.DomainEvents);
    }
}
