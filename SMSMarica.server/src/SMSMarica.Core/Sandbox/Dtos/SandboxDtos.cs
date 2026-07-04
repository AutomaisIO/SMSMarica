namespace SMSMarica.Core.Sandbox.Dtos;

/// <summary>Resultado da busca de paciente real para operar no sandbox.</summary>
public sealed record SandboxPacienteDto(Guid Id, string Nome, string? Cpf);

/// <summary>Solicitação de exame do paciente (para escolher em qual testar a confirmação).</summary>
public sealed record SandboxSolicitacaoDto(
    Guid Id,
    string AccessionNumber,
    string? TipoExame,
    DateTime? DataAgendada,
    string Status,
    string StatusConfirmacao);

public sealed record GerarLinkRequest(Guid PacienteId, string? Destino);

public sealed record LinkTesteDto(string Url, DateTime ExpiraEm);

public sealed record EnviarMensagemTesteRequest(
    string Telefone,
    string Texto,
    /// <summary>Se informado, gera um magic link do paciente e o anexa ao texto.</summary>
    Guid? PacienteId,
    string? Destino);

public sealed record ResultadoEnvioTesteDto(bool Ok, string? Erro, string? Link);

/// <summary>Força o estado de confirmação de uma solicitação (reversível): "pendente" | "confirmada" | "cancelada".</summary>
public sealed record DefinirConfirmacaoRequest(Guid SolicitacaoExameId, string Estado, string? Motivo);
