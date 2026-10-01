using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities;

/// <summary>
/// Um computador autorizado a receber a extensão (ADR-0064). Quem autoriza é uma pessoa logada no
/// painel; o computador guarda um token próprio (aqui só o hash) e o usa para perguntar pela
/// versão publicada. A cada pergunta ele informa o próprio estado — é o inventário da tela.
/// </summary>
public class ExtensaoDispositivo
{
    public Guid Id { get; set; }

    /// <summary>Nome do computador no Windows, como ele mesmo informou.</summary>
    public string Computador { get; set; } = string.Empty;

    /// <summary>Hash SHA-256 (hex) do token do computador. UNIQUE. O token em claro nunca é guardado.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public ExtensaoCanal Canal { get; set; } = ExtensaoCanal.Prod;

    public DateTime AutorizadoEm { get; set; }
    public Guid? AutorizadoPor { get; set; }

    /// <summary>Unidade ativa de quem autorizou (informativo: onde o computador provavelmente está).</summary>
    public Guid? UnidadeId { get; set; }

    public DateTime? UltimoContatoEm { get; set; }

    /// <summary>Versão da extensão que o computador disse ter no disco.</summary>
    public string? VersaoExtensao { get; set; }

    public string? VersaoAtualizador { get; set; }

    /// <summary>
    /// Como a extensão está no Chrome, segundo o computador: <c>carregada</c>, <c>nao-carregada</c>,
    /// <c>desativada</c>, <c>modo-dev-desligado</c>, <c>fechado</c> ou <c>sem-perfil</c>.
    /// </summary>
    public string? SituacaoChrome { get; set; }

    public DateTime? RevogadoEm { get; set; }
    public Guid? RevogadoPor { get; set; }
}
