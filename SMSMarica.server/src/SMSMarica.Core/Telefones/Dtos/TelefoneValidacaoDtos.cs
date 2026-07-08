namespace SMSMarica.Core.Telefones.Dtos;

/// <summary>Pedido para disparar o OTP do contato principal de uma pessoa (painel).</summary>
public sealed record EnviarTelefoneOtpRequest(string Cpf, string Numero);

/// <summary>Confirmação do código recebido pelo número, ancorada no CPF da pessoa.</summary>
public sealed record ConfirmarTelefoneOtpRequest(string Cpf, string Numero, string Codigo);

/// <summary>Resultado do disparo do OTP.</summary>
public sealed record TelefoneOtpEmitidoDto(string Canal, string? Mascara, int ExpiraEmSegundos);

/// <summary>Situação do contato validado de um (CPF, número).</summary>
public sealed record TelefoneValidadoDto(string Numero, bool Validado, DateTime? ValidadoEm);

/// <summary>Define o telefone principal do paciente (por CPF) sem exigir verificação.</summary>
public sealed record DefinirTelefonePrincipalRequest(string Cpf, string Numero);
