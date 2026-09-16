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
/// <see cref="Outro"/> e vira dado, não falha de importação.</para>
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

    /// <summary>Verbo que nenhuma regra reconheceu. O texto cru fica em <c>evento</c>.</summary>
    Outro = 99,
}
