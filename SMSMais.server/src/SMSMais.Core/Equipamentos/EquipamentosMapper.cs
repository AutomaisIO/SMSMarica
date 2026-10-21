using SMSMais.Core.Equipamentos.Dtos;
using SMSMais.Core.Worklist;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Equipamentos;

internal static class EquipamentosMapper
{
    public static EquipamentoDto ParaDto(Equipamento e) => new(
        e.Id,
        e.Nome,
        e.UnidadeId,
        e.Unidade?.Nome ?? string.Empty,
        e.ModalidadeDicom,
        e.IdentificadorDicom,
        e.DescricaoMaxCaracteres,
        e.Ativo,
        e.CriadoEm,
        e.CodigoProcedimentoDestinos.HasFlag(DestinoCodigoProcedimento.ProcedimentoSolicitado),
        e.CodigoProcedimentoDestinos.HasFlag(DestinoCodigoProcedimento.ProtocoloAgendado),
        e.CodigoProcedimentoEsquema,
        Formatada(e.CodigoProcedimentoTag),
        Formatada(e.MedicoSolicitanteTag),
        Formatada(e.UnidadeTag),
        e.UnidadeOrigem,
        e.Laudo,
        e.LiberacaoPaciente,
        e.UsaAnamnese);

    public static EquipamentoListItemDto ParaListItem(Equipamento e) => new(
        e.Id,
        e.Nome,
        e.UnidadeId,
        e.Unidade?.Nome ?? string.Empty,
        e.ModalidadeDicom,
        e.IdentificadorDicom,
        e.DescricaoMaxCaracteres,
        e.Ativo,
        e.CodigoProcedimentoDestinos.HasFlag(DestinoCodigoProcedimento.ProcedimentoSolicitado),
        e.CodigoProcedimentoDestinos.HasFlag(DestinoCodigoProcedimento.ProtocoloAgendado),
        e.CodigoProcedimentoEsquema,
        Formatada(e.CodigoProcedimentoTag),
        Formatada(e.MedicoSolicitanteTag),
        Formatada(e.UnidadeTag),
        e.UnidadeOrigem,
        e.Laudo,
        e.LiberacaoPaciente,
        e.UsaAnamnese);

    /// <summary>As duas caixas da tela → o campo combinável gravado (ADR-0074).</summary>
    public static DestinoCodigoProcedimento Destinos(bool noProcedimentoSolicitado, bool noProtocoloAgendado) =>
        (noProcedimentoSolicitado ? DestinoCodigoProcedimento.ProcedimentoSolicitado : DestinoCodigoProcedimento.Nenhum)
        | (noProtocoloAgendado ? DestinoCodigoProcedimento.ProtocoloAgendado : DestinoCodigoProcedimento.Nenhum);

    /// <summary>Tag do código como gravada: o padrão (0008,0100) — ou vazio — vira null, "na sequência".</summary>
    public static string? TagCodigo(string? entrada)
    {
        var tag = TagDicomWorklist.Normalizar(entrada);
        return tag == TagDicomWorklist.CodigoProcedimentoPadrao ? null : tag;
    }

    /// <summary>Tag de um campo que liga e desliga: desligado = null; ligado sem tag = o padrão.</summary>
    public static string? TagLigada(bool enviar, string? entrada, string padrao) =>
        enviar ? TagDicomWorklist.Normalizar(entrada) ?? padrao : null;

    /// <summary>"Só com o laudo assinado" com o laudo feito fora prenderia o exame para sempre: não há
    /// laudo nosso para soltá-lo (ADR-0075; trava também no banco).</summary>
    public static bool CombinacaoLiberacaoValida(OrigemLaudo laudo, LiberacaoExamePaciente liberacao) =>
        liberacao != LiberacaoExamePaciente.ComLaudoAssinado || laudo == OrigemLaudo.Plataforma;

    public const string MensagemCombinacaoLiberacao =
        "\"Só com o laudo assinado\" exige o laudo feito na plataforma: com o laudo feito fora, o exame "
        + "ficaria retido para sempre. Escolha \"Assim que chegar\".";

    private static string? Formatada(string? tag) => tag is null ? null : TagDicomWorklist.Formatar(tag);
}
