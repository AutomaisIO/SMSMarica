namespace SMSMais.Data.Entities.Enums;

/// <summary>Como a comunicação ao paciente foi disparada.</summary>
public enum OrigemComunicacao
{
    /// <summary>Enfileirada pelo gatilho do sistema (import, exame Realizado, laudo assinado).</summary>
    Automatico = 0,

    /// <summary>Disparada manualmente por um operador (botão Enviar/Reenviar no detalhe).</summary>
    Manual = 1,
}
