namespace Automais.Zap.Data.Entities;

/// <summary>
/// Um arquivo que a plataforma hospeda para a Meta baixar — hoje, a arte do cabeçalho de um
/// modelo com foto no topo.
///
/// <para>Mora aqui, e não na instância do cliente, porque a arte é um ativo do canal: a Meta
/// baixa essa URL a <b>cada</b> mensagem, então ela precisa ser pública, estável e viver junto
/// do que fala com a Meta. Guardar o binário no banco (em vez de em disco) mantém o droplet
/// descartável: subir outro é restaurar o banco, não caçar arquivo solto.</para>
///
/// <para>É por tenant. Duas prefeituras não compartilham arte nem enxergam a do vizinho na
/// listagem — o conteúdo servido é público por natureza (a Meta baixa sem credencial), mas o
/// id é aleatório e não se adivinha.</para>
/// </summary>
public sealed class Midia
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    public required string NomeArquivo { get; set; }

    /// <summary>Só <c>image/png</c> e <c>image/jpeg</c>: é o que a Meta aceita em cabeçalho.</summary>
    public required string MimeType { get; set; }

    public required byte[] Conteudo { get; set; }

    public long TamanhoBytes { get; set; }

    public int? Largura { get; set; }
    public int? Altura { get; set; }

    /// <summary>SHA-256 do conteúdo: reenviar o mesmo arquivo reaproveita o registro e a URL.</summary>
    public required string HashSha256 { get; set; }

    /// <summary>Para que serve (ex.: <c>cabecalho</c>). Livre; só organiza a listagem.</summary>
    public string? Categoria { get; set; }

    public DateTimeOffset CriadoEm { get; set; }

    /// <summary>Quem subiu pelo painel. Nulo quando veio pela API, com token de tenant.</summary>
    public Guid? CriadoPorUsuarioId { get; set; }
}
