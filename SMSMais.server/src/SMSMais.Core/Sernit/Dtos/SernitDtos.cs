using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Sernit.Dtos;

/// <summary>Uma linha da nossa tela de fila do SERNIT — espelha as colunas do SERNIT, com os dados
/// vindos do NOSSO banco.</summary>
public sealed record SernitSolicitacaoListaDto(
    Guid Id,
    string IdSernit,
    TipoRecursoSernit Tipo,
    string Recurso,
    DateOnly? DataSolicitacao,
    string PacienteNome,
    Guid? PacienteId,
    string? IdadeTexto,
    string? Cpf,
    string? Cns,
    string? Cid,
    string? SolicitanteNome,
    string? MunicipioSolicitante,
    string? AgendadoParaTexto,
    SituacaoSernit Situacao,
    SituacaoSernit? SituacaoAnterior,
    DateTime? SituacaoMudouEm,
    DateTime SincronizadoEm,
    DateTime? HistoricoLidoEm,
    int EventosCount,
    bool HistoricoIndisponivel,
    int? DiasNaFila);

/// <summary>Detalhe completo, com os dados que só existem no histórico.</summary>
public sealed record SernitSolicitacaoDetalheDto(
    SernitSolicitacaoListaDto Resumo,
    string? NomeMae,
    string? Sexo,
    DateOnly? DataNascimento,
    string? Etnia,
    string? Cep,
    string? Uf,
    string? MunicipioPaciente,
    string? Bairro,
    string? TipoLogradouro,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? TelefoneResidencial,
    string? TelefoneWhatsapp,
    string? TelefoneContato,
    IReadOnlyList<SernitEventoDto> Eventos);

/// <summary>Um evento da trilha, como o SERNIT mostra.</summary>
public sealed record SernitEventoDto(
    Guid Id,
    DateTime DataEvento,
    string Evento,
    string? EstadoAnterior,
    string? EstadoAtual,
    string? CentralRegulacao,
    string? UnidadeExecutora,
    string? Usuario,
    string? LotacaoEvento,
    string? Ip,
    string? Observacao);

/// <summary>Filtros da NOSSA busca (lê o espelho, não vai ao SERNIT).</summary>
public sealed record SernitBuscaFiltroDto
{
    public SituacaoSernit? Situacao { get; init; }
    public TipoRecursoSernit? Tipo { get; init; }
    public string? Termo { get; init; }
    public DateOnly? DataSolicitacaoInicio { get; init; }
    public DateOnly? DataSolicitacaoFim { get; init; }
    public DateTime? MudouDesde { get; init; }
    public int Pagina { get; init; } = 1;
    public int Tamanho { get; init; } = 25;
}

public sealed record SernitBuscaResultadoDto(
    IReadOnlyList<SernitSolicitacaoListaDto> Itens, int Total, int Pagina, int Tamanho);

public sealed record SernitResumoSituacaoDto(SituacaoSernit Situacao, int Quantidade);

/// <summary>Estado do motor, para a aba SERNIT da Configuração da Regulação.</summary>
public sealed record SernitStatusMotorDto(
    bool CredencialConfigurada,
    bool VarreduraEmAndamento,
    int TotalSolicitacoes,
    int TotalEventos,
    int GatilhosPendentes,
    SernitExecucaoDto? UltimaExecucao);

/// <summary>Uma rodada do motor.</summary>
public sealed record SernitExecucaoDto(
    Guid Id,
    ModoVarreduraSernit Modo,
    DisparoSincronizacao Disparo,
    StatusVarreduraSernit Status,
    DateOnly JanelaInicio,
    DateOnly JanelaFim,
    string SituacoesVarridas,
    int Buscas,
    int Paginas,
    int SolicitacoesEncontradas,
    int SolicitacoesNovas,
    int SolicitacoesAtualizadas,
    int MudancasSituacao,
    int HistoricosLidos,
    int EventosNovos,
    int FollowUpsNovos,
    int HistoricosIndisponiveis,
    int GatilhosGerados,
    int FatiasTruncadas,
    string? MensagemErro,
    DateTime IniciadoEm,
    DateTime? FinalizadoEm,
    int? DuracaoSegundos,
    string? CriadoPorNome,
    FaseVarreduraSernit Fase,
    SituacaoSernit? CursorSituacao,
    DateOnly? CursorData,
    string? CursorIdSernit,
    int HistoricosPendentes,
    int Retomadas,
    DateTime? RetomadaEm,
    DateTime? UltimoSinalEm);

/// <summary>Pedido de varredura vindo da tela.</summary>
public sealed record SernitDispararVarreduraDto
{
    public ModoVarreduraSernit Modo { get; init; } = ModoVarreduraSernit.Diaria;
    public DateOnly? Inicio { get; init; }
    public DateOnly? Fim { get; init; }
    public IReadOnlyList<SituacaoSernit>? Situacoes { get; init; }
}

/// <summary>Credencial avulsa para teste/gravação (a senha trafega, valida e é cifrada no store).</summary>
public sealed record SernitTestarCredencialRequest(string Usuario, string Senha);
