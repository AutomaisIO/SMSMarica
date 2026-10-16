namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Onde o item de worklist leva o CÓDIGO DO PROCEDIMENTO (o código interno do SISREG), por
/// equipamento (ADR-0074). Em todos os destinos o código vai em <b>(0008,0100) CodeValue</b>, com o
/// esquema em (0008,0102) e a descrição em (0008,0104) — muda só a sequência que carrega o item de
/// código.
///
/// <para><b>Desligado por padrão</b> (<see cref="Nenhum"/>): é o aparelho que precisa estar
/// cadastrado com esses códigos para escolher o protocolo por eles. Ligar num equipamento não muda o
/// que os outros recebem.</para>
///
/// <para>Combinável: cada aparelho lê o código numa sequência, e mandar nas duas é inofensivo
/// quando não se sabe qual delas ele lê. Destino novo entra aqui, com o seu valor de bit, sem mexer
/// nos já cadastrados.</para>
/// </summary>
[Flags]
public enum DestinoCodigoProcedimento
{
    Nenhum = 0,

    /// <summary>Requested Procedure Code Sequence (0032,1064), no nível do item.</summary>
    ProcedimentoSolicitado = 1,

    /// <summary>Scheduled Protocol Code Sequence (0040,0008), dentro do passo agendado
    /// (0040,0100).</summary>
    ProtocoloAgendado = 2,
}
