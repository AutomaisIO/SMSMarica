using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Ouvidoria.Dtos;

// DTOs do módulo Ouvidoria (ADR-0060). Contrato do plano SMSMais.ouvidoria/PLANO-FASE-1.md §3.3 —
// o front é feito contra estes nomes; mudança aqui é mudança de contrato.

/// <summary>Página genérica de resultados (não havia genérico de paginação no repo).</summary>
public sealed record PaginaDto<T>(IReadOnlyList<T> Itens, int Total, int Pagina, int Tamanho);

/// <summary>Filtro do <c>GET /ouvidoria/manifestacoes</c> (bindado por query string).</summary>
public sealed record ManifestacaoFiltro(
    OuvidoriaStatus[]? Status,
    OuvidoriaTipo? Tipo,
    Guid? UnidadeId,
    Guid? PontoRespostaId,
    OuvidoriaPrioridade? Prioridade,
    /// <summary>Só manifestações em aberto com o prazo ao cidadão vencido.</summary>
    bool? Atrasadas,
    /// <summary>Só <c>RespondidaPelaArea | EmValidacao</c> (a ouvidoria precisa validar a resposta da área).</summary>
    bool? AguardandoValidacao,
    /// <summary>Protocolo, nome ou CPF do manifestante (nome/CPF só em identificadas não restritas).</summary>
    string? Busca,
    DateOnly? De,
    DateOnly? Ate,
    int Pagina = 1,
    int Tamanho = 50);

/// <summary>Dados do manifestante. Nunca vai ao ponto de resposta; mascarado em sigilosa/denúncia.</summary>
public sealed record ManifestanteDto(string? Nome, string? Cpf, string? Telefone, string? Email, Guid? PatientId);

/// <summary>Paciente em favor de quem se manifesta (pode ser o próprio manifestante).</summary>
public sealed record ReferidoDto(Guid? PatientId, string? Nome, string? Cpf, string? Cns);

/// <summary>
/// Retorno do registro. <see cref="CodigoAcesso"/> é entregue <b>uma única vez</b> — só o hash fica
/// no banco. Nulo em manifestação anônima.
/// </summary>
public sealed record ManifestacaoCriadaDto(Guid Id, string Protocolo, string? CodigoAcesso, DateOnly PrazoRespostaEm);

/// <summary>Linha da fila.</summary>
public sealed record ManifestacaoListaDto(
    Guid Id,
    string Protocolo,
    OuvidoriaTipo Tipo,
    OuvidoriaStatus Status,
    OuvidoriaPrioridade Prioridade,
    OuvidoriaIdentificacao Identificacao,
    OuvidoriaCanal Canal,
    string? Resumo,
    string? AssuntoNome,
    string? UnidadeNome,
    string? PontoRespostaNome,
    /// <summary>Nulo quando a identidade é restrita para quem consulta.</summary>
    string? ManifestanteNome,
    DateTime RegistradaEm,
    DateOnly PrazoRespostaEm,
    DateOnly? PrazoAreaEm,
    /// <summary>Dias além do prazo ao cidadão (0 = no prazo). Em aberto é calculado hoje; fechada, é o gravado na resposta.</summary>
    int DiasAtraso,
    bool Atrasada,
    /// <summary>Encaminhada e a área passou do prazo dela.</summary>
    bool AreaAtrasada,
    DateTime UltimaAtividadeEm,
    string? ResponsavelNome);

public sealed record AnexoDto(Guid Id, Guid MidiaId, string NomeArquivo, bool VisivelAoCidadao, DateTime CriadoEm);

public sealed record EventoDto(
    Guid Id,
    OuvidoriaTipoEvento Tipo,
    OuvidoriaStatus? StatusAnterior,
    OuvidoriaStatus? StatusNovo,
    string? AutorNome,
    string? PontoRespostaNome,
    string? Texto,
    bool VisivelAoCidadao,
    DateTime CriadoEm,
    IReadOnlyList<AnexoDto> Anexos);

/// <summary>
/// Detalhe da manifestação. Começa com todos os campos de <see cref="ManifestacaoListaDto"/>, na
/// mesma ordem, e segue com o restante. <see cref="AcoesPermitidas"/> traz os nomes das ações
/// válidas para o status atual <b>e</b> para o perfil de quem consulta.
/// </summary>
public sealed record ManifestacaoDetalheDto(
    Guid Id,
    string Protocolo,
    OuvidoriaTipo Tipo,
    OuvidoriaStatus Status,
    OuvidoriaPrioridade Prioridade,
    OuvidoriaIdentificacao Identificacao,
    OuvidoriaCanal Canal,
    string? Resumo,
    string? AssuntoNome,
    string? UnidadeNome,
    string? PontoRespostaNome,
    string? ManifestanteNome,
    DateTime RegistradaEm,
    DateOnly PrazoRespostaEm,
    DateOnly? PrazoAreaEm,
    int DiasAtraso,
    bool Atrasada,
    bool AreaAtrasada,
    DateTime UltimaAtividadeEm,
    string? ResponsavelNome,
    /// <summary>Para o ponto de resposta em denúncia é o teor pseudonimizado (o integral nunca sai).</summary>
    string Teor,
    string? TeorPseudonimizado,
    /// <summary>Nulo quando <see cref="IdentidadeRestrita"/> — a revelação é por endpoint próprio, com justificativa.</summary>
    ManifestanteDto? Manifestante,
    bool IdentidadeRestrita,
    ReferidoDto? Referido,
    Guid? EnvolvidoPractitionerId,
    string? EnvolvidoDescricao,
    Guid? AssuntoId,
    Guid? SubassuntoId,
    Guid? UnidadeId,
    Guid? PontoRespostaId,
    Guid? RegulacaoSolicitacaoId,
    string? ProtocoloExterno,
    string? SistemaExterno,
    DateOnly? DataFato,
    string? LocalFato,
    OuvidoriaOrigem Origem,
    DateTime? ProrrogadoEm,
    string? ProrrogacaoJustificativa,
    bool ComplementacaoUsada,
    DateTime? EncaminhadaEm,
    DateTime? RespondidaEm,
    DateTime? ConcluidaEm,
    OuvidoriaResolutividade? Resolutividade,
    OuvidoriaSituacaoFinal? SituacaoFinal,
    OuvidoriaMotivoNaoAtendimento? MotivoNaoAtendimento,
    OuvidoriaMotivoArquivamento? MotivoArquivamento,
    string? RespostaConclusiva,
    DateTime? HabilitadaEm,
    Guid? ResponsavelId,
    IReadOnlyList<Guid> MarcadorIds,
    /// <summary>Protocolos do mesmo CPF + assunto + unidade nos últimos 90 dias (a decisão de arquivar é humana).</summary>
    IReadOnlyList<string> PossiveisDuplicatas,
    IReadOnlyList<EventoDto> Eventos,
    IReadOnlyList<AnexoDto> Anexos,
    IReadOnlyList<string> AcoesPermitidas);

// ---- Requests ----

public sealed record AnexoRef(Guid MidiaId, string NomeArquivo);

public sealed record RegistrarManifestacaoRequest(
    OuvidoriaTipo Tipo,
    OuvidoriaIdentificacao Identificacao,
    OuvidoriaCanal Canal,
    OuvidoriaOrigem Origem,
    string Teor,
    string? Resumo,
    Guid? AssuntoId,
    Guid? SubassuntoId,
    Guid? UnidadeId,
    DateOnly? DataFato,
    string? LocalFato,
    ManifestanteDto? Manifestante,
    ReferidoDto? Referido,
    string? EnvolvidoDescricao,
    string? ProtocoloExterno,
    string? SistemaExterno,
    Guid? RegulacaoSolicitacaoId,
    IReadOnlyList<AnexoRef> Anexos);

/// <summary>Registro pelo site público: canal e origem são fixos (<c>SitePublico</c>/<c>Cidadao</c>).</summary>
public sealed record RegistrarManifestacaoPublicaRequest(
    OuvidoriaTipo Tipo,
    OuvidoriaIdentificacao Identificacao,
    string Teor,
    Guid? AssuntoId,
    Guid? UnidadeId,
    DateOnly? DataFato,
    string? LocalFato,
    ManifestanteDto? Manifestante,
    ReferidoDto? Referido,
    string? EnvolvidoDescricao);

/// <summary>Triagem: todos os campos opcionais; só o que vier preenchido é alterado (<see cref="MarcadorIds"/> substitui o conjunto).</summary>
public sealed record TriarRequest(
    OuvidoriaTipo? Tipo,
    Guid? AssuntoId,
    Guid? SubassuntoId,
    OuvidoriaPrioridade? Prioridade,
    Guid? UnidadeId,
    string? Resumo,
    Guid? ResponsavelId,
    Guid? RegulacaoSolicitacaoId,
    IReadOnlyList<Guid>? MarcadorIds);

/// <summary><see cref="PrazoDias"/> sobrepõe o prazo calculado; <see cref="TeorPseudonimizado"/> pode vir junto (denúncia).</summary>
public sealed record EncaminharRequest(Guid PontoRespostaId, int? PrazoDias, string? Texto, string? TeorPseudonimizado);

public sealed record TextoRequest(string Texto);

public sealed record TextoComAnexosRequest(string Texto, IReadOnlyList<AnexoRef> Anexos);

/// <summary>
/// Resposta ao cidadão. <see cref="Conclusiva"/> = false gera resposta intermediária (status não muda);
/// true exige <see cref="Resolutividade"/> e <see cref="SituacaoFinal"/> coerente com o tipo.
/// </summary>
public sealed record ResponderCidadaoRequest(
    string Texto,
    bool Conclusiva,
    OuvidoriaResolutividade? Resolutividade,
    OuvidoriaSituacaoFinal? SituacaoFinal,
    OuvidoriaMotivoNaoAtendimento? MotivoNaoAtendimento);

/// <summary><c>Duplicidade</c> exige <see cref="Texto"/> com o protocolo original.</summary>
public sealed record ArquivarRequest(OuvidoriaMotivoArquivamento Motivo, string? Texto);

public sealed record EncaminharExternoRequest(string SistemaExterno, string? ProtocoloExterno, string Texto);

// ---- Resumo / catálogo / configuração ----

/// <summary>Contadores da fila (badges das abas). <see cref="MeuPonto"/> = encaminhadas aos pontos de que o usuário é membro.</summary>
public sealed record OuvidoriaResumoDto(
    int Registradas,
    int EmTriagem,
    int Encaminhadas,
    int AguardandoComplementacao,
    int AguardandoValidacao,
    int Atrasadas,
    int AreaAtrasadas,
    int EmRecurso,
    int MeuPonto);

/// <summary><see cref="Pendentes"/> = manifestações encaminhadas ao ponto ainda sem resposta da área.</summary>
public sealed record PontoRespostaDto(
    Guid Id,
    string Nome,
    OuvidoriaTipoPontoResposta Tipo,
    Guid? UnidadeId,
    string? UnidadeNome,
    int? PrazoDias,
    bool Ativo,
    IReadOnlyList<PontoRespostaMembroDto> Membros,
    int Pendentes);

public sealed record PontoRespostaMembroDto(Guid UsuarioId, string Nome, bool Titular);

/// <summary>Os membros substituem o conjunto atual por inteiro.</summary>
public sealed record SalvarPontoRespostaRequest(
    string Nome,
    OuvidoriaTipoPontoResposta Tipo,
    Guid? UnidadeId,
    int? PrazoDias,
    bool Ativo,
    IReadOnlyList<SalvarMembroRequest> Membros);

public sealed record SalvarMembroRequest(Guid UsuarioId, bool Titular);

/// <summary>Árvore plana: subassunto aponta para o pai por <see cref="PaiId"/>.</summary>
public sealed record AssuntoDto(Guid Id, Guid? PaiId, string Nome, string? CodigoOuvidorSus, int Ordem, bool Ativo);

public sealed record SalvarAssuntoRequest(Guid? PaiId, string Nome, string? CodigoOuvidorSus, int Ordem, bool Ativo);

public sealed record MarcadorDto(Guid Id, string Nome, bool Ativo);

public sealed record SalvarMarcadorRequest(string Nome, bool Ativo);

/// <summary>Prazos e notificação da instância (D-4). <see cref="TextoRecibo"/> aceita <c>{protocolo}</c>, <c>{codigo}</c> e <c>{prazo}</c>.</summary>
public sealed record OuvidoriaConfiguracaoDto(
    int PrazoCidadaoDias,
    int ProrrogacaoDias,
    int PrazoAreaDias,
    int PrazoAreaAltaDias,
    int PrazoAreaUrgenteDiasUteis,
    int ComplementacaoDias,
    int ArquivamentoAutomaticoDias,
    bool NotificarPorWhatsApp,
    string? TextoRecibo);

// ---- Painel ----

/// <summary><see cref="Chave"/> é o valor técnico (nome do enum, id); <see cref="Rotulo"/> é o texto de exibição.</summary>
public sealed record ContagemDto(string Chave, string Rotulo, int Quantidade);

/// <summary>Indicadores do período (registradas em <c>[de, ate]</c>). <see cref="FaixasPrazo"/>: <c>ate30 | 31a60 | mais60</c>.</summary>
public sealed record OuvidoriaPainelDto(
    int Total,
    IReadOnlyList<ContagemDto> PorTipo,
    IReadOnlyList<ContagemDto> PorStatus,
    IReadOnlyList<ContagemDto> PorCanal,
    IReadOnlyList<ContagemDto> PorAssunto,
    IReadOnlyList<ContagemDto> PorUnidade,
    int Respondidas,
    int NoPrazo,
    int ForaPrazo,
    double? TempoMedioDias,
    double? TempoMedioAreaDias,
    /// <summary>Em aberto (nem respondida nem em status final).</summary>
    int Estoque,
    int Resolvidas,
    int NaoResolvidas,
    IReadOnlyList<ContagemDto> FaixasPrazo);

// ---- Público (protocolo + código) ----

public sealed record AssuntoPublicoDto(Guid Id, string Nome, Guid? PaiId);

public sealed record UnidadePublicaDto(Guid Id, string Nome);

/// <summary>Acompanhamento pelo cidadão: só eventos visíveis, sem nomes de pessoas.</summary>
public sealed record AcompanhamentoDto(
    string Protocolo,
    OuvidoriaTipo Tipo,
    OuvidoriaStatus Status,
    DateTime RegistradaEm,
    DateOnly PrazoRespostaEm,
    bool Prorrogada,
    string? RespostaConclusiva,
    OuvidoriaResolutividade? Resolutividade,
    bool PodeComplementar,
    bool PodeRecorrer,
    IReadOnlyList<EventoPublicoDto> Eventos);

public sealed record EventoPublicoDto(OuvidoriaTipoEvento Tipo, string? Texto, DateTime CriadoEm);
