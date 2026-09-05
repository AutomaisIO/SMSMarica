using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.Catalogo.Dtos;

/// <summary>Retrato de uma passada do sincronismo do catálogo canônico.</summary>
public sealed record RegulacaoCatalogoSyncResultadoDto(
    int OrigensNovas,
    int OrigensDesativadas,
    int CanonicosNovos,
    int EmbeddingsGerados,
    int SemEmbedding,
    int Sugestoes);

/// <summary>Como um procedimento aparece em um dos sistemas de regulação.</summary>
public sealed record RegulacaoOrigemDto(
    Guid Id,
    SistemaRegulacao Sistema,
    string Rotulo,
    string? Ramo,
    string ChaveExterna);

/// <summary>Unidade de Maricá que executa o procedimento, com a oferta que o SISREG mostra.</summary>
public sealed record ExecutanteInternoDto(
    Guid UnidadeId,
    string Nome,
    string? Cnes,
    int VagasTotal,
    DateOnly? ProximaVigencia);

/// <summary>
/// Em quais sistemas externos o procedimento existe. Os dois ramos do SER aparecem separados
/// porque mudam o formulário e as regras do mesmo recurso.
/// </summary>
public sealed record ExisteExternoDto(bool Ser, bool SerAmbulatorioEstadual, bool Sernit);

public sealed record RegulacaoProcedimentoItemDto(
    Guid Id,
    string Nome,
    TipoProcedimentoRegulacao Tipo,
    double Score,
    IReadOnlyList<RegulacaoOrigemDto> Origens,
    IReadOnlyList<ExecutanteInternoDto> ExecutantesInternos,
    ExisteExternoDto ExisteExterno);

/// <summary>
/// Resultado da busca. <paramref name="Degradada"/> avisa que o provedor de embeddings falhou e
/// só a parte lexical respondeu — a tela mostra isso, em vez de fingir que não achou nada.
/// </summary>
public sealed record RegulacaoBuscaResultadoDto(
    IReadOnlyList<RegulacaoProcedimentoItemDto> Itens,
    bool Degradada);

public sealed record RegulacaoProcedimentoDetalheDto(
    Guid Id,
    string Nome,
    TipoProcedimentoRegulacao Tipo,
    string? CodigoSigtap,
    IReadOnlyList<RegulacaoOrigemDto> Origens,
    IReadOnlyList<ExecutanteInternoDto> ExecutantesInternos,
    ExisteExternoDto ExisteExterno);

/// <summary>Par que o robô propôs e que só a curadoria confirma.</summary>
public sealed record RegulacaoSugestaoPareamentoDto(
    Guid OrigemId,
    SistemaRegulacao Sistema,
    string Rotulo,
    Guid CanonicoAtualId,
    string CanonicoAtual,
    Guid SugeridoId,
    string SugeridoNome,
    double Score);
