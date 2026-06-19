namespace SMSMarica.Core.Cidadao.Dtos;

public sealed record SolicitarOtpRequest(string Cpf);

/// <summary>
/// Resultado de solicitar o código. Em <b>modo de teste</b> (WhatsApp ainda não ativo),
/// <see cref="CodigoTeste"/> traz o código para ser exibido na tela; em produção será null
/// (o código vai só pelo WhatsApp).
/// </summary>
public sealed record OtpEmitidoDto(bool Enviado, string Canal, string? CodigoTeste, int ValidadeSegundos);

public sealed record ValidarOtpRequest(string Cpf, string Codigo);

public sealed record PacienteSessaoDto(Guid Id, string Nome, string Cpf);

public sealed record RespostaLoginPacienteDto(string Token, PacienteSessaoDto Paciente);
