using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.RoboAtendimento.Dtos;

/// <summary>Uma condição de ativação (pré-match barato) de um assunto do robô.</summary>
public sealed record RoboAssuntoCondicaoDto(
    TipoCondicaoRobo Tipo,
    string Valor,
    bool Ativo,
    int Ordem);

/// <summary>Um treino (instrução/exemplo/glossário/do/don't) de um assunto do robô.</summary>
public sealed record RoboAssuntoTreinoDto(
    TipoTreinoRobo Tipo,
    string? Titulo,
    string Conteudo,
    int Ordem,
    bool Ativo);

/// <summary>Detalhe de um assunto do robô, com seus filhos e comandos habilitados.</summary>
public sealed record RoboAssuntoDto(
    Guid Id,
    string Nome,
    string? Descricao,
    string InstrucoesPersona,
    string? Modelo,
    bool Ativo,
    TimeOnly? HorarioInicio,
    TimeOnly? HorarioFim,
    int? DiasSemana,
    int MaxInteracoesSemResolver,
    double LimiarConfianca,
    Guid? EscalonamentoUnidadeId,
    string? EscalonamentoUnidadeNome,
    int Ordem,
    IReadOnlyList<RoboAssuntoCondicaoDto> Condicoes,
    IReadOnlyList<RoboAssuntoTreinoDto> Treinos,
    IReadOnlyList<ComandoRobo> Comandos,
    DateTime CriadoEm);

/// <summary>Item da listagem de assuntos.</summary>
public sealed record RoboAssuntoListItemDto(
    Guid Id,
    string Nome,
    string? Descricao,
    bool Ativo,
    string? Modelo,
    int Ordem,
    int QtdComandos);

/// <summary>Payload de criar/atualizar assunto (carrega os filhos e os comandos habilitados).</summary>
public sealed record SalvarRoboAssuntoRequest(
    string Nome,
    string? Descricao,
    string InstrucoesPersona,
    string? Modelo,
    bool Ativo,
    TimeOnly? HorarioInicio,
    TimeOnly? HorarioFim,
    int? DiasSemana,
    int MaxInteracoesSemResolver,
    double LimiarConfianca,
    Guid? EscalonamentoUnidadeId,
    int Ordem,
    IReadOnlyList<RoboAssuntoCondicaoDto> Condicoes,
    IReadOnlyList<RoboAssuntoTreinoDto> Treinos,
    IReadOnlyList<ComandoRobo> Comandos);

/// <summary>Item do catálogo fixo de comandos, para a tela ligar/desligar por assunto.</summary>
/// <param name="Escrita"><c>true</c> quando o comando altera dado (aviso na UI).</param>
public sealed record ComandoRoboCatalogoDto(
    ComandoRobo Comando,
    string Rotulo,
    string Descricao,
    bool Escrita);
