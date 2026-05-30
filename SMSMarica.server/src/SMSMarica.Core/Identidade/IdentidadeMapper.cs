using SMSMarica.Core.Identidade.Dtos;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Identidade;

/// <summary>
/// Mapeia <see cref="Usuario"/> + papel para <see cref="UsuarioDto"/>.
/// Após o refator FHIR (Fatias 2–4) os campos de identidade (nome, CPF,
/// data de nascimento, foto) saem do papel correspondente:
/// <list type="bullet">
/// <item>Patient → <c>fhir.patient</c> + colaterais (names/identifiers/photos)</item>
/// <item>Practitioner → <c>fhir.practitioner</c> + colaterais</item>
/// <item>Motorista → <c>smsmarica.motorista</c> (inline)</item>
/// <item>Sem papel → fallback em <c>Usuario.NomeExibicao</c></item>
/// </list>
/// </summary>
internal static class IdentidadeMapper
{
    public static UsuarioDto ParaDto(Usuario u, IEnumerable<Guid>? perfilIds = null)
    {
        var papel = DetectarPapel(u);
        var nome = ResolverNome(u);
        var cpf = ResolverCpf(u);
        var dataNascimento = ResolverDataNascimento(u);
        var fotoBase64 = ResolverFoto(u);

        return new UsuarioDto(
            u.Id,
            nome,
            u.Email,
            cpf,
            dataNascimento,
            null, // Telefone vive no papel — não exposto aqui
            null, // Endereco idem
            fotoBase64,
            u.Ativo,
            u.CriadoEm,
            u.UltimoAcessoEm,
            [.. perfilIds ?? u.UsuariosPerfis.Select(up => up.PerfilId)],
            u.DeveTrocarSenha,
            papel);
    }

    public static UsuarioListItemDto ParaListItem(Usuario u) =>
        new(u.Id, ResolverNome(u), u.Email, ResolverFoto(u), u.Ativo, u.DeveTrocarSenha);

    private static string? DetectarPapel(Usuario u)
    {
        if (u.PractitionerId is not null) return "Medico";
        if (u.MotoristaId is not null) return "Motorista";
        if (u.PatientId is not null) return "Paciente";
        return null;
    }

    private static string ResolverNome(Usuario u)
    {
        if (u.Patient is not null)
        {
            var nomeOficial = u.Patient.Names
                .FirstOrDefault(n => n.Use == Data.Entities.Fhir.Enums.NameUse.Official)?.Text
                ?? u.Patient.Names.FirstOrDefault()?.Text;
            if (!string.IsNullOrWhiteSpace(nomeOficial)) return nomeOficial;
        }

        if (u.Practitioner is not null)
        {
            var nomeOficial = u.Practitioner.Names
                .FirstOrDefault(n => n.Use == Data.Entities.Fhir.Enums.NameUse.Official)?.Text
                ?? u.Practitioner.Names.FirstOrDefault()?.Text;
            if (!string.IsNullOrWhiteSpace(nomeOficial)) return nomeOficial;
        }

        if (u.Motorista is not null && !string.IsNullOrWhiteSpace(u.Motorista.NomeCompleto))
        {
            return u.Motorista.NomeCompleto;
        }

        return u.NomeExibicao;
    }

    private static string? ResolverCpf(Usuario u)
    {
        if (u.Patient is not null)
        {
            var cpf = u.Patient.Identifiers
                .FirstOrDefault(i => i.Type == Data.Entities.Fhir.Enums.IdentifierTypeCode.Cpf)?.Value;
            if (!string.IsNullOrWhiteSpace(cpf)) return cpf;
        }

        if (u.Practitioner is not null)
        {
            var cpf = u.Practitioner.Identifiers
                .FirstOrDefault(i => i.Type == Data.Entities.Fhir.Enums.IdentifierTypeCode.Cpf)?.Value;
            if (!string.IsNullOrWhiteSpace(cpf)) return cpf;
        }

        if (u.Motorista is not null && !string.IsNullOrWhiteSpace(u.Motorista.Cpf))
        {
            return u.Motorista.Cpf;
        }

        return null;
    }

    private static DateOnly? ResolverDataNascimento(Usuario u) =>
        u.Patient?.BirthDate
        ?? u.Practitioner?.BirthDate
        ?? u.Motorista?.DataNascimento;

    private static string? ResolverFoto(Usuario u)
    {
        if (u.Patient is not null)
        {
            var foto = u.Patient.Photos.FirstOrDefault(f => f.IsPrimary)
                    ?? u.Patient.Photos.FirstOrDefault();
            if (foto?.DataBase64 is not null) return foto.DataBase64;
        }
        if (u.Motorista is not null && !string.IsNullOrWhiteSpace(u.Motorista.FotoBase64))
        {
            return u.Motorista.FotoBase64;
        }
        return null;
    }
}
