using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Ser.Dtos;

/// <summary>
/// Uma linha da nossa tela de fila do SER. Os campos espelham as colunas da grade do SER de
/// propósito — o operador reconhece a tela — mas com os dados vindos do NOSSO banco.
/// </summary>
public sealed record SerSolicitacaoListaDto(
    Guid Id,
    string IdSer,
    TipoRecursoSer Tipo,
    string Recurso,
    DateOnly? DataSolicitacao,
    string PacienteNome,
    string? IdadeTexto,
    string? Cpf,
    string? Cns,
    string? Cid,
    string? SolicitanteNome,
    string? MunicipioSolicitante,
    string? AgendadoParaTexto,
    SituacaoSer Situacao,
    SituacaoSer? SituacaoAnterior,
    DateTime? SituacaoMudouEm,
    DateTime SincronizadoEm,
    DateTime? HistoricoLidoEm,
    int EventosCount,
    bool HistoricoIndisponivel,
    /// <summary>Quantos dias a solicitação está esperando desde que foi criada no SER. É a
    /// pergunta que a regulação faz, e calculá-la no servidor evita que cada tela repita a conta.</summary>
    int? DiasNaFila);

/// <summary>Detalhe completo de uma solicitação, com os dados que só existem no histórico.</summary>
public sealed record SerSolicitacaoDetalheDto(
    SerSolicitacaoListaDto Resumo,
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
    IReadOnlyList<SerEventoDto> Eventos);

/// <summary>Um evento da trilha, como o SER mostra.</summary>
public sealed record SerEventoDto(
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

/// <summary>Filtros da NOSSA busca (não vai ao SER — lê o espelho).</summary>
public sealed record SerBuscaFiltroDto
{
    public SituacaoSer? Situacao { get; init; }
    public TipoRecursoSer? Tipo { get; init; }

    /// <summary>Busca livre: casa contra nome do paciente, CPF, CNS, ID do SER e recurso.</summary>
    public string? Termo { get; init; }

    public DateOnly? DataSolicitacaoInicio { get; init; }
    public DateOnly? DataSolicitacaoFim { get; init; }

    /// <summary>Só as que mudaram de situação desde esta data — é o "o que mudou hoje".</summary>
    public DateTime? MudouDesde { get; init; }

    public int Pagina { get; init; } = 1;
    public int Tamanho { get; init; } = 25;
}

/// <summary>Página de resultados.</summary>
public sealed record SerBuscaResultadoDto(
    IReadOnlyList<SerSolicitacaoListaDto> Itens,
    int Total,
    int Pagina,
    int Tamanho);

/// <summary>Contagem por situação — os cards do topo da tela.</summary>
public sealed record SerResumoSituacaoDto(SituacaoSer Situacao, int Quantidade);

/// <summary>Estado do motor, para a aba SER da Configuração da Regulação.</summary>
public sealed record SerStatusMotorDto(
    bool CredencialConfigurada,
    bool VarreduraEmAndamento,
    int TotalSolicitacoes,
    int TotalEventos,
    int GatilhosPendentes,
    SerExecucaoDto? UltimaExecucao);

/// <summary>Uma rodada do motor.</summary>
public sealed record SerExecucaoDto(
    Guid Id,
    ModoVarreduraSer Modo,
    DisparoSincronizacao Disparo,
    StatusVarreduraSer Status,
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
    string? CriadoPorNome);

/// <summary>Pedido de varredura vindo da tela.</summary>
public sealed record SerDispararVarreduraDto
{
    public ModoVarreduraSer Modo { get; init; } = ModoVarreduraSer.Diaria;

    /// <summary>Início da janela de Data da Solicitação. Default: 01/01/2016 (a solicitação mais
    /// antiga vista em produção é de 2016).</summary>
    public DateOnly? Inicio { get; init; }

    public DateOnly? Fim { get; init; }

    /// <summary>Vazio = todas as situações.</summary>
    public IReadOnlyList<SituacaoSer>? Situacoes { get; init; }
}
