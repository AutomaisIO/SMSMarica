using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Uma versão PUBLICADA da extensão do Chrome ou do atualizador (ADR-0064). Publicar é o ato que
/// separa o desenvolvimento do que chega aos computadores: a versão nasce no canal de teste
/// (<see cref="PromovidoEm"/> nulo) e passa a valer para todos quando é promovida. O arquivo fica
/// aqui, em <c>bytea</c> — a plataforma é o único lugar de onde ele sai, e só para computador
/// autorizado.
/// </summary>
public class ExtensaoPacote
{
    public Guid Id { get; set; }

    public ExtensaoArtefato Artefato { get; set; }

    /// <summary>Versão no formato <c>1.2.3</c>. UNIQUE por artefato.</summary>
    public string Versao { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex minúsculo) do <see cref="Conteudo"/>. O atualizador confere antes de se trocar.</summary>
    public string Sha256 { get; set; } = string.Empty;

    public int Tamanho { get; set; }

    public byte[] Conteudo { get; set; } = [];

    /// <summary>O que mudou nesta versão (livre; aparece na tela de versões).</summary>
    public string? Notas { get; set; }

    public DateTime PublicadoEm { get; set; }
    public Guid? PublicadoPor { get; set; }

    /// <summary>Publicado pela API de publicação (com a chave), e não por uma pessoa no painel.</summary>
    public bool PublicadoPelaApi { get; set; }

    /// <summary>Quando passou a valer para o canal de produção. Nulo = só os computadores de teste recebem.</summary>
    public DateTime? PromovidoEm { get; set; }
    public Guid? PromovidoPor { get; set; }
    public bool PromovidoPelaApi { get; set; }

    /// <summary>
    /// Versão retirada: deixa de ser entregue. Quem já a recebeu continua com ela (computador não
    /// rebaixa) — o conserto é publicar uma versão maior.
    /// </summary>
    public DateTime? RetiradoEm { get; set; }
    public Guid? RetiradoPor { get; set; }
}
