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
/// não gerar duas notas da mesma passagem.</para>
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

    /// <summary>Quando o atendimento terminou — a origem da contagem do prazo.</summary>
    public DateTime AtendimentoEm { get; set; }

    /// <summary>
    /// Limite para responder (<c>AtendimentoEm</c> + janela). A tela some antes disso, mas quem
    /// guarda o link ainda alcançaria: a regra vale aqui, no servidor.
    /// </summary>
    public DateTime ExpiraEm { get; set; }

    /// <summary>
    /// Versão do instrumento respondido (ex.: <c>pnass-2015-emergencia-v1</c>). Sem isto, mudar
    /// a redação de uma pergunta mistura em série histórica duas coisas diferentes.
    /// </summary>
    public string InstrumentoVersao { get; set; } = string.Empty;

    /// <summary>Respostas como JSON (<c>{"geral":"Bom","espera":"Regular",...}</c>).</summary>
    public string? RespostasJson { get; set; }

    public DateTime? RespondidaEm { get; set; }
    public string? RespondidaIp { get; set; }

    /// <summary>Quando o convite saiu por WhatsApp. Null = criada e ainda não enviada.</summary>
    public DateTime? EnviadaEm { get; set; }

    /// <summary>Operador que disparou o envio manual. Null quando o disparo for automático.</summary>
    public Guid? EnviadaPor { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
}
