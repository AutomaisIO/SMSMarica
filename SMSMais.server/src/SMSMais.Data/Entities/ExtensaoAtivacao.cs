namespace SMSMais.Data.Entities;

/// <summary>
/// Pedido de autorização de um computador (ADR-0064). Nasce de dois jeitos: junto com o instalador
/// baixado pelo painel (já autorizado por quem baixou) ou quando o atualizador pede, pelo
/// "Configurar" (fica aguardando alguém logado autorizar). Em ambos, o código é de uso único e
/// tem validade curta; trocado, vira um <see cref="ExtensaoDispositivo"/>.
/// </summary>
public class ExtensaoAtivacao
{
    public Guid Id { get; set; }

    /// <summary>Hash SHA-256 (hex) do código secreto que só o computador conhece. UNIQUE.</summary>
    public string CodigoHash { get; set; } = string.Empty;

    /// <summary>Código curto que vai no endereço da página de autorização. UNIQUE. Não é segredo.</summary>
    public string CodigoPublico { get; set; } = string.Empty;

    /// <summary>Nome do computador que pediu (nulo no pedido que nasce com o instalador).</summary>
    public string? Computador { get; set; }

    public string? VersaoAtualizador { get; set; }

    /// <summary>Nasceu com o instalador baixado pelo painel (já autorizado por quem baixou).</summary>
    public bool PeloInstalador { get; set; }

    public DateTime CriadoEm { get; set; }
    public DateTime ExpiraEm { get; set; }

    public DateTime? AutorizadoEm { get; set; }
    public Guid? AutorizadoPor { get; set; }

    /// <summary>Unidade ativa de quem autorizou; passa para o computador.</summary>
    public Guid? UnidadeId { get; set; }

    /// <summary>Quando o código foi trocado pelo token. Preenchido, o código não vale mais.</summary>
    public DateTime? UsadoEm { get; set; }

    public Guid? DispositivoId { get; set; }
}
