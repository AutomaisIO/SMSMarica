namespace SMSMais.Data.Entities.Enums;

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

    /// <summary>
    /// Retido: confirmação de agendamento para número NÃO verificado. Em vez de enviar os dados
    /// do agendamento, mandou-se o desafio cadastral (template <c>validacao_cadastro</c>) pedindo
    /// os 4 primeiros dígitos do CPF. Quando o robô valida (comando <c>VerificarCadastro</c>),
    /// volta para <see cref="Pendente"/> e o worker envia a confirmação real. NÃO é terminal.
    /// </summary>
    AguardandoVerificacaoCadastral = 8,

    /// <summary>
    /// Retido: o número de destino tem pendência ABERTA de "número errado" — quem atende já
    /// disse que NÃO é o paciente. Enviar de novo é assediar a pessoa errada (foi o padrão de
    /// 01-02/09: templates em massa para números negados). NÃO é terminal: quando a recepção
    /// resolve (ou ignora) a pendência, a comunicação volta a Pendente e o envio re-resolve o
    /// telefone já corrigido do cadastro.
    /// </summary>
    AguardandoCorrecaoContato = 9,
}
