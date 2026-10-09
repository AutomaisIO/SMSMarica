namespace SMSMais.Core.Sandbox.Dtos;

/// <summary>
/// Situação do "Entrar como paciente" para o operador logado.
/// </summary>
/// <param name="Apta">O operador pode usar: tem CPF no usuário e esse CPF tem WhatsApp verificado.</param>
/// <param name="MotivoInapta">Quando não está apto, o que falta — escrito para o operador.</param>
/// <param name="CpfMascarado">CPF do próprio operador, o que ele digita no app.</param>
/// <param name="TelefoneMascarado">WhatsApp verificado do operador, para onde vai o código.</param>
/// <param name="Ativa">Paciente que o app abre agora; null quando não há nenhum.</param>
public sealed record PersonificacaoStatusDto(
    bool Apta,
    string? MotivoInapta,
    string? CpfMascarado,
    string? TelefoneMascarado,
    PersonificacaoAtivaDto? Ativa);

/// <param name="SessoesAbertas">Quantos aparelhos estão logados como o paciente por esta personificação.</param>
public sealed record PersonificacaoAtivaDto(
    Guid PacienteId,
    string PacienteNome,
    DateTime CriadaEm,
    DateTime ExpiraEm,
    int SessoesAbertas);

public sealed record AtivarPersonificacaoRequest(Guid PacienteId);
