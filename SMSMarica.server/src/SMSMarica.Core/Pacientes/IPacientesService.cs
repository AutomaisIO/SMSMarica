using SMSMarica.Core.Pacientes.Dtos;

namespace SMSMarica.Core.Pacientes;

public interface IPacientesService
{
    /// <summary>
    /// Busca em tempo real por nome (qualquer parte, múltiplos tokens) ou CPF
    /// (formatado ou não). Sem termo retorna lista vazia. Limite 20. Soft-deleted
    /// não aparecem.
    /// </summary>
    Task<IReadOnlyList<PacienteListItemDto>> BuscarAsync(
        string? termo,
        CancellationToken cancellationToken = default);

    Task<PacienteDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retorna informação mínima para verificação de existência por CPF, incluindo
    /// registros desativados. Usado pelo fluxo de cadastro para oferecer reativação.
    /// </summary>
    Task<PacienteExistenciaDto?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default);

    Task<Guid> CadastrarAsync(CadastrarPacienteRequest request, CancellationToken cancellationToken = default);

    Task AtualizarAsync(Guid id, AtualizarPacienteRequest request, CancellationToken cancellationToken = default);

    Task DesativarAsync(Guid id, CancellationToken cancellationToken = default);

    Task ReativarAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Resumo para checar existência por CPF (inclusive inativos).</summary>
public sealed record PacienteExistenciaDto(
    Guid Id,
    string NomeCompleto,
    string Cpf,
    bool Ativo);
