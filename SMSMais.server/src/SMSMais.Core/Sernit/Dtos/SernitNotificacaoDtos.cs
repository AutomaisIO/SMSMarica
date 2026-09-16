using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Sernit.Dtos;

/// <summary>Um movimento por ler do SERNIT — traz junto os dados da solicitação para a tela
/// identificar o paciente sem um segundo request por linha.</summary>
public sealed record SernitNotificacaoDto(
    Guid Id,
    Guid SolicitacaoId,
    string IdSernit,
    TipoGatilhoSernit Tipo,
    SituacaoSernit? SituacaoAnterior,
    SituacaoSernit? SituacaoAtual,
    DateTime CriadoEm,
    TipoRecursoSernit? TipoRecurso,
    string? PacienteNome,
    Guid? PacienteId,
    string? Recurso,
    DateOnly? DataSolicitacao,
    string? AgendadoParaTexto,
    string? UnidadeExecutora,
    /// <summary>O FollowUP mais recente da trilha da solicitação, em QUALQUER notificação — não
    /// só na de "FollowUP novo". Null quando a solicitação nunca teve FollowUP.</summary>
    SernitFollowUpResumoDto? UltimoFollowUp);

/// <summary>Resumo de um FollowUP para o card: quando, quem e o texto (de <c>sernit_evento</c>).</summary>
public sealed record SernitFollowUpResumoDto(DateTime DataEvento, string? Usuario, string? Observacao);

public sealed record SernitNotificacaoPaginaDto(
    IReadOnlyList<SernitNotificacaoDto> Itens, int Total, int Pagina, int Tamanho);

public sealed record SernitNotificacaoContadorDto(
    TipoRecursoSernit? Tipo, SituacaoSernit Situacao, int Quantidade);

public sealed record SernitNotificacaoResumoDto(
    int Total, IReadOnlyList<SernitNotificacaoContadorDto> Contadores);

public sealed record SernitNotificacaoFiltroDto
{
    public TipoRecursoSernit? Tipo { get; init; }
    public SituacaoSernit? Situacao { get; init; }
    public TipoGatilhoSernit? TipoGatilho { get; init; }
    public int Pagina { get; init; } = 1;
    public int Tamanho { get; init; } = 50;
}
