using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.EscopoExames.Dtos;

/// <summary>
/// Uma linha da aba "Exames de imagem" da unidade. Traz o suficiente para a tela decidir sem
/// segunda chamada: o que é o exame, para onde vai e o que falta configurar.
/// </summary>
/// <param name="Situacao">
/// Resumo pronto para o operador: <c>Configurado</c>, <c>ADefinir</c> (worklist ligada e sem
/// destino, com mais de um aparelho possível) ou <c>Desligado</c>.
/// </param>
public sealed record EscopoExameItemDto(
    Guid Id,
    Guid TipoExameId,
    string TipoExameNome,
    string? CodigoSisreg,
    ModalidadeDicom ModalidadeDicom,
    Guid UnidadeId,
    string UnidadeNome,
    bool EnviarParaWorklist,
    Guid? EquipamentoId,
    string? EquipamentoNome,
    string? EquipamentoAeTitle,
    int EquipamentosCompativeis,
    bool Ativo,
    SituacaoEscopoExame Situacao);

/// <summary>Como a tela pinta a linha. Derivado, nunca gravado.</summary>
public enum SituacaoEscopoExame
{
    /// <summary>Não envia à worklist nesta unidade — estado de quem acabou de chegar pela importação.</summary>
    Desligado = 0,

    /// <summary>Envia e o destino está resolvido (amarrado ou dedutível sem ambiguidade).</summary>
    Configurado = 1,

    /// <summary>Envia, mas o destino depende de escolha: nenhum aparelho compatível, ou mais de um.</summary>
    ADefinir = 2,
}

public sealed record AdicionarEscopoExameRequest(
    Guid TipoExameId,
    Guid UnidadeId,
    bool EnviarParaWorklist = false,
    Guid? EquipamentoId = null);

public sealed record AtualizarEscopoExameRequest(
    bool EnviarParaWorklist,
    Guid? EquipamentoId,
    bool Ativo = true);

/// <summary>Uma linha do painel "Exames a configurar" — a fila de trabalho.</summary>
public sealed record PendenciaEscopoExameDto(
    Guid Id,
    Guid UnidadeId,
    string UnidadeNome,
    Guid TipoExameId,
    string TipoExameNome,
    ModalidadeDicom ModalidadeDicom,
    SituacaoEscopoExame Situacao,
    string OQueFalta,
    int ExamesParados);
