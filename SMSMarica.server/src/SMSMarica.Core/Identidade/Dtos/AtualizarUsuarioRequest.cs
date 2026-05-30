namespace SMSMarica.Core.Identidade.Dtos;

/// <summary>
/// Admin atualiza apenas o nome de exibição de um Usuario sem papel
/// (admin/operador). Para usuários com papel (Patient/Practitioner/Motorista),
/// os dados pessoais são editados pelo endpoint específico do papel — o nome
/// na tela cai por sincronização automática de <c>nome_exibicao</c>.
/// </summary>
public sealed record AtualizarUsuarioRequest(
    string NomeCompleto);

/// <summary>
/// O próprio usuário (sem papel) atualiza só o nome de exibição. Usuários com
/// papel atualizam pela tela do papel; este endpoint vira no-op pra eles.
/// </summary>
public sealed record AtualizarMinhaContaRequest(
    string NomeCompleto);
