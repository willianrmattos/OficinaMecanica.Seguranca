namespace OficinaMecanica.Seguranca.Domain.Interfaces;

using OficinaMecanica.Seguranca.Domain.Entities;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default);
    Task<bool> ExisteAlgumAsync(CancellationToken cancellationToken = default);
    Task AdicionarAsync(Usuario usuario, CancellationToken cancellationToken = default);
}
