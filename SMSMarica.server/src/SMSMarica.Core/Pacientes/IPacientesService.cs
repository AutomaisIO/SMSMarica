using SMSMarica.Core.Pacientes.Dtos;

namespace SMSMarica.Core.Pacientes;

public interface IPacientesService
{
    /// <summary>
    /// Busca em tempo real por nome (qualquer parte, múltiplos tokens) ou CPF
    /// (formatado ou não). Sem termo retorna os 10 últimos cadastros ativos
    /// (ordenados por CriadoEm desc). Com termo, limita a 10 ocorrências mais
    /// relevantes. Soft-deleted não aparecem.
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

    /// <summary>
    /// Promove um <see cref="SMSMarica.Data.Entities.Usuario"/> existente
    /// (sem papel atual) a Paciente, criando linha em paciente com os campos
    /// específicos. Papel é determinado pela existência da linha 1:1 (ADR-0006).
    /// </summary>
    Task<Guid> PromoverAsync(PromoverPacienteRequest request, CancellationToken cancellationToken = default);

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
