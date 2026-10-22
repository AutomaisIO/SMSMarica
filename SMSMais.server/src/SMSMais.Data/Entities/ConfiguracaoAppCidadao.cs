namespace SMSMais.Data.Entities;

/// <summary>
/// Como o app do cidadão se comporta, por canal — o PWA (navegador) e o app nativo (Android/iPhone)
/// podem ser configurados de jeitos diferentes (ADR-0078). Linha única com Id fixo; sem a linha,
/// valem os padrões (tudo desligado). Só admin global altera, e cada ajuste fica na trilha de
/// auditoria (entidade <c>ConfiguracaoAppCidadao</c>).
/// </summary>
public sealed class ConfiguracaoAppCidadao
{
    /// <summary>Id fixo da linha singleton.</summary>
    public static readonly Guid IdSingleton = new("78787878-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = IdSingleton;

    /// <summary>Na entrada pelo PWA, "nenhum desses é meu" procura o celular no e-SUS PEC.</summary>
    public bool PwaConsultarEsus { get; set; }

    /// <summary>Na entrada pelo app nativo, "nenhum desses é meu" procura o celular no e-SUS PEC.</summary>
    public bool NativoConsultarEsus { get; set; }

    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
