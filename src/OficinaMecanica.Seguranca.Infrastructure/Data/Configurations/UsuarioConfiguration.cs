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

        builder.Property(u => u.NomeUsuario)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(u => u.NomeUsuario).IsUnique();

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
