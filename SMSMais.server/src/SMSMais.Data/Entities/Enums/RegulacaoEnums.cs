namespace SMSMais.Data.Entities.Enums;

/// <summary>Sistema de regulação em que uma solicitação termina (ADR-0052).</summary>
public enum SistemaRegulacao
{
    /// <summary>Regulação municipal de Maricá.</summary>
    Sisreg = 1,

    /// <summary>SER da SES-RJ (ADR-0042).</summary>
    Ser = 2,

    /// <summary>SER de Niterói.</summary>
    Sernit = 3,

    /// <summary>Reservado: ainda não há origem eSUS no catálogo.</summary>
    Esus = 4,
}

/// <summary>Natureza do procedimento canônico.</summary>
public enum TipoProcedimentoRegulacao
{
    Consulta = 1,
    Exame = 2,
    Cirurgia = 3,
    Outro = 9,
}

/// <summary>
/// Como uma origem chegou ao procedimento canônico a que está ligada.
///
/// <para>Mesmo idioma de <c>SisregProcedimentoSigtap.SugeridoSigtapId</c>/<c>ConfirmadoEm</c>:
/// o robô só <b>sugere</b>; quem confirma é a curadoria. Origem <see cref="Automatico"/> pode ser
/// movida por uma sugestão aceita; <see cref="Confirmado"/> nunca é mexida pelo sync.</para>
/// </summary>
public enum VinculoOrigemRegulacao
{
    Automatico = 1,
    Confirmado = 2,
}

/// <summary>
/// O que fazer quando o solicitante responde "não sei" a uma regra não dedutível.
///
/// <para>Existe porque "não sei" é resposta legítima e frequente: o critério é clínico e quem
/// abre a solicitação nem sempre é quem examinou. Tratar como "não atende" barraria paciente por
/// falta de informação; tratar como "atende" mentiria para a regulação.</para>
/// </summary>
public enum NaoSeiViraRegulacao
{
    /// <summary>Segue com ressalva — o agente regulador decide na triagem.</summary>
    Ressalva = 1,

    /// <summary>Vira pendência: não envia até alguém responder.</summary>
    Pendencia = 2,
}

/// <summary>Para onde a solicitação vai. Define formulário, regras e credencial de envio.</summary>
public enum FluxoRegulacao
{
    /// <summary>Termina no SISREG — regulação do próprio município.</summary>
    Interno = 1,

    /// <summary>Termina no SER (SES-RJ) ou no SERNIT (Niterói). Quem executa é outro ente.</summary>
    Externo = 2,

    /// <summary>
    /// Agendamento indireto: sempre SISREG, aberto <b>em nome de</b> outra unidade solicitante,
    /// e incluído pelo agente com a credencial daquela unidade (D-9).
    /// </summary>
    Nar = 3,
}

/// <summary>Estado da solicitação. A ordem numérica não é a do fluxo — ver a máquina no plano 04.</summary>
public enum StatusRegulacao
{
    Rascunho = 1,

    /// <summary>Na fila de pré-regulação, esperando um agente assumir.</summary>
    PendenteRegulacao = 2,

    EmAnalise = 3,

    /// <summary>Devolvida à ponta com pendência aberta.</summary>
    Devolvida = 4,

    /// <summary>Envio em curso. Estado curto que serve de trava anti-duplo-envio.</summary>
    EnviandoAoSistema = 5,

    EnviadaAoSistema = 6,

    /// <summary>A varredura casou o número externo com o espelho: agora quem manda é o sistema de lá.</summary>
    EmFilaExterna = 7,

    Agendada = 8,
    Concluida = 9,
    Cancelada = 10,
    Recusada = 11,
    FalhaEnvio = 12,
}

/// <summary>Estado de uma exigência documental — a "caixinha" de anexo.</summary>
public enum SituacaoExigenciaRegulacao
{
    Pendente = 1,
    Atendida = 2,

    /// <summary>Resolvida com exame que o próprio sistema já tinha, validado por um operador.</summary>
    AtendidaPorExameInterno = 3,

    Dispensada = 4,

    /// <summary>O sistema de regulação recusou o documento e pediu outro.</summary>
    Criticada = 5,
}

/// <summary>
/// Estado de um arquivo dentro da caixinha. A caixinha guarda versões: documento criticado não
/// é apagado, vira <see cref="Criticado"/> e o novo entra como versão seguinte.
/// </summary>
public enum SituacaoArquivoExigencia
{
    Atual = 1,
    Substituido = 2,
    Criticado = 3,
    Removido = 4,
}

public enum OrigemArquivoExigencia
{
    Upload = 1,

    /// <summary>Gerado a partir de exame/laudo que já existia no SMSMais.</summary>
    ExameInterno = 2,
}

/// <summary>
/// O que aconteceu com a solicitação. É a trilha que responde, meses depois, por que ela demorou —
/// e por isso o evento é <b>append-only</b>: nada aqui é editado nem apagado (ADR-0052).
/// </summary>
public enum TipoEventoRegulacao
{
    Criacao = 1,
    Edicao = 2,
    Anexo = 3,
    RespostaRegra = 4,

    /// <summary>A ponta mandou para a pré-regulação.</summary>
    EnvioFila = 5,

    /// <summary>Um agente assumiu o caso.</summary>
    Assumida = 6,

    /// <summary>Ajuste do agente durante a triagem, com o diff do que mudou.</summary>
    Ajuste = 7,

    Devolucao = 8,

    /// <summary>Envio ao sistema de regulação disparado (a credencial usada vai no detalhe).</summary>
    EnvioSistema = 9,

    FalhaEnvio = 10,

    /// <summary>Número do SISREG/SER/SERNIT capturado — automático ou digitado pelo agente.</summary>
    NumeroExterno = 11,

    /// <summary>Destino permitido só com ressalva (bloqueado em um sistema, livre em outro).</summary>
    RessalvaDestino = 12,

    PendenciaAberta = 13,
    PendenciaRespondida = 14,
    PendenciaSubmetida = 15,
    PendenciaBaixada = 16,

    /// <summary>A situação mudou no sistema de lá e a varredura trouxe.</summary>
    SituacaoExterna = 17,

    Cancelamento = 18,
    Recusa = 19,

    /// <summary>D-8: o agente conferiu a solicitação interna que o solicitante já incluiu no SISREG.</summary>
    OkInterno = 20,

    /// <summary>
    /// D-10: o agente trocou o procedimento canônico. Não é <see cref="Ajuste"/> porque muda o
    /// formulário e as regras — o diff registra o que sobreviveu e o que caiu.
    /// </summary>
    TrocaProcedimento = 21,
}

/// <summary>
/// De que lado veio a ação. Separa o que a unidade fez do que a regulação fez e do que o sistema
/// fez sozinho — sem isso, a linha do tempo não distingue "a unidade corrigiu" de "o agente
/// corrigiu por ela".
/// </summary>
public enum PapelEventoRegulacao
{
    Solicitante = 1,
    Agente = 2,

    /// <summary>Sem usuário: varredura, importação, job.</summary>
    Sistema = 3,
}

/// <summary>
/// Se a solicitação pode ir para aquele sistema. <see cref="ComRessalva"/> é o caso do procedimento
/// bloqueado num sistema e permitido em outro: a solicitação passa, marcada "só pode ir para X".
/// </summary>
public enum SituacaoDestinoRegulacao
{
    Elegivel = 1,
    Bloqueado = 2,
    ComRessalva = 3,
}

/// <summary>
/// Como a regra do manual se comporta na tela.
///
/// <para><b><see cref="Informativa"/> existe por medição, não por elegância</b> (spike e, 05/09/2026):
/// 83% das 1.169 regras extraídas dos manuais CRECE/REUNI são texto clínico corrido. Virando
/// pergunta, um recurso com 20 critérios pediria 20 respostas ao solicitante — e o questionário
/// morreria de inanição. Informativa é lida, não respondida.</para>
/// </summary>
public enum TipoRegraRegulacao
{
    /// <summary>O sistema decide sozinho pelo cadastro (idade, sexo, CID).</summary>
    Dedutivel = 1,

    /// <summary>Vira pergunta ao solicitante: sim / não / não sei.</summary>
    NaoDedutivel = 2,

    /// <summary>Exige documento — vira uma caixinha de anexo própria.</summary>
    Documental = 3,

    /// <summary>Texto do manual que a tela mostra e ninguém responde.</summary>
    Informativa = 4,
}

/// <summary>O que acontece quando a regra não é atendida.</summary>
public enum SeveridadeRegraRegulacao
{
    /// <summary>Aquele destino sai da lista.</summary>
    Bloqueia = 1,

    /// <summary>Passa marcada — o agente decide na triagem.</summary>
    Ressalva = 2,

    /// <summary>Só avisa.</summary>
    Aviso = 3,
}

public enum RespostaRegraRegulacao
{
    Sim = 1,
    Nao = 2,

    /// <summary>O solicitante não sabe. O destino disso é configurável (ressalva ou pendência).</summary>
    NaoSei = 3,

    /// <summary>Não foi perguntado: o sistema deduziu do cadastro.</summary>
    Deduzido = 4,
}

public enum ResultadoRegraRegulacao
{
    Atende = 1,
    Bloqueia = 2,
    Ressalva = 3,

    /// <summary>Falta dado para decidir (sem nascimento, sem CID, sem resposta).</summary>
    Indefinido = 4,
}
