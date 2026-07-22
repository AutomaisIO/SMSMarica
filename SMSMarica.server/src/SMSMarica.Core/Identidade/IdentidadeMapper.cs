using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Identidade.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Identidade;

/// <summary>Vínculo médico resolvido pelo CPF no hub FHIR (Practitioner).</summary>
public sealed record VinculoMedico(string Conselho, string Registro, string UfConselho)
{
    /// <summary>Ex.: "CRM 52702650/RJ" (omite partes vazias).</summary>
    public string Texto => string.Join(" ",
        new[] { Conselho, Registro }.Where(x => !string.IsNullOrWhiteSpace(x)))
        + (string.IsNullOrWhiteSpace(UfConselho) ? "" : "/" + UfConselho);
}

internal static class IdentidadeMapper
{
    public static UsuarioDto ParaDto(Usuario u, IEnumerable<Guid>? perfilIds = null, VinculoMedico? medico = null) => new(
        u.Id, u.NomeCompleto, u.Email, u.Cpf, u.DataNascimento, u.Telefone,
        u.Endereco is null ? null : EnderecoDto.ParaDto(u.Endereco),
        u.FotoBase64,
        u.Ativo, u.CriadoEm, u.UltimoAcessoEm,
        [.. perfilIds ?? u.UsuariosPerfis.Select(up => up.PerfilId)],
        u.DeveTrocarSenha,
        // Motorista (linha 1:1) tem prioridade; senão, médico resolvido via FHIR pelo CPF.
        DetectarPapel(u) ?? (medico is not null ? "Medico" : null),
        medico?.Texto,
        u.Login,
        u.AcessoGlobal);

    public static UsuarioListItemDto ParaListItem(Usuario u) =>
        new(u.Id, u.NomeCompleto, u.Cpf, u.Email, u.FotoBase64, u.Ativo, u.DeveTrocarSenha);

    private static string? DetectarPapel(Usuario u)
    {
        // Paciente e Médico migraram para o hub FHIR — Usuário só tem papel Motorista.
        if (u.Motorista is not null) return "Motorista";
        return null;
    }
}
