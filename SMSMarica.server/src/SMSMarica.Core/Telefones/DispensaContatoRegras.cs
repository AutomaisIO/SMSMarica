using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Telefones;

/// <summary>
/// Régua da dispensa de verificação de contato: rótulo de cada motivo e, principalmente, se o
/// motivo deixa ou não o dado clínico sair por WhatsApp.
///
/// Toda dispensa libera a AUTORIZAÇÃO PRESENCIAL (o paciente está no balcão, a recepção o vê).
/// O que varia é o ENVIO de resultado e laudo: só sai quando o motivo diz que existe um número
/// utilizável e consentido. "Outro" é conservador de propósito — motivo que o sistema não
/// conhece não autoriza mandar exame de ninguém.
/// </summary>
public static class DispensaContatoRegras
{
    /// <summary>
    /// O motivo permite enviar dado clínico (exame/laudo) por WhatsApp, assumindo o risco de o
    /// número não ter passado por OTP? false = entrega presencial.
    /// </summary>
    public static bool PermiteEnvio(MotivoDispensaContato motivo) => motivo switch
    {
        MotivoDispensaContato.NumeroDeTerceiro => true,
        MotivoDispensaContato.NaoConsegueConfirmar => true,
        MotivoDispensaContato.SemSinalNoMomento => true,
        _ => false,
    };

    /// <summary>Rótulo exibido no painel (fonte única — o front lê de /telefones/dispensa/motivos).</summary>
    public static string Rotulo(MotivoDispensaContato motivo) => motivo switch
    {
        MotivoDispensaContato.SemCelular => "Não possui celular",
        MotivoDispensaContato.SemWhatsApp => "Possui celular, mas sem WhatsApp",
        MotivoDispensaContato.NumeroDeTerceiro => "Número é de terceiro (familiar / cuidador / responsável)",
        MotivoDispensaContato.NaoConsegueConfirmar =>
            "Não consegue ler ou informar o código (idoso, deficiência visual, baixa alfabetização)",
        MotivoDispensaContato.SemSinalNoMomento => "Sem sinal / sem internet no momento",
        MotivoDispensaContato.RecusaValidar => "Paciente recusa informar ou validar o número",
        MotivoDispensaContato.Outro => "Outro (descrever)",
        _ => motivo.ToString(),
    };

    /// <summary>
    /// O que o operador precisa entender ANTES de escolher: o que este motivo faz com os avisos
    /// do paciente. Vai no modal, ao lado da opção selecionada.
    /// </summary>
    public static string Consequencia(MotivoDispensaContato motivo) => PermiteEnvio(motivo)
        ? "O exame será autorizado e os avisos (resultado e laudo) continuarão indo para o número do cadastro."
        : "O exame será autorizado, mas resultado e laudo NÃO serão enviados por WhatsApp — a entrega é presencial.";

    /// <summary>Ordem de exibição: os casos de balcão mais comuns primeiro, "Outro" por último.</summary>
    public static IReadOnlyList<MotivoDispensaContato> Todos { get; } =
    [
        MotivoDispensaContato.SemCelular,
        MotivoDispensaContato.SemWhatsApp,
        MotivoDispensaContato.NumeroDeTerceiro,
        MotivoDispensaContato.NaoConsegueConfirmar,
        MotivoDispensaContato.SemSinalNoMomento,
        MotivoDispensaContato.RecusaValidar,
        MotivoDispensaContato.Outro,
    ];
}
