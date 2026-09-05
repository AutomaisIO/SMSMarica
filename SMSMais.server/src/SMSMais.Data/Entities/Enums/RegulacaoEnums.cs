namespace SMSMais.Data.Entities.Enums;

/// <summary>Sistema de regulação em que uma solicitação termina (ADR-0052).</summary>
public enum SistemaRegulacao
{
    /// <summary>Regulação municipal de Maricá.</summary>
    Sisreg = 1,

    /// <summary>SER da SES-RJ (ADR-0042).</summary>
    Ser = 2,

    /// <summary>SER de Niterói.</summary>
    Sernit = 3,

    /// <summary>Reservado: ainda não há origem eSUS no catálogo.</summary>
    Esus = 4,
}

/// <summary>Natureza do procedimento canônico.</summary>
public enum TipoProcedimentoRegulacao
{
    Consulta = 1,
    Exame = 2,
    Cirurgia = 3,
    Outro = 9,
}

/// <summary>
/// Como uma origem chegou ao procedimento canônico a que está ligada.
///
/// <para>Mesmo idioma de <c>SisregProcedimentoSigtap.SugeridoSigtapId</c>/<c>ConfirmadoEm</c>:
/// o robô só <b>sugere</b>; quem confirma é a curadoria. Origem <see cref="Automatico"/> pode ser
/// movida por uma sugestão aceita; <see cref="Confirmado"/> nunca é mexida pelo sync.</para>
/// </summary>
public enum VinculoOrigemRegulacao
{
    Automatico = 1,
    Confirmado = 2,
}
