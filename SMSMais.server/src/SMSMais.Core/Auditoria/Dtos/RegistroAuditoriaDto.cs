namespace SMSMais.Core.Auditoria.Dtos;

/// <summary>Quem executou a ação auditada.</summary>
public enum TipoAtorAuditoria
{
    /// <summary>Id não resolvido em usuário nem paciente (ex.: registro antigo, job).</summary>
    Desconhecido = 0,
    /// <summary>Usuário do sistema (equipe, logado no painel).</summary>
    UsuarioSistema = 1,
    /// <summary>Paciente (agiu pelo app do cidadão sobre o próprio cadastro).</summary>
    Paciente = 2,
}

/// <summary>Uma linha da trilha de auditoria.</summary>
public sealed record RegistroAuditoriaDto(
    Guid Id,
    string Entidade,
    string EntidadeId,
    string Acao,
    string? ValorAnterior,
    string? ValorNovo,
    Guid? UsuarioId,
    /// <summary>Nome do ator resolvido na leitura: usuário do sistema OU paciente (app).
    /// Retroativo — preenche registros antigos que ficaram sem nome.</summary>
    string? UsuarioNome,
    /// <summary>Tipo do ator (usuário do sistema vs paciente) — o "quem é" pedido na tela.</summary>
    TipoAtorAuditoria AtorTipo,
    /// <summary>Nome da entidade AFETADA (ex.: o paciente cujo cadastro mudou). Resolvido do FHIR.</summary>
    string? EntidadeNome,
    /// <summary>IP de onde a ação partiu (null em registros antigos/jobs).</summary>
    string? Ip,
    DateTime CriadoEm);

/// <summary>Filtros da busca de auditoria (todos opcionais, combináveis).</summary>
public sealed record AuditoriaFiltroDto(
    string? Entidade = null,
    string? EntidadeId = null,
    Guid? UsuarioId = null,
    string? Texto = null,
    DateTime? De = null,
    DateTime? Ate = null,
    int Pagina = 1,
    int Tamanho = 50);

/// <summary>Página de resultados da busca de auditoria.</summary>
public sealed record PaginaAuditoriaDto(
    IReadOnlyList<RegistroAuditoriaDto> Itens,
    int Total,
    int Pagina,
    int Tamanho);
