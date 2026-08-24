using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame.Dtos;

/// <summary>
/// Corpo do envio manual do resultado ao paciente. <see cref="Finalidade"/> = ExameLiberado ou
/// LaudoPronto. <see cref="AssumirRisco"/> = enviar mesmo com telefone não verificado.
/// </summary>
public sealed record EnviarComunicacaoManualRequest(
    FinalidadeComunicacao Finalidade,
    bool AssumirRisco = false);
