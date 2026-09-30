using SMSMais.Core.Regulacao.Regras;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.AnaliseRegras.Dtos;

/// <summary>O que a fila mostra ao lado de um pedido de espelho (ADR-0063 §4).</summary>
public sealed record AnaliseRegrasResumoDto(
    VereditoAnaliseRegras Veredito,
    string? Resumo,
    int Bloqueios,
    int Ressalvas,
    int PerguntasPendentes,
    int DocumentosPendentes,
    DateTime AnalisadoEm);

/// <summary>O que o detalhe mostra: o resumo e cada regra avaliada, com resultado e motivo.</summary>
public sealed record AnaliseRegrasDetalheDto(
    AnaliseRegrasResumoDto Resumo,
    Guid? ProcedimentoId,
    string? ProcedimentoNome,
    IReadOnlyList<RegraAvaliadaDto> Regras,
    IReadOnlyList<PerguntaPendenteDto> Perguntas,
    IReadOnlyList<string> Documentos);

/// <summary>Contagem por veredito — os cartões da fila.</summary>
public sealed record AnaliseRegrasContagemDto(VereditoAnaliseRegras Veredito, int Quantidade);
