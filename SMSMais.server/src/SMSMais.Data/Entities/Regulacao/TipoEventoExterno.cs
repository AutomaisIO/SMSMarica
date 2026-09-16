namespace SMSMais.Data.Entities.Regulacao;

/// <summary>
/// O verbo da trilha "Histórico da Solicitação" do SER e do SERNIT, tipado.
///
/// <para>Os dois sistemas entregam o verbo como TEXTO numa coluna de tabela HTML ("Solicitar",
/// "FollowUP", "Pendenciar", "Cancelar"); não há código nem id de tipo na origem. A classificação
/// é textual por natureza — o que este enum faz é executá-la <b>uma vez, na captura</b>, e gravar
/// o resultado ao lado do verbo cru, em vez de repetir <c>ILIKE</c> em toda consulta.</para>
///
/// <para>O verbo cru continua guardado em <c>evento</c>: um verbo novo do sistema externo cai em
/// <see cref="Outro"/> e vira dado, não falha de importação. Os valores 6 a 13 vieram de olhar
/// o que tinha caído em "Outro" em produção (16/09/2026): "Chegada no Destino" sozinho eram
/// 12.985 eventos.</para>
/// </summary>
public enum TipoEventoExterno
{
    Solicitar = 1,

    /// <summary>Tentativa de contato, cobrança da fila, orientação — o evento que carrega
    /// informação de contato com o paciente. Só este ganha categoria pelo classificador.</summary>
    FollowUp = 2,

    Pendenciar = 3,
    Cancelar = 4,
    Agendar = 5,

    /// <summary>"Chegada no Destino": o paciente chegou à unidade executora — é o fecho do ciclo
    /// para a regulação, e o verbo mais frequente da trilha depois de Solicitar.</summary>
    ChegadaNoDestino = 6,

    /// <summary>"Transferir": a solicitação mudou de central/unidade executora.</summary>
    Transferir = 7,

    /// <summary>"Devolvido para a regulação": a unidade executora recusou/devolveu.</summary>
    DevolvidoParaRegulacao = 8,

    /// <summary>"WhatsApp": mensagem enviada pelo próprio SER ao paciente.</summary>
    WhatsApp = 9,

    /// <summary>"Reagendar" — distinto de <see cref="Agendar"/>: é remarcação.</summary>
    Reagendar = 10,

    /// <summary>"Retornar para Fila" (inclui a variante ", não apto").</summary>
    RetornarParaFila = 11,

    /// <summary>"Alta" / "Dar Alta".</summary>
    Alta = 12,

    /// <summary>"Corrigir dados da solicitação".</summary>
    CorrigirDados = 13,

    /// <summary>Verbo que nenhuma regra reconheceu. O texto cru fica em <c>evento</c>.</summary>
    Outro = 99,
}
