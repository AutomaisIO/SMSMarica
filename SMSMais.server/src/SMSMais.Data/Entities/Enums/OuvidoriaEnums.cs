namespace SMSMais.Data.Entities.Enums;

// Enums do módulo Ouvidoria (ADR-0060). Valores explícitos a partir de 1: no banco vão como int,
// no JSON como string (JsonStringEnumConverter global). Nunca renumerar valor já persistido.

/// <summary>
/// Tipo da manifestação (Lei 13.460/2017 + Manual de Ouvidoria do SUS, MS 2014).
/// Define as regras de identificação (§2.1 do plano): solicitação e informação nunca são
/// sigilosas nem anônimas; anonimato só em denúncia.
/// </summary>
public enum OuvidoriaTipo
{
    Solicitacao = 1,
    Reclamacao = 2,
    Denuncia = 3,
    Sugestao = 4,
    Elogio = 5,
    Informacao = 6,
}

/// <summary>
/// Como o manifestante se identificou. <c>Sigilosa</c> = a ouvidoria conhece a identidade,
/// mas ela fica restrita (Decreto 10.153/2019); <c>Anonima</c> = ninguém conhece (sem código
/// de acesso, sem resposta ao cidadão).
/// </summary>
public enum OuvidoriaIdentificacao
{
    Identificada = 1,
    Sigilosa = 2,
    Anonima = 3,
}

/// <summary>Canal por onde a manifestação chegou. <c>Outro = 99</c> é a válvula de escape.</summary>
public enum OuvidoriaCanal
{
    Painel = 1,
    SitePublico = 2,
    AppCidadao = 3,
    WhatsApp = 4,
    Presencial = 5,
    Telefone = 6,
    Email = 7,
    Carta = 8,
    Urna = 9,
    BuscaAtiva = 10,
    Disque136 = 11,
    FalaBr = 12,
    OuvidoriaGeral = 13,
    Outro = 99,
}

/// <summary>Quem deu origem: o cidadão, a ouvidoria ativa (busca), de ofício ou coletiva.</summary>
public enum OuvidoriaOrigem
{
    Cidadao = 1,
    OuvidoriaAtiva = 2,
    DeOficio = 3,
    Coletiva = 4,
}

/// <summary>Prioridade que define o prazo da área (D-4: Normal 20 dias, Alta 10, Urgente 2 dias úteis).</summary>
public enum OuvidoriaPrioridade
{
    Normal = 1,
    Alta = 2,
    Urgente = 3,
}

/// <summary>
/// Máquina de estados da manifestação. Finais: <c>Concluida</c>, <c>Arquivada</c> e
/// <c>EncaminhadaOutroOrgao</c>. Cada transição gera um <see cref="OuvidoriaTipoEvento"/>.
/// </summary>
public enum OuvidoriaStatus
{
    Registrada = 1,
    EmTriagem = 2,
    Encaminhada = 3,
    AguardandoComplementacao = 4,
    RespondidaPelaArea = 5,
    EmValidacao = 6,
    Respondida = 7,
    EmRecurso = 8,
    Concluida = 9,
    Arquivada = 10,
    EncaminhadaOutroOrgao = 11,
}

/// <summary>Resolutividade declarada na resposta conclusiva (indicador do OuvidorSUS).</summary>
public enum OuvidoriaResolutividade
{
    Resolvida = 1,
    NaoResolvida = 2,
}

/// <summary>
/// Situação final, coerente com o tipo: solicitação → Atendida/NaoAtendida/NaoLocalizado/Faleceu;
/// reclamação e denúncia → Procede/NaoProcede/Inconclusiva; demais → Atendida.
/// </summary>
public enum OuvidoriaSituacaoFinal
{
    Atendida = 1,
    NaoAtendida = 2,
    NaoLocalizado = 3,
    Faleceu = 4,
    Procede = 5,
    NaoProcede = 6,
    Inconclusiva = 7,
}

/// <summary>Por que a solicitação não foi atendida (obrigatório quando <c>SituacaoFinal = NaoAtendida</c>).</summary>
public enum OuvidoriaMotivoNaoAtendimento
{
    FaltaRecursos = 1,
    NaoCobertoSus = 2,
    FezParticular = 3,
    VagasInsuficientes = 4,
    NaoCompareceu = 5,
    Outro = 99,
}

/// <summary>Motivo do arquivamento (PN CGU 116, arts. 28–30). <c>Duplicidade</c> exige o protocolo original no texto.</summary>
public enum OuvidoriaMotivoArquivamento
{
    Duplicidade = 1,
    TextoIncompreensivel = 2,
    FaltaUrbanidade = 3,
    Impropria = 4,
    CopiaConhecimento = 5,
    PerdaObjeto = 6,
    SemComplementacao = 7,
    SemElementosMinimos = 8,
    Outro = 99,
}

/// <summary>
/// Tipo de cada linha da trilha append-only (<c>ouvidoria_evento</c>). O que é visível ao cidadão
/// é decidido por evento (<c>VisivelAoCidadao</c>), não pelo tipo.
/// </summary>
public enum OuvidoriaTipoEvento
{
    Registro = 1,
    Triagem = 2,
    Reclassificacao = 3,
    Encaminhamento = 4,
    PedidoComplementacao = 5,
    Complementacao = 6,
    RespostaArea = 7,
    DevolucaoParaReanalise = 8,
    RespostaIntermediaria = 9,
    RespostaConclusiva = 10,
    Prorrogacao = 11,
    Cobranca = 12,
    Escalonamento = 13,
    Recurso = 14,
    Conclusao = 15,
    Arquivamento = 16,
    EncaminhamentoExterno = 17,
    Anotacao = 18,
    Habilitacao = 19,
    Reabertura = 20,
    AcessoIdentidade = 21,
}

/// <summary>
/// Natureza do ponto de resposta: uma unidade de saúde, uma área central da secretaria
/// (Regulação, Farmácia…) ou uma unidade apuratória (corregedoria, comissão de ética — D-6),
/// única que recebe denúncia habilitada.
/// </summary>
public enum OuvidoriaTipoPontoResposta
{
    Unidade = 1,
    AreaCentral = 2,
    Apuracao = 3,
}
