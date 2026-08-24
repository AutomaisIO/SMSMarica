namespace SMSMais.Data.Entities;

/// <summary>
/// Configuração da pesquisa de satisfação de uma unidade.
///
/// <para><b>A pesquisa não é nossa.</b> Quem recebe e trata a resposta é a AvanteSocial, num link
/// por unidade (<see cref="LinkResponder"/>). Nós só provocamos: mandamos o WhatsApp, contamos o
/// clique e encaminhamos. Isso é o que sustenta o anonimato — a resposta nunca passa por aqui, e
/// o link de saída não leva identificador nenhum.</para>
///
/// <para>Uma linha por unidade. Só três unidades têm atendimento no hub hoje (Conde, UPA Inoã e
/// Santa Rita), então só nelas o disparo tem efeito; as demais existem para o cartaz e o QR.</para>
/// </summary>
public class UnidadePesquisaConfig
{
    /// <summary>Unidade configurada — também é a chave.</summary>
    public Guid UnidadeId { get; set; }

    /// <summary>Liga o disparo por WhatsApp. Desligado, a unidade segue só com cartaz/QR.</summary>
    public bool EnvioWhatsAppAtivo { get; set; }

    /// <summary>Link da pesquisa na AvanteSocial — o destino do redirect.</summary>
    public string? LinkResponder { get; set; }

    /// <summary>Painel de resultados da unidade, para a gestão abrir a partir da nossa tela.</summary>
    public string? LinkPainel { get; set; }

    /// <summary>
    /// Horas após o fim do atendimento para disparar. Cedo demais o paciente ainda está
    /// sintomático e responde sobre o mal-estar; tarde demais já esqueceu o detalhe.
    /// </summary>
    public int HorasAposAtendimento { get; set; } = 24;

    /// <summary>
    /// Até onde a varredura já processou (fim de atendimento). É a marca d'água: a janela do
    /// próximo ciclo começa aqui, então nem pula quem terminou durante uma parada, nem convida
    /// duas vezes quem cai na borda.
    /// </summary>
    public DateTime? UltimoFimProcessadoEm { get; set; }

    public DateTime AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
