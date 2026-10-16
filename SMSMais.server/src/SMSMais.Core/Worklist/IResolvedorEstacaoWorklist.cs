using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Worklist;

/// <summary>Equipamento elegível para executar um exame (unidade executante + modalidade).</summary>
public sealed record EquipamentoCandidato(
    Guid Id, string Nome, string AeTitle, int DescricaoMaxCaracteres = Equipamento.DescricaoMaxPadrao,
    DestinoCodigoProcedimento CodigoProcedimentoDestinos = DestinoCodigoProcedimento.Nenhum,
    string? CodigoProcedimentoEsquema = null,
    CamposOpcionaisWorklist? Campos = null)
{
    public EstacaoWorklist ComoEstacao() =>
        new(AeTitle, DescricaoMaxCaracteres, CodigoProcedimentoDestinos, CodigoProcedimentoEsquema, Campos);
}

/// <summary>
/// A estação que vai executar o exame e o que ela aguenta. O limite de descrição viaja junto com
/// o AE Title de propósito: é propriedade do aparelho, não do exame nem da rede — foi por ele ter
/// sido global que o remendo do Fuji truncava a worklist de todo mundo. O código do procedimento
/// (ADR-0074) segue a mesma régua: cada aparelho diz se quer e em que sequência; o padrão é não
/// mandar, e o item sai como sempre saiu. Os campos opcionais (médico, unidade, código fora da
/// sequência) também.
/// </summary>
public sealed record EstacaoWorklist(
    string AeTitle,
    int DescricaoMaxCaracteres,
    DestinoCodigoProcedimento CodigoProcedimentoDestinos = DestinoCodigoProcedimento.Nenhum,
    string? CodigoProcedimentoEsquema = null,
    CamposOpcionaisWorklist? Campos = null);

/// <summary>
/// Campos opcionais do item de worklist, ligados aparelho a aparelho (ADR-0074, adendo de
/// 16/10/2026). Tag nula = o campo não vai (médico, unidade) ou vai no padrão (código, em (0008,0100)
/// dentro da sequência). Tags em 8 dígitos hexadecimais, como gravadas no equipamento.
/// </summary>
public sealed record CamposOpcionaisWorklist(
    string? MedicoSolicitanteTag = null,
    string? UnidadeTag = null,
    OrigemUnidadeWorklist UnidadeOrigem = OrigemUnidadeWorklist.Executante,
    string? CodigoProcedimentoTag = null)
{
    /// <summary>Do cadastro do equipamento; null quando nada está ligado (o caso de quase todos).</summary>
    public static CamposOpcionaisWorklist? De(Equipamento e) =>
        De(e.MedicoSolicitanteTag, e.UnidadeTag, e.UnidadeOrigem, e.CodigoProcedimentoTag);

    public static CamposOpcionaisWorklist? De(
        string? medicoTag, string? unidadeTag, OrigemUnidadeWorklist unidadeOrigem, string? codigoTag) =>
        medicoTag is null && unidadeTag is null && codigoTag is null
            ? null
            : new(medicoTag, unidadeTag, unidadeOrigem, codigoTag);
}

/// <summary>
/// Descobre qual estação (AE Title) deve executar um exame, para carimbar o
/// ScheduledStationAETitle (0040,0001) e o WorklistLabel (0074,1202) do item de
/// worklist — o primeiro é o filtro que o equipamento usa no C-FIND MWL, o segundo
/// é a trava equivalente do lado do servidor.
/// </summary>
public interface IResolvedorEstacaoWorklist
{
    /// <summary>
    /// AE Title da estação para <paramref name="exame"/>. Usa o equipamento ESCOLHIDO
    /// (<see cref="ExameImagem.EquipamentoId"/>) quando houver; senão deduz pela unidade
    /// executante + modalidade. Lança <see cref="Common.Excecoes.ConflitoException"/>:
    /// <c>worklist.sem_equipamento</c> quando não há nenhum, e
    /// <c>worklist.equipamento_ambiguo</c> quando há mais de um e ninguém escolheu —
    /// o sistema não sorteia estação.
    /// </summary>
    Task<EstacaoWorklist> ResolverAsync(ExameImagem exame, CancellationToken cancellationToken = default);

    /// <summary>
    /// Equipamentos elegíveis para o exame (unidade executante + modalidade, ativos e com
    /// AE Title válido). Vazio = nenhum cadastrado; mais de um = a recepção precisa escolher
    /// ao autorizar. Ordenado por nome.
    /// </summary>
    Task<IReadOnlyList<EquipamentoCandidato>> ListarCandidatosAsync(
        ExameImagem exame, CancellationToken cancellationToken = default);
}
