namespace SMSMarica.Core.Telefones.Dtos;

/// <summary>Pedido para disparar o OTP de validação de um número (painel).</summary>
public sealed record EnviarTelefoneOtpRequest(string Numero);

/// <summary>Confirmação do código recebido pelo número.</summary>
public sealed record ConfirmarTelefoneOtpRequest(string Numero, string Codigo);

/// <summary>Resultado do disparo do OTP.</summary>
public sealed record TelefoneOtpEmitidoDto(string Canal, string? Mascara, int ExpiraEmSegundos);

/// <summary>Situação de validação de um número.</summary>
public sealed record TelefoneValidadoDto(string Numero, bool Validado, DateTime? ValidadoEm);
