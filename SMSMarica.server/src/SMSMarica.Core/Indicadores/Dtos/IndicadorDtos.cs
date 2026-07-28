using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Indicadores.Dtos;

/// <summary>Item da listagem de uma aba — a linha que a tela desenha, espelhando a planilha.</summary>
/// <remarks>
/// Os campos numéricos de meta (<see cref="MetaOperador"/>, <see cref="MetaValor"/>,
/// <see cref="MetaValorMaximo"/>) e o <see cref="FatorDensidade"/> vêm junto da listagem para a
/// exportação em Excel poder escrever as fórmulas de meta (tudo-ou-nada) e de resultado
/// (numerador ÷ denominador × fator) direto nas células — sem uma segunda chamada por indicador.
///
/// Pelo mesmo motivo vêm <see cref="MemoriaCalculo"/>, <see cref="FonteDeclarada"/> e
/// <see cref="Sql"/>: a exportação precisa ser um documento auditável e autossuficiente — quem
/// recebe a planilha tem que ver de onde o número saiu sem pedir acesso ao sistema.
/// </remarks>
public sealed record IndicadorResumoDto(
    Guid Id,
    AbaIndicador Aba,
    string Numero,
    int Ordem,
    Guid? IndicadorPaiId,
    string Nome,
    string? Meta,
    MetaOperador? MetaOperador,
    decimal? MetaValor,
    decimal? MetaValorMaximo,
    decimal? Pontuacao,
    TipoResultadoIndicador TipoResultado,
    string? UnidadeMedida,
    decimal? FatorDensidade,
    SituacaoIndicador Situacao,
    bool TemMotor,
    string? Ressalva,
    bool Ativo,
    ResultadoIndicadorDto? Resultado,
    string? MemoriaCalculo,
    string? FonteDeclarada,
    string? FonteNome,
    string? Sql,
    /// <summary>Tem SQL analítico cadastrado — dá para pedir a evidência linha a linha.</summary>
    bool TemAnalitico);

/// <summary>Detalhe completo — tudo é editável, porque a planilha contratual muda.</summary>
public sealed record IndicadorDetalheDto(
    Guid Id,
    AbaIndicador Aba,
    string Numero,
    int Ordem,
    Guid? IndicadorPaiId,
    string Nome,
    string? MemoriaCalculo,
    string? FonteDeclarada,
    string? Meta,
    MetaOperador? MetaOperador,
    decimal? MetaValor,
    decimal? MetaValorMaximo,
    decimal? Pontuacao,
    TipoResultadoIndicador TipoResultado,
    string? UnidadeMedida,
    decimal? FatorDensidade,
    SituacaoIndicador Situacao,
    Guid? FonteId,
    string? FonteNome,
    string? Sql,
    string? SqlAnalitico,
    string? Ressalva,
    bool Ativo,
    int TotalVersoes);

/// <summary>Resultado apurado de um indicador para o período filtrado.</summary>
public sealed record ResultadoIndicadorDto(
    decimal? Valor,
    decimal? Numerador,
    decimal? Denominador,
    IReadOnlyList<LinhaDistribuicaoDto>? Distribuicao,
    bool? AtingiuMeta,
    decimal? PontuacaoApurada,
    int DuracaoMs,
    DateTime ExecutadoEm,
    string? Erro);

public sealed record LinhaDistribuicaoDto(string Rotulo, decimal Quantidade);

/// <summary>
/// Evidência linha a linha por trás de um indicador: os registros que entraram na conta e os
/// que foram excluídos, com o motivo. É o retorno cru do SQL analítico — colunas do jeito que a
/// consulta as nomeou, sem interpretação —, porque um relatório auditável não pode ter uma
/// camada de tradução escondendo o que o banco devolveu.
/// </summary>
/// <param name="Colunas">Nomes das colunas, na ordem em que o SQL as devolveu.</param>
/// <param name="Linhas">Uma lista por registro, alinhada a <paramref name="Colunas"/>.</param>
/// <param name="IndiceIncluido">Posição da coluna <c>incluido</c> em <paramref name="Colunas"/>; -1 se o SQL não a trouxe.</param>
/// <param name="Truncado">O retorno bateu no teto de linhas — a evidência está incompleta e a planilha precisa dizer isso.</param>
public sealed record AnaliticoIndicadorDto(
    Guid IndicadorId,
    string Numero,
    string Nome,
    IReadOnlyList<string> Colunas,
    IReadOnlyList<IReadOnlyList<object?>> Linhas,
    int IndiceIncluido,
    int IndiceMotivo,
    int Incluidos,
    int Excluidos,
    bool Truncado,
    int LimiteLinhas,
    int DuracaoMs,
    DateTime ExecutadoEm,
    string? Erro);

/// <summary>Filtro do topo da página: unidade (hospital de origem) + período.</summary>
public sealed record FiltroIndicadorDto(int Hospital, DateOnly Inicio, DateOnly Fim);

/// <summary>
/// Cadastro completo do indicador. Tudo é configurável de propósito: meta, peso e forma de
/// cálculo são cláusula de contrato e mudam entre versões da planilha — mudar isso não pode
/// exigir deploy.
/// </summary>
public sealed record SalvarIndicadorDto(
    AbaIndicador Aba,
    string Numero,
    int Ordem,
    Guid? IndicadorPaiId,
    string Nome,
    string? MemoriaCalculo,
    string? FonteDeclarada,
    string? Meta,
    MetaOperador? MetaOperador,
    decimal? MetaValor,
    decimal? MetaValorMaximo,
    decimal? Pontuacao,
    TipoResultadoIndicador TipoResultado,
    string? UnidadeMedida,
    decimal? FatorDensidade,
    SituacaoIndicador Situacao,
    Guid? FonteId,
    string? Sql,
    string? SqlAnalitico,
    string? Ressalva,
    bool Ativo,
    /// <summary>O que mudou nesta gravação — vira a nota da versão quando o SQL muda.</summary>
    string? Nota);

public sealed record IndicadorVersaoDto(
    Guid Id,
    int Numero,
    string Sql,
    string? Nota,
    DateTime CriadoEm,
    string? CriadoPorNome);

/// <summary>Unidade disponível no filtro (hospital na base de origem).</summary>
public sealed record UnidadeIndicadorDto(int Hospital, string Nome);

/// <summary>Base de dados disponível para rodar o motor.</summary>
public sealed record FonteIndicadorDto(Guid Id, string Nome);
