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
    string? CriadoPorNome,
    // ---- Ponteiro de retomada ----
    // Aparecem na tela porque uma rodada de horas que reinicia precisa poder ser acompanhada:
    // sem isso, "Interrompida" fica indistinguível de travada.
    FaseVarreduraSer Fase,
    SituacaoSer? CursorSituacao,
    DateOnly? CursorData,
    string? CursorIdSer,
    int HistoricosPendentes,
    int Retomadas,
    DateTime? RetomadaEm,

    /// <summary>Quando o motor gravou progresso pela última vez. É o que distingue "trabalhando
    /// numa fatia grande" de "pendurada" — contador parado, sozinho, não diz qual dos dois.</summary>
    DateTime? UltimoSinalEm);

/// <summary>
/// Consulta DIRETA ao SER — a "tela de testes": mesmos filtros da busca de lá, resultado cru,
/// <b>nada é gravado</b>. Serve para validar o motor (login, módulo, ViewState, parsers) com um
/// clique, sem disparar uma varredura inteira.
/// </summary>
public sealed record SerConsultaDiretaRequest
{
    public SituacaoSer Situacao { get; init; } = SituacaoSer.EmFila;
    public TipoRecursoSer? Tipo { get; init; }
    public DateOnly? DataSolicitacaoInicio { get; init; }
    public DateOnly? DataSolicitacaoFim { get; init; }
    public string? Cpf { get; init; }
    public string? Nome { get; init; }
    public string? Cns { get; init; }
    public string? IdSolicitacao { get; init; }

    /// <summary>Página do datascroller (1..5 — a tela do SER não vai além). Ignorado no export.</summary>
    public int Pagina { get; init; } = 1;

    /// <summary>
    /// Consultar pela tela de <b>Histórico</b> (export de 500) em vez da tela de Solicitação
    /// (paginada, teto de 100).
    ///
    /// <para>É o caminho que a varredura usa de verdade desde 06/08/2026, e o único que permite
    /// <b>contar</b> alguma coisa: a tela de Solicitação trava em 100 por construção, então
    /// conferir cobertura por ela é impossível. Continua somente leitura — nada é gravado.</para>
    /// </summary>
    public bool PorExport { get; init; }

    /// <summary>
    /// No export, aplicar o filtro de unidade solicitante (<c>GESTOR SMS MARICA</c>), amarrado
    /// pela ida-e-volta do autocomplete (docs/ser.md §4.3).
    ///
    /// <para><b>Ligado por padrão desde 07/08/2026:</b> foi medido que sem a amarração a consulta
    /// devolve a fila do <b>Estado inteiro</b> — PII de pacientes de outros municípios — e em
    /// ordem instável entre chamadas. Desligar é escolha deliberada (diagnóstico), nunca default:
    /// a justificativa antiga ("texto não resolvido pode zerar a consulta") caiu junto com a
    /// teoria do texto puro.</para>
    /// </summary>
    public bool FiltrarPorSolicitante { get; init; } = true;
}

/// <summary>Qual tela do SER respondeu — muda o teto e o que a resposta significa.</summary>
public enum FonteConsultaSer
{
    /// <summary>Tela de Solicitação: 20 por página, 5 páginas, corta em 100 <b>em silêncio</b>.</summary>
    TelaSolicitacao = 1,

    /// <summary>Tela de Histórico: até 500 num .xls, e <b>avisa</b> quando corta.</summary>
    ExportHistorico = 2,
}

/// <summary>Linha crua devolvida pelo SER, exatamente como o parser leu.</summary>
public sealed record SerLinhaDiretaDto(
    string IdSer,
    string? Tipo,
    string? Recurso,
    string? DataSolicitacao,
    string? Paciente,
    string? Idade,
    string? Cpf,
    string? Cns,
    string? Cid,
    string? Solicitante,
    string? MunicipioSolicitante,
    string? AgendadoPara,
    string? Situacao);

/// <summary>Resultado da consulta direta, com o diagnóstico que interessa a quem testa.</summary>
public sealed record SerConsultaDiretaDto(
    IReadOnlyList<SerLinhaDiretaDto> Linhas,
    /// <summary>Páginas que o datascroller expôs. <b>5 = bateu no teto de 100</b> da tela do SER.
    /// Sempre 0 no export: aquela tela devolve o lote inteiro de uma vez, não pagina.</summary>
    int Paginas,
    bool BateuNoTeto,
    int DuracaoMs,
    FonteConsultaSer Fonte,
    /// <summary>
    /// O aviso de corte, <b>com as palavras do próprio SER</b>, ou nulo quando o lote veio
    /// inteiro. Só a tela de Histórico avisa; por isso "sem aviso" ali significa cobertura
    /// completa daquele recorte, enquanto na tela de Solicitação não significa nada.
    /// </summary>
    string? AvisoDoSer);

/// <summary>Histórico lido ao vivo de uma solicitação, para conferir contra a tela do SER.</summary>
public sealed record SerHistoricoDiretoDto(
    string IdSer,
    IReadOnlyDictionary<string, string> Paciente,
    IReadOnlyList<SerEventoDiretoDto> Eventos,
    int DuracaoMs);

public sealed record SerEventoDiretoDto(
    string? Data,
    string? Evento,
    string? EstadoAnterior,
    string? EstadoAtual,
    string? CentralRegulacao,
    string? UnidadeExecutora,
    string? Usuario,
    string? LotacaoEvento,
    string? Ip,
    string? Observacao);

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

// ---------------------------------------------------------------- notificações
// A fila de gatilhos vista pela regulação: o que mudou no SER e ainda ninguém olhou.

/// <summary>Um movimento por ler. Traz junto os dados da solicitação porque a tela precisa
/// identificar o paciente sem um segundo request por linha.</summary>
public sealed record SerNotificacaoDto(
    Guid Id,

    /// <summary>Id interno da solicitação — é por ele que a tela abre o detalhe completo, sem
    /// precisar de um endpoint novo só para resolver o IdSer do SER.</summary>
    Guid SolicitacaoId,

    string IdSer,
    TipoGatilhoSer Tipo,
    SituacaoSer? SituacaoAnterior,
    SituacaoSer? SituacaoAtual,
    DateTime CriadoEm,
    TipoRecursoSer? TipoRecurso,
    string? PacienteNome,
    string? Recurso,
    DateOnly? DataSolicitacao,
    string? AgendadoParaTexto,
    string? UnidadeExecutora);

public sealed record SerNotificacaoPaginaDto(
    IReadOnlyList<SerNotificacaoDto> Itens, int Total, int Pagina, int Tamanho);

/// <summary>Quantos movimentos por ler existem em cada (tipo de recurso, situação). É o que
/// alimenta as abas Consulta/Exame e os números por situação dentro delas.</summary>
public sealed record SerNotificacaoContadorDto(
    TipoRecursoSer? Tipo, SituacaoSer Situacao, int Quantidade);

public sealed record SerNotificacaoResumoDto(
    int Total, IReadOnlyList<SerNotificacaoContadorDto> Contadores);

public sealed record SerNotificacaoFiltroDto
{
    public TipoRecursoSer? Tipo { get; init; }
    public SituacaoSer? Situacao { get; init; }

    /// <summary>Filtra pelo que provocou a notificação (mudança de situação, FollowUP novo…).</summary>
    public TipoGatilhoSer? TipoGatilho { get; init; }

    public int Pagina { get; init; } = 1;
    public int Tamanho { get; init; } = 50;
}

// ---------------------------------------------------------------- nova solicitação
// O formulário de criação do SER, lido AO VIVO. Ver docs/ser-criar-solicitacao.md.

public sealed record SerOpcaoDto(string Valor, string Rotulo);

/// <summary>
/// Um campo que o SER acrescenta conforme o Recurso escolhido. É o que faz oncologia pedir peso,
/// altura, IMC e datas de biópsia enquanto uma consulta comum pede só três textos.
/// </summary>
public sealed record SerCampoDinamicoDto(
    string Numero,
    /// <summary>Nome JSF do campo — é por ele que o valor viajaria no envio.</summary>
    string Campo,
    string Rotulo,
    /// <summary><c>text</c>, <c>textarea</c>, <c>select</c>, <c>radio</c> ou <c>checkbox</c>.</summary>
    string Tipo,
    bool Obrigatorio,
    IReadOnlyList<SerOpcaoDto>? Opcoes);

/// <summary>Bloco fixo do formulário: vale para todo pedido, independente do recurso.</summary>
public sealed record SerFormularioNovaDto(
    IReadOnlyList<SerOpcaoDto> AmbulatorioEstadual,
    IReadOnlyList<SerOpcaoDto> Tipos,
    IReadOnlyList<SerOpcaoDto> ClassificacoesRisco,
    IReadOnlyList<SerOpcaoDto> Medicos,
    IReadOnlyList<SerCampoDinamicoDto> CamposDinamicosPadrao);

// ---------------------------------------------------------------- rascunhos
// Pedidos montados na NOSSA base, com anexos, esperando autorização para ir ao SER.

public sealed record SerRascunhoListaDto(
    Guid Id,
    StatusRascunhoSer Status,
    TipoRecursoSer? Tipo,
    string? RecursoRotulo,
    string? PacienteNome,
    string? Cns,
    string? Hipotese,
    string? IdSerGerado,
    string? CriadoPorNome,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    DateTime? EnviadoEm,
    int Anexos);

public sealed record SerRascunhoAnexoDto(
    Guid Id,
    Guid MidiaId,
    string NomeArquivo,
    string? ContentType,
    long Tamanho,
    /// <summary>Nulo = o arquivo ainda é só nosso; não subiu para o SER.</summary>
    DateTime? EnviadoEm,
    DateTime CriadoEm);

public sealed record SerRascunhoDetalheDto(
    Guid Id,
    StatusRascunhoSer Status,
    TipoRecursoSer? Tipo,
    string? RecursoValor,
    string? RecursoRotulo,
    string? Cns,
    string? PacienteNome,
    string? Hipotese,
    /// <summary>Valores do formulário com os nomes JSF do SER como chave — é o que seria postado.</summary>
    IReadOnlyDictionary<string, string> Campos,
    string? IdSerGerado,
    string? MensagemErro,
    string? CriadoPorNome,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    DateTime? EnviadoEm,
    IReadOnlyList<SerRascunhoAnexoDto> Anexos);

public sealed record SerRascunhoRequest
{
    public TipoRecursoSer? Tipo { get; init; }
    public string? RecursoValor { get; init; }
    public string? RecursoRotulo { get; init; }
    public string? Cns { get; init; }
    public string? PacienteNome { get; init; }
    public string? Hipotese { get; init; }
    public Dictionary<string, string>? Campos { get; init; }
}

// ---------------------------------------------------------------- catálogo espelhado

/// <summary>O formulário montado a partir do NOSSO catálogo — sem tocar no SER.</summary>
public sealed record SerCatalogoFormularioDto(
    IReadOnlyList<SerOpcaoDto> AmbulatorioEstadual,
    IReadOnlyList<SerOpcaoDto> ClassificacoesRisco,
    IReadOnlyList<SerOpcaoDto> Medicos,
    IReadOnlyList<SerCatalogoRecursoDto> Recursos,
    DateTime? SincronizadoEm,
    /// <summary>Recursos cujos campos ainda não foram lidos — o catálogo está incompleto.</summary>
    int RecursosSemCampos);

public sealed record SerCatalogoRecursoDto(
    TipoRecursoSer Tipo, string Valor, string Rotulo, bool CamposLidos);
