namespace SMSMarica.Data.Entities.Pep;

/// <summary>
/// Uma falha durável de importação de PEP — gravada na hora em que acontece (incremental),
/// não só no fim do run. Sobrevive a crash/órfã (ao contrário de <c>FalhasJson</c>, que só é
/// persistido na conclusão limpa) e serve de insumo para o reimport direcionado por
/// <see cref="CdPaciente"/>: reprocessa só os pacientes que falharam e marca
/// <see cref="ResolvidoEm"/>. Granularidade por recurso (cada Condition/Medication/etc. que
/// o hub rejeitou vira uma linha).
/// </summary>
public class PepSincronizacaoFalha
{
    public Guid Id { get; set; }

    /// <summary>Execução em que a falha ocorreu.</summary>
    public Guid ExecucaoId { get; set; }

    /// <summary>Base (IaFonte) de origem — permite filtrar/reimportar por base.</summary>
    public Guid FonteId { get; set; }

    /// <summary>Slug da base carimbado no momento da falha (sobrevive à edição da fonte).</summary>
    public string FonteSlug { get; set; } = string.Empty;

    /// <summary>Código do paciente na origem (cd_paciente) — chave do reimport direcionado.</summary>
    public long CdPaciente { get; set; }

    /// <summary>Primeira linha da mensagem de erro (ex.: corpo do 400 do hub FHIR).</summary>
    public string Mensagem { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    /// <summary>Setado quando a falha for reprocessada com sucesso. NULL = pendente de resolução.</summary>
    public DateTime? ResolvidoEm { get; set; }
}
