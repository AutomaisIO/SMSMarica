namespace SMSMarica.Data.Entities;

/// <summary>
/// Pesquisa de satisfação de UM atendimento. O <see cref="Id"/> é o token que vai na URL
/// (<c>app.smsmarica.online/pesquisa/{id}</c>) — token escopado, que abre só a pesquisa e nunca
/// uma sessão: mandar magic-link para dezenas de milhares de pessoas por mês, só para colher
/// nota, entregaria o prontuário inteiro a quem recebesse a mensagem por engano.
///
/// <para><b>Não é de uso único.</b> Diferente de <see cref="CidadaoLoginLink"/> e
/// <see cref="DownloadToken"/>, aqui o link vale até <see cref="ExpiraEm"/> mesmo depois de
/// aberto — quem toca no link e se distrai precisa poder voltar. Quem encerra é
/// <see cref="RespondidaEm"/>: respondida uma vez, não se responde de novo.</para>
///
/// <para>Uma pesquisa por atendimento (índice único em <see cref="EncounterId"/>), para reenvio
/// não gerar dois convites da mesma passagem.</para>

/// <para><b>Aqui não mora resposta.</b> O questionário é da AvanteSocial, num link por unidade;
/// nós provocamos, contamos o clique e encaminhamos. Esta linha guarda QUEM foi convidado e SE
/// clicou — nunca O QUE respondeu. É essa separação que sustenta o anonimato da pesquisa: as
/// duas metades existem em sistemas diferentes e não se juntam.</para>
/// </summary>
public class PesquisaSatisfacao
{
    /// <summary>Token da URL pública.</summary>
    public Guid Id { get; set; }

    /// <summary>Paciente (fhir.patient) que viveu o atendimento.</summary>
    public Guid PatientId { get; set; }

    /// <summary>Atendimento avaliado (fhir.encounter). É o "prontuário" a que a resposta se liga.</summary>
    public Guid EncounterId { get; set; }

    /// <summary>
    /// Nome da unidade no momento do envio. Denormalizado de propósito: é o que a tela mostra no
    /// cabeçalho, e uma unidade renomeada não pode reescrever o passado de quem já respondeu.
    /// </summary>
    public string? UnidadeNome { get; set; }

    /// <summary>CNES da unidade — é por ele que o redirect acha o link da AvanteSocial.</summary>
    public string? UnidadeCnes { get; set; }

    /// <summary>Quando o atendimento terminou — a origem da contagem do prazo.</summary>
    public DateTime AtendimentoEm { get; set; }

    /// <summary>
    /// Limite para responder (<c>AtendimentoEm</c> + janela). A tela some antes disso, mas quem
    /// guarda o link ainda alcançaria: a regra vale aqui, no servidor.
    /// </summary>
    public DateTime ExpiraEm { get; set; }

    /// <summary>
    /// Primeiro clique no link. <b>É o mais longe que a nossa medição vai</b>: a resposta é
    /// tratada pela AvanteSocial e nunca passa por aqui. Clicar não é responder — e chamar isso
    /// de "respondida" num painel seria inventar uma taxa que não temos.
    /// </summary>
    public DateTime? ClicadaEm { get; set; }

    /// <summary>Cliques no total — a mesma pessoa pode voltar ao link.</summary>
    public int Cliques { get; set; }

    /// <summary>
    /// Id da mensagem no WhatsApp (<c>wamid</c>). É a ponte para <c>whatsapp_mensagem</c>, onde o
    /// webhook já grava entregue/lida — as métricas de "vistas" saem de lá, sem duplicar estado.
    /// </summary>
    public string? WaMessageId { get; set; }

    /// <summary>Quando o convite saiu por WhatsApp. Null = criada e ainda não enviada.</summary>
    public DateTime? EnviadaEm { get; set; }

    /// <summary>Operador que disparou o envio manual. Null quando o disparo for automático.</summary>
    public Guid? EnviadaPor { get; set; }

    /// <summary>
    /// Sexo e nascimento do paciente no momento do convite. Desnormalizados porque o hub FHIR é
    /// serviço autônomo (ADR-0010) e o painel não pode fazer uma chamada por convidado.
    ///
    /// <para>Tem virtude de privacidade também: o perfil de quem clicou fica ao lado do convite,
    /// que não guarda resposta nenhuma. Não há consulta que junte demografia e opinião — nem por
    /// engano, nem de propósito.</para>
    /// </summary>
    public Enums.Sexo? PacienteSexo { get; set; }
    public DateOnly? PacienteNascimento { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}
