using SMSMais.Core.Regulacao.AnaliseRegras.Dtos;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.EsusSg.Dtos;

public sealed record EsusSgSolicitacaoListaDto(
    Guid Id,
    string IdEsusSg,
    TipoRecursoEsusSg Tipo,
    string Recurso,
    DateOnly? DataSolicitacao,
    DateOnly? DataEntradaFila,
    int? DiasNaFila,
    string? Prioridade,
    string? PrioridadeCor,
    string? Pendencia,
    int? PosicaoFila,
    string PacienteNome,
    Guid? PacienteId,
    string? Cpf,
    string? Cns,
    DateOnly? DataNascimento,
    string? UnidadeExecutora,
    DateOnly? DataAgendada,
    string? DataHoraAgendadaTexto,
    string? NotificacaoResposta,
    SituacaoEsusSg Situacao,
    SituacaoEsusSg? SituacaoAnterior,
    DateTime? SituacaoMudouEm,
    DateTime SincronizadoEm,
    int EventosCount,
    AnaliseRegrasResumoDto? Analise);

public sealed record EsusSgSolicitacaoDetalheDto(
    EsusSgSolicitacaoListaDto Resumo,
    string? CodigoInterno,
    string? Subprocedimentos,
    string? ProfissionalSolicitante,
    string? UnidadeSolicitante,
    string? UsuarioInclusao,
    string? Regulador,
    int? OrdemEntrada,
    string? Sexo,
    string? NomeMae,
    string? Telefone,
    string? Celular,
    string? MunicipioPaciente,
    string? Bairro,
    string? CnesExecutora,
    string? Setor,
    string? Local,
    string? UsuarioAgendamento,
    DateOnly? AgendamentoCadastradoEm,
    DateOnly? DataSaidaFila,
    bool? ComprovanteImpresso,
    bool? AgendadoTfd,
    string? NotificacaoTipo,
    string? NotificacaoEntrega,
    DateTime? VistoNaFilaEm,
    DateTime? VistoNosAgendadosEm,
    IReadOnlyList<EsusSgEventoDto> Eventos,
    AnaliseRegrasDetalheDto? Analise);

public sealed record EsusSgEventoDto(
    Guid Id,
    DateTime DataEvento,
    string Evento,
    TipoEventoExterno TipoEvento,
    string? EstadoAnterior,
    string? EstadoAtual,
    string? UnidadeExecutora,
    string? Usuario,
    string? LotacaoEvento,
    string? Observacao);

public sealed record EsusSgBuscaFiltroDto
{
    public SituacaoEsusSg? Situacao { get; init; }
    public TipoRecursoEsusSg? Tipo { get; init; }

    /// <summary>Nome, CPF, CNS ou número do pedido no ESUS.</summary>
    public string? Termo { get; init; }

    public string? Recurso { get; init; }
    public string? Prioridade { get; init; }
    public VereditoAnaliseRegras? Veredito { get; init; }
    public DateOnly? EntradaInicio { get; init; }
    public DateOnly? EntradaFim { get; init; }
    public DateOnly? AgendadaInicio { get; init; }
    public DateOnly? AgendadaFim { get; init; }
    public DateTime? MudouDesde { get; init; }
    public int Pagina { get; init; } = 1;
    public int Tamanho { get; init; } = 25;
}

public sealed record EsusSgBuscaResultadoDto(
    IReadOnlyList<EsusSgSolicitacaoListaDto> Itens, int Total, int Pagina, int Tamanho);

public sealed record EsusSgResumoSituacaoDto(SituacaoEsusSg Situacao, int Quantidade);

public sealed record EsusSgResumoDto(
    IReadOnlyList<EsusSgResumoSituacaoDto> PorSituacao,
    IReadOnlyList<AnaliseRegrasContagemDto> PorVeredito,
    IReadOnlyList<EsusSgContagemTextoDto> PorRecurso,
    int AgendadosProximos30Dias);

public sealed record EsusSgContagemTextoDto(string Texto, int Quantidade);

public sealed record EsusSgStatusMotorDto(
    bool CredencialConfigurada,
    string? Usuario,
    string Cliente,
    bool VarreduraEmAndamento,
    int TotalSolicitacoes,
    int TotalEventos,
    int GatilhosPendentes,
    int RecursosNoCatalogo,
    EsusSgExecucaoDto? UltimaExecucao);

public sealed record EsusSgExecucaoDto(
    Guid Id,
    ModoVarreduraEsusSg Modo,
    DisparoSincronizacao Disparo,
    StatusVarreduraEsusSg Status,
    DateOnly JanelaInicio,
    DateOnly JanelaFim,
    int Requisicoes,
    int NaFila,
    int AgendadosLidos,
    int SolicitacoesNovas,
    int SolicitacoesAtualizadas,
    int MudancasSituacao,
    int SaidasDaFila,
    int EventosNovos,
    int GatilhosGerados,
    int MesesIncompletos,
    string? MensagemErro,
    DateTime IniciadoEm,
    DateTime? FinalizadoEm,
    int? DuracaoSegundos,
    string? CriadoPorNome,
    FaseVarreduraEsusSg Fase,
    DateOnly? CursorMes,
    int Retomadas,
    DateTime? RetomadaEm,
    DateTime? UltimoSinalEm);

public sealed record EsusSgDispararVarreduraDto
{
    public ModoVarreduraEsusSg Modo { get; init; } = ModoVarreduraEsusSg.Diaria;
}

public sealed record EsusSgCredencialRequest(string Usuario, string Senha, string? Cliente);

public sealed record EsusSgVarreduraConfigDto(bool Ativo, string HoraLocal);
