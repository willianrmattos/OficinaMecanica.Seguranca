namespace OficinaMecanica.Seguranca.Application.DTOs;

public record LoginResponseDto(string Token, string Tipo, int ExpiracaoMinutos);
