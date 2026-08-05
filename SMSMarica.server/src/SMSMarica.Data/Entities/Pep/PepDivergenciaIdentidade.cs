using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities.Pep;

/// <summary>
/// Divergência de identidade entre a ORIGEM (PEP) e o hub FHIR para o MESMO CPF — hoje,
/// data de nascimento diferente. Medido em 01/08/2026: 142 casos hub×Salux (81 com ano
/// diferente) e 1.099 Salux×UPA (371 com ano diferente) — ver
/// <c>docs/integracoes/plano-sincronismo-hub.md §3.2</c>.
///
/// Por que é 1ª classe e não uma "falha": não é erro de escrita, é <b>conflito de verdade</b>.
/// Fundir às cegas mistura o histórico clínico de duas pessoas. Enquanto a divergência está
/// <see cref="StatusDivergenciaIdentidade.Pendente"/> (ou com veredicto
/// <see cref="VeredictoDivergenciaIdentidade.HubCorreto"/>), o campo fica <b>congelado</b>:
/// a origem não sobrescreve o hub. O árbitro é a consulta de CPF (Receita via Hub do
/// Desenvolvedor, CADSUS como fallback), que só valida quando o par CPF+nascimento confere.
///
/// Uma linha por (fonte, CPF, tipo) — re-detecção atualiza a existente em vez de duplicar.
/// </summary>
public class PepDivergenciaIdentidade
{
    public Guid Id { get; set; }

    /// <summary>Execução em que a divergência foi detectada pela ÚLTIMA vez.</summary>
    public Guid? ExecucaoId { get; set; }

    /// <summary>Base (IaFonte) de origem.</summary>
    public Guid FonteId { get; set; }

    /// <summary>Slug da base no momento da detecção (sobrevive à edição da fonte).</summary>
    public string FonteSlug { get; set; } = string.Empty;

    /// <summary>
    /// Código do paciente na origem, NUMÉRICO. Serve ao Salux, cujo <c>cd_paciente</c> é number.
    /// Zero quando a origem não usa código numérico — aí o que vale é <see cref="CodigoOrigem"/>.
    /// </summary>
    public long CdPaciente { get; set; }

    /// <summary>
    /// Código do paciente na origem, <b>como texto e exatamente como a origem o escreve</b> —
    /// é o que permite o reimport direcionado em qualquer base.
    ///
    /// <para>Existe porque <see cref="CdPaciente"/> mentia fora do Salux: o código do Klinikos é
    /// <c>char</c> com zeros à esquerda (<c>062608050044</c>), e extrair "os dígitos" do
    /// identifier prefixado (<c>upa24h-marica-sqlserver:062608050044</c>) capturava o <c>24</c>
    /// do próprio slug e perdia os zeros — 959 divergências da UPA nasceram apontando para um
    /// paciente que não existe. Texto não tem esse problema.</para>
    /// </summary>
    public string? CodigoOrigem { get; set; }

    /// <summary>CPF normalizado (11 dígitos) — a chave da identidade em disputa.</summary>
    public string Cpf { get; set; } = string.Empty;

    public TipoDivergenciaIdentidade Tipo { get; set; }

    /// <summary>Valor vindo da ORIGEM (para nascimento: ISO <c>yyyy-MM-dd</c>).</summary>
    public string ValorOrigem { get; set; } = string.Empty;

    /// <summary>Valor que já estava no HUB (mesmo formato de <see cref="ValorOrigem"/>).</summary>
    public string ValorHub { get; set; } = string.Empty;

    /// <summary>Nome na origem — contexto para a revisão humana (não é usado na arbitragem).</summary>
    public string? NomeOrigem { get; set; }

    /// <summary>Nome no hub — idem.</summary>
    public string? NomeHub { get; set; }

    /// <summary>Id lógico do Patient no hub, quando conhecido.</summary>
    public string? PatientIdHub { get; set; }

    public StatusDivergenciaIdentidade Status { get; set; } = StatusDivergenciaIdentidade.Pendente;

    public VeredictoDivergenciaIdentidade Veredicto { get; set; } = VeredictoDivergenciaIdentidade.Indefinido;

    /// <summary>Motor que arbitrou (<c>hubdodesenvolvedor</c> / <c>sisreg-cadsus</c>).</summary>
    public string? VeredictoMotor { get; set; }

    /// <summary>Valor apontado como correto pela consulta (ISO), quando conclusivo.</summary>
    public string? ValorCorreto { get; set; }

    /// <summary>Nome oficial devolvido pela consulta — ajuda a confirmar que é a mesma pessoa.</summary>
    public string? NomeOficial { get; set; }

    /// <summary>Detalhe da arbitragem (mensagem do motor, motivo de não ser conclusiva).</summary>
    public string? Detalhe { get; set; }

    /// <summary>Quantas vezes a divergência foi re-detectada (indica sincronismo insistindo no conflito).</summary>
    public int Ocorrencias { get; set; } = 1;

    public DateTime CriadoEm { get; set; }

    public DateTime AtualizadoEm { get; set; }

    /// <summary>Quando a consulta externa arbitrou (ou tentou).</summary>
    public DateTime? VerificadoEm { get; set; }

    /// <summary>Quando um operador encerrou manualmente (ignorar).</summary>
    public DateTime? ResolvidoEm { get; set; }

    public Guid? ResolvidoPor { get; set; }
}
