namespace SMSMarica.Core.Integracoes.Proxy.Configuracao;

/// <summary>
/// Motor de proxy exposto na tela. Nunca devolve o token: apenas sinaliza se já está
/// definido (<see cref="TokenDefinido"/>). Inclui placeholders "não configurado" para os
/// motores suportados que ainda não têm linha no banco.
/// </summary>
public sealed record ProxyMotorDto(
    string Servico,
    string Motor,
    string Rotulo,
    bool Ativo,
    int Ordem,
    bool ExigeToken,
    bool TokenDefinido,
    int TimeoutSegundos,
    int Tentativas,
    string? ParametrosJson);

/// <summary>
/// Upsert de um motor. Token vazio = mantém o atual; preenchido = cifra e substitui
/// (padrão write-only).
/// </summary>
public sealed record AtualizarProxyMotorRequest(
    string? Token,
    bool Ativo,
    int Ordem,
    int TimeoutSegundos,
    int Tentativas,
    string? ParametrosJson);
