using SMSMais.Data.Entities.Enums;

namespace SMSMais.Data.Entities.Alertas;

/// <summary>
/// Histórico do que foi (ou não) mandado ao celular. Existe porque "nunca recebi nada" não pode
/// ser uma pergunta sem resposta: aqui fica se saiu, para quem, por qual caminho (texto ou
/// template) e, se falhou, o que a Meta respondeu.
/// </summary>
public sealed class AlertaEnvio
{
    public Guid Id { get; set; }
    public string OrigemChave { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public string Detalhe { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }

    /// <summary>Ocorrências agrupadas neste aviso (1 + as que o freio segurou antes dele).</summary>
    public int Ocorrencias { get; set; } = 1;

    public SituacaoAlertaEnvio Situacao { get; set; }

    /// <summary>Uma linha por telefone: "21999990000 template ok" / "… falhou: (132001) …".</summary>
    public string? Resultado { get; set; }
}
