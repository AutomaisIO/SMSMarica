using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Data.Entities;

/// <summary>
/// Alarme de "este exame pode estar no paciente errado" — e a QUARENTENA que ele impõe.
///
/// <para>Nasce do incidente de 11/08/2026: entre alguém perceber a troca e um administrador
/// conseguir corrigir, o exame contaminado continuava circulando — dava para laudar em cima dele e
/// o aviso saía para o paciente errado. Qualquer usuário que enxergue o exame pode abrir o
/// incidente; só quem tem <see cref="ModuloPermissao.CorrecaoIdentidadeExame"/> resolve ou descarta.</para>
///
/// <para>Enquanto houver incidente <c>Aberto</c> para o estudo: não se cria nem se assina laudo,
/// o aviso ao paciente não sai, a conciliação automática não mexe no estudo e ele some da
/// listagem do app do cidadão.</para>
/// </summary>
public class ExameIncidenteIdentidade
{
    public Guid Id { get; set; }

    /// <summary>Estudo sob suspeita — a quarentena é do ESTUDO, não do exame: o vínculo é
    /// justamente o que está em dúvida.</summary>
    public string StudyInstanceUID { get; set; } = string.Empty;

    /// <summary>Exame a que o estudo estava vinculado quando o alarme soou (histórico).</summary>
    public Guid? ExameImagemId { get; set; }

    /// <summary>Paciente que constava como dono no momento do alarme (histórico).</summary>
    public Guid? PacienteSuspeitoId { get; set; }

    /// <summary>Por que se desconfiou. Obrigatório — sem isso o próximo a olhar não sabe o que
    /// conferir.</summary>
    public string Motivo { get; set; } = string.Empty;

    public StatusIncidenteIdentidade Status { get; set; } = StatusIncidenteIdentidade.Aberto;

    /// <summary>Aberto por varredura automática (detector) em vez de por uma pessoa.</summary>
    public bool Automatico { get; set; }

    public DateTime? ResolvidoEm { get; set; }
    public Guid? ResolvidoPor { get; set; }
    public string? ResolucaoNota { get; set; }

    // ---- Auditoria ADR-0006 ----
    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
