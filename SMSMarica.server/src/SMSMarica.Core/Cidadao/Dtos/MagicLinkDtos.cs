namespace SMSMarica.Core.Cidadao.Dtos;

/// <summary>Link de acesso (magic-link) gerado para enviar ao paciente.</summary>
public sealed record MagicLinkDto(Guid Token, string Url, DateTime ExpiraEm);

/// <summary>Corpo da troca do magic-link por sessão.</summary>
public sealed record MagicLinkTrocaRequest(Guid Token);

/// <summary>Resposta da troca: sessão do cidadão + rota de destino no app.</summary>
public sealed record RespostaMagicLinkDto(string Token, PacienteSessaoDto Paciente, string Destino);
