namespace SMSMarica.Core.Cidadao.Dtos;

/// <summary>Um acesso (sessão) do cidadão — para a aba "Histórico de Acesso" no painel.</summary>
public sealed record AcessoCidadaoDto(
    Guid Id,
    string Canal,
    string? Dispositivo,
    string? Ip,
    DateTime CriadaEm,
    DateTime ExpiraEm,
    DateTime? RevogadaEm,
    bool Ativa);
