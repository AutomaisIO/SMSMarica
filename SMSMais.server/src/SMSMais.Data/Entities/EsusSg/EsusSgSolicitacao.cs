namespace SMSMais.Data.Entities.EsusSg;

/// <summary>
/// Situação de um pedido de Maricá no ESUS de São Gonçalo, <b>derivada</b> das duas telas que a
/// conta de Maricá enxerga (medido em 30/09/2026, ADR-0063):
///
/// <list type="bullet">
/// <item>"Fila de Regulação" — o pedido ainda espera (com ou sem pendência);</item>
/// <item>"Pacientes Agendados pela Fila" — o pedido saiu da fila com data marcada.</item>
/// </list>
///
/// <para>As duas listas são <b>disjuntas</b> e compartilham a chave <c>fil_id</c>: ao ser agendado,
/// o pedido some da fila e aparece nos agendados. A tela de excluídos de exame é negada à conta
/// ("Usuário não possui permissão"), então quem some da fila sem aparecer nos agendados vira
/// <see cref="SaiuDaFila"/> — o motivo (exclusão, cancelamento, transferência) não é visível.</para>
/// </summary>
public enum SituacaoEsusSg
{
    EmFila = 1,

    /// <summary>Na fila com pendência ativa (o ESUS responde algo diferente de "NAO" e de
    /// "TODAS RESOLVIDAS" no campo <c>pendencia</c>).</summary>
    Pendente = 2,

    Agendada = 3,

    /// <summary>Não está mais na fila e não consta nos agendados. Declarado, não adivinhado.</summary>
    SaiuDaFila = 4,
}

/// <summary>
/// O que a unidade executante apontou do agendamento no ESUS (<c>efl_id_exames_efetivacao</c> do
/// detalhe do exame no histórico do paciente). Mesmos três estados do SISREG. Números = os do ESUS.
/// </summary>
public enum EfetivacaoEsusSg
{
    /// <summary>A unidade marcou explicitamente como em aberto ("MODIFICADO PARA EM ABERTO").</summary>
    EmAberto = 1,

    /// <summary>Exame efetivado — o paciente compareceu.</summary>
    Efetivado = 2,

    /// <summary>Não efetivado; o motivo vem em <see cref="EsusSgSolicitacao.MotivoNaoEfetivacao"/>
    /// ("Não Compareceu").</summary>
    NaoEfetivado = 3,
}

/// <summary>Módulo do ESUS em que o pedido vive. Maricá usa, na prática, só exame (PPI).</summary>
public enum TipoRecursoEsusSg
{
    Consulta = 1,
    Exame = 2,
}

/// <summary>
/// Espelho fiel de um pedido de Maricá no <b>ESUS de São Gonçalo</b> — o produto ESUS
/// (esusmais.com.br), <b>não</b> o e-SUS do governo. Terceiro irmão do SER-RJ e do SERNIT
/// (ADR-0042/ADR-0063), em tabelas próprias <c>esussg_*</c>.
///
/// <para><b>Uma linha por pedido, para todas as situações.</b> Chave natural
/// (<see cref="Tipo"/>, <see cref="IdEsusSg"/>) — o <c>fil_id</c> do ESUS, que é o mesmo na fila e
/// nos agendados.</para>
///
/// <para>Diferente do SER/SERNIT, o ESUS entrega tudo em JSON (duas APIs: Node em :8001 e PHP
/// legado em :9001) — não há histórico por pedido visível à conta. A trilha
/// (<see cref="EsusSgEvento"/>) é <b>montada</b> dos marcos que as listas trazem (inclusão na fila,
/// agendamento) e das diferenças entre varreduras (mudança de prioridade, saída da fila).</para>
/// </summary>
public class EsusSgSolicitacao
{
    public Guid Id { get; set; }

    /// <summary><b>Chave natural: o <c>fil_id</c> do ESUS.</b> Texto por ser identificador externo.</summary>
    public string IdEsusSg { get; set; } = string.Empty;

    public TipoRecursoEsusSg Tipo { get; set; } = TipoRecursoEsusSg.Exame;

    // ---- Pedido ----

    /// <summary>Procedimento como o ESUS escreve ("TRATAMENTO DE RETINA (PPI)"). É o nome que casa
    /// com o catálogo (<see cref="EsusSgCatalogoRecurso"/>).</summary>
    public string Recurso { get; set; } = string.Empty;

    /// <summary><c>codigo_procedimento</c> do ESUS. <b>Não é SIGTAP</b>: é código interno (o mesmo
    /// valor aparece em procedimentos diferentes). Guardado só para rastreabilidade.</summary>
    public string? CodigoInterno { get; set; }

    /// <summary>Subprocedimentos que o ESUS pendura no pedido (consulta, mapeamento de retina…).</summary>
    public string? Subprocedimentos { get; set; }

    /// <summary>Data do pedido médico (<c>fil_data_pedido</c>). Mesmo nome de coluna do SER/SERNIT —
    /// as estatísticas de operadores leem <c>data_solicitacao</c> por SQL com prefixo.</summary>
    public DateOnly? DataSolicitacao { get; set; }

    /// <summary>Entrada na fila do ESUS (<c>fil_data</c>) — a régua da espera.</summary>
    public DateOnly? DataEntradaFila { get; set; }

    /// <summary>Prioridade ("A REGULAR", "URGENTE", "MAIS DE 60 ANOS", "MANDADO JUDICIAL"…).</summary>
    public string? Prioridade { get; set; }

    public string? PrioridadeCor { get; set; }

    /// <summary>Texto de pendência do ESUS ("NAO", "TODAS RESOLVIDAS" ou a pendência ativa).</summary>
    public string? Pendencia { get; set; }

    /// <summary>Posição regulada na fila do procedimento (<c>fil_ordem_regulada</c>) — responde
    /// "em que lugar da fila estou" para o robô e a ficha. Só enquanto na fila.</summary>
    public int? PosicaoFila { get; set; }

    /// <summary>Ordem de entrada (<c>fil_ordem_entrada</c>).</summary>
    public int? OrdemEntrada { get; set; }

    public string? ProfissionalSolicitante { get; set; }

    /// <summary>Unidade solicitante como o ESUS mostra ("MUNICÍPIO DE MARICÁ - 0000001" —
    /// cadastro-placeholder, não é CNES real).</summary>
    public string? UnidadeSolicitante { get; set; }

    /// <summary>Operador que incluiu o pedido na fila do ESUS (servidor de Maricá).</summary>
    public string? UsuarioInclusao { get; set; }

    public string? Regulador { get; set; }

    // ---- Paciente (como o ESUS tem) ----

    /// <summary>Id da pessoa no ESUS (<c>pes_id</c>). Identificador do sistema externo, não do hub.</summary>
    public string? PessoaIdEsus { get; set; }

    public string PacienteNome { get; set; } = string.Empty;
    public string? Cpf { get; set; }
    public string? Cns { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public string? Sexo { get; set; }
    public string? NomeMae { get; set; }
    public string? Telefone { get; set; }
    public string? Celular { get; set; }
    public string? MunicipioPaciente { get; set; }
    public string? Bairro { get; set; }

    // ---- Agendamento (só quando saiu da fila com data) ----

    /// <summary>Unidade de destino como o ESUS escreve ("ABRAE  2297523") — o CNES vem no texto.</summary>
    public string? UnidadeExecutora { get; set; }

    /// <summary>CNES extraído do nome da unidade de destino, quando reconhecível (7 dígitos).</summary>
    public string? CnesExecutora { get; set; }

    public string? Setor { get; set; }
    public string? Local { get; set; }
    public DateOnly? DataAgendada { get; set; }

    /// <summary>Data e hora do atendimento como o ESUS formata ("06/10/2026 13:15:00").</summary>
    public string? DataHoraAgendadaTexto { get; set; }

    /// <summary>Operador de São Gonçalo que marcou.</summary>
    public string? UsuarioAgendamento { get; set; }

    public DateOnly? AgendamentoCadastradoEm { get; set; }
    public DateOnly? DataSaidaFila { get; set; }
    public bool? ComprovanteImpresso { get; set; }
    public bool? AgendadoTfd { get; set; }

    /// <summary>Canal da notificação que o ESUS enviou ao paciente (WHATSAPP, SMS) — vazio na
    /// maioria (medido: 4 de 64).</summary>
    public string? NotificacaoTipo { get; set; }
    public string? NotificacaoEntrega { get; set; }

    /// <summary>Resposta do paciente à notificação do ESUS (CONFIRMADO, NÃO RESPONDIDO, AGUARDANDO).</summary>
    public string? NotificacaoResposta { get; set; }

    // ---- Comparecimento (lido do histórico do paciente no ESUS, não das listas) ----

    /// <summary>
    /// O que a unidade executante apontou do agendamento. <c>null</c> = nada apontado (ou ainda não
    /// lido — ver <see cref="EfetivacaoLidaEm"/>).
    ///
    /// <para>Não vem nas listas de fila e agendados: só no detalhe do exame do "Histórico de
    /// Atendimentos do Paciente", uma requisição por paciente e uma por exame. A varredura relê os
    /// agendamentos dos últimos dias (a unidade aponta com atraso).</para>
    /// </summary>
    public EfetivacaoEsusSg? Efetivacao { get; set; }

    /// <summary>Quando a unidade efetivou (UTC; o ESUS dá hora de Brasília).</summary>
    public DateTime? EfetivadoEm { get; set; }

    /// <summary>Motivo da não efetivação, como o ESUS escreve ("Não Compareceu").</summary>
    public string? MotivoNaoEfetivacao { get; set; }

    /// <summary>Última leitura do comparecimento (UTC). Só diz algo do atendimento se for depois do dia
    /// agendado: lido depois e ainda sem apontamento = a unidade deixou em aberto.</summary>
    public DateTime? EfetivacaoLidaEm { get; set; }

    public SituacaoEsusSg Situacao { get; set; }

    // ---- Controle da sincronização ----

    /// <summary>Id do paciente no nosso hub FHIR, quando a conciliação resolveu. Sem FK (ADR-0010).</summary>
    public Guid? PacienteId { get; set; }

    /// <summary>Dado de paciente mudou e ainda não foi levado ao hub (o runner de conciliação drena).</summary>
    public DateTime? PacienteConciliarEm { get; set; }

    public DateTime SincronizadoEm { get; set; }

    /// <summary>Última varredura em que o pedido estava na lista da fila.</summary>
    public DateTime? VistoNaFilaEm { get; set; }

    /// <summary>Última varredura em que o pedido estava na lista de agendados.</summary>
    public DateTime? VistoNosAgendadosEm { get; set; }

    public int EventosCount { get; set; }
    public DateTime? UltimoEventoEm { get; set; }

    public DateTime? SituacaoMudouEm { get; set; }
    public SituacaoEsusSg? SituacaoAnterior { get; set; }

    // ---- Auditoria (ADR-0006) ----

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
    public DateTime? ExcluidoEm { get; set; }
    public Guid? ExcluidoPor { get; set; }

    public ICollection<EsusSgEvento> Eventos { get; set; } = [];
}
