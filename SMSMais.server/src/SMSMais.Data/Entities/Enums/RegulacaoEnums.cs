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
