using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Acompanhantes.Dtos;

public sealed record AcompanhanteDto(
    Guid Id,
    Guid PacienteId,
    string Cpf,
    string Nome,
    DateOnly DataNascimento,
    ParentescoAcompanhante? Parentesco,
    string? Telefone,
    /// <summary>A pessoa também é paciente na base.</summary>
    bool TambemEPaciente,
    OrigemCadastroAcompanhante Origem,
    DateTime CriadoEm);

/// <summary>CPF + nascimento para conferir quem é a pessoa antes de cadastrar.</summary>
public sealed record ConsultarAcompanhanteRequest(string Cpf, DateOnly DataNascimento);

/// <summary>A pessoa encontrada pelo par CPF + nascimento — para quem cadastra confirmar o nome.</summary>
public sealed record AcompanhanteConsultaDto(
    string Cpf,
    string Nome,
    DateOnly DataNascimento,
    bool JaCadastrado);

/// <summary>O nome nunca vem do cliente: o servidor confere o par CPF + nascimento de novo.</summary>
public sealed record AdicionarAcompanhanteRequest(
    string Cpf,
    DateOnly DataNascimento,
    ParentescoAcompanhante? Parentesco,
    string? Telefone);

/// <summary>
/// O acompanhante como o próprio paciente vê no app. Sem <c>TambemEPaciente</c> nem ids internos:
/// dizer a quem cadastrou que aquela pessoa é paciente da rede seria revelar dado de saúde de outra
/// pessoa.
/// </summary>
public sealed record AcompanhanteCidadaoDto(
    Guid Id,
    string Cpf,
    string Nome,
    DateOnly DataNascimento,
    ParentescoAcompanhante? Parentesco,
    string? Telefone,
    OrigemCadastroAcompanhante Origem)
{
    public static AcompanhanteCidadaoDto De(AcompanhanteDto a) =>
        new(a.Id, a.Cpf, a.Nome, a.DataNascimento, a.Parentesco, a.Telefone, a.Origem);
}
