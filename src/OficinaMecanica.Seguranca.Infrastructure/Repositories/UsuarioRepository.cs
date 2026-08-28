using OficinaMecanica.Seguranca.Domain.Entities;
using OficinaMecanica.Seguranca.Domain.Interfaces;
using OficinaMecanica.Seguranca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace OficinaMecanica.Seguranca.Infrastructure.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly AppDbContext _context;

    public UsuarioRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Usuario?> ObterPorNomeUsuarioAsync(string nomeUsuario, CancellationToken cancellationToken = default)
    {
        return await _context.Usuarios
            .FirstOrDefaultAsync(u => u.NomeUsuario == nomeUsuario, cancellationToken);
    }

    public async Task<bool> ExisteAlgumAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Usuarios.AnyAsync(cancellationToken);
    }

    public async Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        await _context.Usuarios.AddAsync(usuario, cancellationToken);
    }
}
