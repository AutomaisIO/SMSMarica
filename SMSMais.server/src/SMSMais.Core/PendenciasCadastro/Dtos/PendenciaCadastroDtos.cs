using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.PendenciasCadastro.Dtos;

public sealed record PendenciaCadastroListItemDto(
    Guid Id,
    string TelefoneCanonical,
    Guid? PacienteId,
    string? PacienteNome,
    string? PacienteCpf,
    TipoPendenciaCadastro Tipo,
    VinculoContato Vinculo,
    string? Observacao,
    StatusPendenciaCadastro Status,
    bool CriadaPeloRobo,
    DateTime CriadoEm,
    DateTime? ResolvidoEm,
    string? ResolucaoNota);

public sealed record ResolverPendenciaRequest(string? Nota);
