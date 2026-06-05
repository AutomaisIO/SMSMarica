using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities.Ia;

/// <summary>
/// Log de uso/auditoria de uma pergunta processada contra uma fonte (uma linha por
/// par pergunta×fonte). Guarda o SQL gerado, status, tentativas, visualização e custo.
/// </summary>
public class IaConsulta
{
    public Guid Id { get; set; }
    public Guid FonteId { get; set; }
    public IaFonte? Fonte { get; set; }

    public string Pergunta { get; set; } = string.Empty;
    public string? SqlGerado { get; set; }
    public StatusConsulta Status { get; set; }

    /// <summary>Tipo de visualização escolhido pela IA (tabela/lista/numero/grafico_*).</summary>
    public string? Visualizacao { get; set; }

    public int Tentativas { get; set; }
    public string? Erro { get; set; }
    public int? TokensEntrada { get; set; }
    public int? TokensSaida { get; set; }
    public int? DuracaoMs { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}
