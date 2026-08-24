using SMSMarica.Core.TiposExame.Dtos;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.TiposExame;

internal static class TiposExameMapper
{
    /// <summary>
    /// Tipo criado pela importação do SISREG que ninguém configurou ainda: ele nomeia, lista e
    /// lauda normalmente, mas não vai ao PACS. Modalidade indefinida é o sinal mais honesto de
    /// "falta configurar" — um tipo com modalidade escolhida e worklist desligada foi decisão de
    /// alguém, não pendência.
    /// </summary>
    private static bool AguardandoConfiguracaoDicom(TipoExame t) =>
        t.AutoCriado && t.ModalidadeDicom == ModalidadeDicom.Indefinida;

    public static TipoExameDto ParaDto(TipoExame t) => new(
        t.Id,
        t.Nome,
        t.CodigoSisreg,
        t.AutoCriado,
        t.ProcedimentoSigtapId,
        t.ProcedimentoSigtap?.Codigo ?? string.Empty,
        t.ProcedimentoSigtap?.Nome ?? string.Empty,
        t.ModalidadeDicom,
        t.RequestedProcedureDescription,
        t.ScheduledProcedureStepDescription,
        t.CodigosProtocolo,
        t.TempoEstimadoMinutos,
        t.UnidadePadraoId,
        t.UnidadePadrao?.Nome,
        t.Ativo,
        t.EnviarParaWorklist,
        t.CriadoEm);

    public static TipoExameListItemDto ParaListItem(TipoExame t) => new(
        t.Id,
        t.Nome,
        t.CodigoSisreg,
        t.AutoCriado,
        AguardandoConfiguracaoDicom(t),
        t.ModalidadeDicom,
        t.ProcedimentoSigtap?.Codigo ?? string.Empty,
        t.TempoEstimadoMinutos,
        t.Ativo,
        t.EnviarParaWorklist);
}
