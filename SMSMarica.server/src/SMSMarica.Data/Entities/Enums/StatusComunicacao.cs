namespace SMSMarica.Data.Entities.Enums;

/// <summary>
/// Ciclo de vida do ENVIO de uma <see cref="Entities.ComunicacaoPaciente"/>.
/// (Renomeado de StatusNotificacaoAgendamento em 2026-07-05 — valores preservados.)
///
/// Transições:
///   Pendente → Enviada            (template aceito pela Meta)
///   Enviada  → Entregue → Lida    (recibos via webhook value.statuses)
///   Enviada/Entregue → Pendente   (falha de entrega retentável — reenvia com link novo)
///   Pendente → Falha              (esgotou tentativas ou erro Meta permanente)
///   Pendente → SemTelefoneValido  (nenhum celular BR válido — nem tenta)
///   Pendente → AguardandoTelefoneVerificado (dado clínico só vai para contato verificado)
/// </summary>
public enum StatusComunicacao
{
    Pendente = 1,
    Enviada = 2,
    Entregue = 3,
    Lida = 4,
    Falha = 5,
    SemTelefoneValido = 6,

    /// <summary>
    /// Retido: a comunicação leva DADO CLÍNICO (imagem do exame ou laudo) e o contato do
    /// paciente não está verificado. NÃO é terminal — assim que a recepção verificar o
    /// telefone, o worker envia sozinho. Confirmação de agendamento não passa por aqui (não
    /// expõe resultado; é justamente ela que provoca o contato).
    /// </summary>
    AguardandoTelefoneVerificado = 7,
}
