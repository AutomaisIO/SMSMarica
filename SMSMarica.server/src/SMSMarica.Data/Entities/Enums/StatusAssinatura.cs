namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Estado de uma tentativa de assinatura digital (PAdES) de um laudo.
/// Fluxo diferido (two-step): <see cref="Iniciada"/> guarda o estado de
/// transferência (hash + PDF preparado) enquanto o médico assina no cliente
/// (Web PKI / VIDaaS Connect); <see cref="Concluida"/> grava o PDF assinado
/// byte-estável. Uma única assinatura <see cref="Concluida"/> por laudo.
/// </summary>
public enum StatusAssinatura
{
    /// <summary>Job criado, aguardando o agente do médico reivindicar e enviar o certificado.</summary>
    Iniciada = 1,
    Concluida = 2,
    Falhou = 3,
    Cancelada = 4,

    /// <summary>Hash preparado (agente já enviou o certificado); aguardando a assinatura crua.</summary>
    AguardandoAssinatura = 5,
}
