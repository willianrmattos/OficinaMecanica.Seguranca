using System.Text.Json;

namespace OficinaMecanica.Seguranca.Functions.Json;

// Opcoes de serializacao compartilhadas entre as Functions e o middleware de excecao
// Instancia unica e reaproveitada de proposito -
// JsonSerializerOptions e recomendado ser cacheado, nao recriado a cada request.
public static class SerializationDefaults
{
    public static readonly JsonSerializerOptions CamelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
}
