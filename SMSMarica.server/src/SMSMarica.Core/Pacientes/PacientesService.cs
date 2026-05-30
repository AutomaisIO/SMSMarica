using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Fhir;
using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Core.Pacientes;

/// <summary>
/// Serviço de pacientes operando direto sobre o agregado FHIR <c>Patient</c>
/// + tabelas-filhas (identifiers/names/addresses/telecoms/contacts/photos/
/// disabilities). Fatia 1 do refator FHIR — não cria/lê mais a entidade
/// legada <c>Paciente</c> nem <c>Usuario</c> (login do cidadão fica para
/// Fatia 2, quando <c>Usuario.PatientId</c> for adicionado).
/// </summary>
public sealed class PacientesService(SmsMaricaDbContext db, IUsuarioAtualAccessor atual) : IPacientesService
{
    private const int LimiteBusca = 10;
    private readonly SmsMaricaDbContext _db = db;
    private readonly IUsuarioAtualAccessor _atual = atual;

    public async Task<IReadOnlyList<PacienteListItemDto>> BuscarAsync(
        string? termo,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Patient> query = QueryAgregado().Where(p => p.DeletedAt == null);

        if (string.IsNullOrWhiteSpace(termo))
        {
            var recentes = await query
                .OrderByDescending(p => p.CreatedAt)
                .Take(LimiteBusca)
                .ToListAsync(cancellationToken);
            return [.. recentes.Select(PacientesMapper.ParaListItem)];
        }

        termo = termo.Trim();
        var digitos = NormalizarDigitos(termo);
        var ehBuscaCpf = digitos.Length is >= 3 and <= 11
            && digitos.Length == termo.Replace(".", "").Replace("-", "").Replace(" ", "").Length;

        if (ehBuscaCpf)
        {
            query = query.Where(p => p.Identifiers.Any(i =>
                i.Type == IdentifierTypeCode.Cpf && EF.Functions.Like(i.Value, "%" + digitos + "%")));
        }
        else
        {
            var tokens = termo.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var token in tokens)
            {
                var like = "%" + token + "%";
                query = query.Where(p => p.Names.Any(n => EF.Functions.ILike(n.Text, like)));
            }
        }

        var encontrados = await query
            .OrderBy(p => p.Names.OrderBy(n => n.Use).Select(n => n.Text).FirstOrDefault())
            .Take(LimiteBusca)
            .ToListAsync(cancellationToken);

        return [.. encontrados.Select(PacientesMapper.ParaListItem)];
    }

    public async Task<PacienteDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var paciente = await QueryAgregado()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Patient", id);

        return PacientesMapper.ParaDto(paciente);
    }

    public async Task<PacienteExistenciaDto?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        var normalizado = NormalizarDigitos(cpf);
        if (normalizado.Length != 11) return null;

        var patient = await _db.Patients.AsNoTracking()
            .Include(p => p.Names)
            .Include(p => p.Identifiers)
            .FirstOrDefaultAsync(p => p.Identifiers.Any(i =>
                i.Type == IdentifierTypeCode.Cpf && i.Value == normalizado), cancellationToken);

        if (patient is null) return null;

        var nome = patient.Names.FirstOrDefault(n => n.Use == NameUse.Official)?.Text
                ?? patient.Names.FirstOrDefault()?.Text
                ?? string.Empty;

        return new PacienteExistenciaDto(patient.Id, nome, normalizado, patient.DeletedAt == null);
    }

    public async Task<Guid> CadastrarAsync(CadastrarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var cpfNormalizado = NormalizarDigitos(request.Cpf);

        var existente = await _db.Patients.AsNoTracking()
            .Where(p => p.Identifiers.Any(i => i.Type == IdentifierTypeCode.Cpf && i.Value == cpfNormalizado))
            .Select(p => new { p.Id, p.DeletedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (existente is not null)
        {
            if (existente.DeletedAt is null)
            {
                throw new ConflitoException("paciente.cpf_duplicado", "Já existe paciente ativo com este CPF.");
            }
            throw new ConflitoException("paciente.cpf_excluido", "Existe paciente excluído com este CPF. Reative o cadastro.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;
        var patient = MontarPatient(request, cpfNormalizado, agora, atualId);

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync(cancellationToken);

        return patient.Id;
    }

    public Task<Guid> PromoverAsync(PromoverPacienteRequest request, CancellationToken cancellationToken = default)
    {
        // Fatia 1 do refator FHIR: o vínculo Usuario↔Patient (PatientId em Usuario)
        // entra na Fatia 2. Até lá, não é possível promover um Usuario existente
        // a Patient. O caminho válido é POST /pacientes (cria fhir.patient direto).
        throw new ConflitoException(
            "paciente.promover_indisponivel",
            "Promoção temporariamente indisponível — refator FHIR Fatia 2 (vínculo usuario↔patient) ainda não foi aplicado.");
    }

    public async Task AtualizarAsync(Guid id, AtualizarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var patient = await QueryAgregado()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Patient", id);

        if (patient.DeletedAt is not null)
        {
            throw new ConflitoException("paciente.excluido", "Paciente excluído não pode ser editado.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;

        // Patient base
        patient.Gender = PacientesMapper.ParaAdministrativeGender(request.Sexo);
        patient.MaritalStatus = PacientesMapper.ParaMaritalStatus(request.EstadoCivil);
        patient.Race = request.RacaCor;
        patient.EducationLevel = PacientesMapper.ParaEducationLevel(request.Escolaridade);
        patient.MothersMaidenName = NormalizaOpcional(request.NomeDaMae, false);
        patient.FathersName = NormalizaOpcional(request.NomeDoPai, false);
        patient.Notes = NormalizaOpcional(request.Observacoes, false);
        patient.UpdatedAt = agora;
        patient.UpdatedBy = atualId;
        patient.LastUpdated = agora;
        patient.VersionId += 1;

        // Identifiers: CNS + RG (CPF é imutável — ignora se vier)
        SubstituirIdentifier(patient, IdentifierTypeCode.Cns, PacientesMapper.SystemCns,
            NormalizaOpcional(request.Cns, true));
        SubstituirIdentifier(patient, IdentifierTypeCode.Rg, PacientesMapper.SystemRg,
            NormalizaOpcional(request.Rg, false));

        // Names: NomeSocial (Nickname). Oficial é imutável (igual ao gate inicial).
        SubstituirNomeSocial(patient, NormalizaOpcional(request.NomeSocial, false));

        // Address Home
        SubstituirEnderecoHome(patient, request.Endereco);

        // Telecoms (limpa Phone/Email — recria com base no request)
        SubstituirTelecoms(patient,
            request.TelefonePrincipal,
            request.TelefoneCelular,
            request.TelefoneResidencial,
            request.Email);

        // Contato emergência
        SubstituirContatoEmergencia(patient, request.ContatoEmergencia);

        // Foto principal
        SubstituirFotoPrincipal(patient, NormalizaOpcional(request.FotoBase64, false), agora);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Patient", id);

        if (patient.DeletedAt is not null)
        {
            throw new ConflitoException("paciente.ja_excluido", "Paciente já foi excluído.");
        }

        var agora = DateTime.UtcNow;
        patient.DeletedAt = agora;
        patient.DeletedBy = _atual.UsuarioId;
        patient.Active = false;
        patient.LastUpdated = agora;
        patient.VersionId += 1;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ReativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Patient", id);

        if (patient.DeletedAt is null)
        {
            throw new ConflitoException("paciente.nao_excluido", "Paciente não está excluído.");
        }

        var agora = DateTime.UtcNow;
        patient.DeletedAt = null;
        patient.DeletedBy = null;
        patient.Active = true;
        patient.UpdatedAt = agora;
        patient.UpdatedBy = _atual.UsuarioId;
        patient.LastUpdated = agora;
        patient.VersionId += 1;

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---------------- helpers ----------------

    private IQueryable<Patient> QueryAgregado() => _db.Patients
        .Include(p => p.Names)
        .Include(p => p.Identifiers)
        .Include(p => p.Addresses).ThenInclude(a => a.Municipio)
        .Include(p => p.Telecoms)
        .Include(p => p.Contacts)
        .Include(p => p.Communications)
        .Include(p => p.Disabilities)
        .Include(p => p.Photos)
        .Include(p => p.BirthMunicipio)
        .Include(p => p.BirthCountry);

    private static Patient MontarPatient(CadastrarPacienteRequest req, string cpf, DateTime agora, Guid? atualId)
    {
        var patient = new Patient
        {
            Id = Guid.CreateVersion7(),
            Active = true,
            Gender = PacientesMapper.ParaAdministrativeGender(req.Sexo),
            BirthDate = req.DataNascimento,
            MaritalStatus = PacientesMapper.ParaMaritalStatus(req.EstadoCivil),
            Race = req.RacaCor,
            EducationLevel = PacientesMapper.ParaEducationLevel(req.Escolaridade),
            MothersMaidenName = NormalizaOpcional(req.NomeDaMae, false),
            FathersName = NormalizaOpcional(req.NomeDoPai, false),
            UseSocialName = !string.IsNullOrWhiteSpace(req.NomeSocial),
            Notes = NormalizaOpcional(req.Observacoes, false),
            CreatedAt = agora,
            CreatedBy = atualId,
            LastUpdated = agora,
            VersionId = 1,
        };

        // Identifiers
        patient.Identifiers.Add(new PatientIdentifier
        {
            Id = Guid.CreateVersion7(),
            PatientId = patient.Id,
            System = PacientesMapper.SystemCpf,
            Value = cpf,
            Type = IdentifierTypeCode.Cpf,
            Use = IdentifierUse.Official,
        });

        var cns = NormalizaOpcional(req.Cns, true);
        if (cns is not null)
        {
            patient.Identifiers.Add(new PatientIdentifier
            {
                Id = Guid.CreateVersion7(),
                PatientId = patient.Id,
                System = PacientesMapper.SystemCns,
                Value = cns,
                Type = IdentifierTypeCode.Cns,
                Use = IdentifierUse.Official,
            });
        }

        var rg = NormalizaOpcional(req.Rg, false);
        if (rg is not null)
        {
            patient.Identifiers.Add(new PatientIdentifier
            {
                Id = Guid.CreateVersion7(),
                PatientId = patient.Id,
                System = PacientesMapper.SystemRg,
                Value = rg,
                Type = IdentifierTypeCode.Rg,
                Use = IdentifierUse.Official,
            });
        }

        // Names (Official + opcional Nickname/social)
        var nomeOficial = req.NomeCompleto.Trim();
        var partesNome = nomeOficial.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        patient.Names.Add(new PatientName
        {
            Id = Guid.CreateVersion7(),
            PatientId = patient.Id,
            Use = NameUse.Official,
            Text = nomeOficial,
            Family = partesNome.Length > 1 ? partesNome[^1] : null,
            Given = partesNome.Length > 1 ? partesNome[..^1] : partesNome,
        });

        var nomeSocial = NormalizaOpcional(req.NomeSocial, false);
        if (nomeSocial is not null)
        {
            patient.Names.Add(new PatientName
            {
                Id = Guid.CreateVersion7(),
                PatientId = patient.Id,
                Use = NameUse.Nickname,
                Text = nomeSocial,
            });
        }

        // Address Home
        if (req.Endereco is not null)
        {
            var addr = PacientesMapper.ParaPatientAddress(req.Endereco);
            addr.PatientId = patient.Id;
            patient.Addresses.Add(addr);
        }

        // Telecoms
        var rank = 1;
        if (!string.IsNullOrWhiteSpace(req.TelefonePrincipal))
        {
            patient.Telecoms.Add(NovoTelecom(patient.Id, ContactPointSystem.Phone, ContactPointUse.Home, req.TelefonePrincipal, rank++));
        }
        if (!string.IsNullOrWhiteSpace(req.TelefoneCelular))
        {
            patient.Telecoms.Add(NovoTelecom(patient.Id, ContactPointSystem.Phone, ContactPointUse.Mobile, req.TelefoneCelular, rank++));
        }
        if (!string.IsNullOrWhiteSpace(req.TelefoneResidencial))
        {
            patient.Telecoms.Add(NovoTelecom(patient.Id, ContactPointSystem.Phone, ContactPointUse.Home, req.TelefoneResidencial, rank++));
        }
        if (!string.IsNullOrWhiteSpace(req.Email))
        {
            patient.Telecoms.Add(NovoTelecom(patient.Id, ContactPointSystem.Email, ContactPointUse.Home, req.Email.Trim().ToLowerInvariant(), 1));
        }

        // Contato emergência
        if (req.ContatoEmergencia is not null)
        {
            var contato = PacientesMapper.ParaPatientContact(req.ContatoEmergencia);
            contato.PatientId = patient.Id;
            patient.Contacts.Add(contato);
        }

        // Deficiências
        if (req.Deficiencias is { Count: > 0 })
        {
            foreach (var desc in req.Deficiencias.Where(d => !string.IsNullOrWhiteSpace(d)))
            {
                patient.Disabilities.Add(new PatientDisability
                {
                    Id = Guid.CreateVersion7(),
                    PatientId = patient.Id,
                    Type = DisabilityType.Outro,
                    Description = desc.Trim(),
                });
            }
        }

        // Foto principal
        var foto = NormalizaOpcional(req.FotoBase64, false);
        if (foto is not null)
        {
            patient.Photos.Add(new PatientPhoto
            {
                Id = Guid.CreateVersion7(),
                PatientId = patient.Id,
                ContentType = "image/jpeg",
                DataBase64 = foto,
                CreatedAt = agora,
                IsPrimary = true,
                AuthorizedDisplay = false,
            });
        }

        return patient;
    }

    private static PatientTelecom NovoTelecom(Guid patientId, ContactPointSystem system, ContactPointUse use, string value, int rank) => new()
    {
        Id = Guid.CreateVersion7(),
        PatientId = patientId,
        System = system,
        Use = use,
        Value = value.Trim(),
        Rank = rank,
    };

    private void SubstituirIdentifier(Patient patient, IdentifierTypeCode tipo, string system, string? valor)
    {
        var atual = patient.Identifiers.FirstOrDefault(i => i.Type == tipo);
        if (valor is null)
        {
            if (atual is not null) patient.Identifiers.Remove(atual);
            return;
        }
        if (atual is null)
        {
            patient.Identifiers.Add(new PatientIdentifier
            {
                Id = Guid.CreateVersion7(),
                PatientId = patient.Id,
                System = system,
                Value = valor,
                Type = tipo,
                Use = IdentifierUse.Official,
            });
        }
        else
        {
            atual.Value = valor;
            atual.System = system;
        }
    }

    private void SubstituirNomeSocial(Patient patient, string? nomeSocial)
    {
        var atual = patient.Names.FirstOrDefault(n => n.Use == NameUse.Nickname);
        if (nomeSocial is null)
        {
            if (atual is not null) patient.Names.Remove(atual);
            patient.UseSocialName = false;
            return;
        }
        if (atual is null)
        {
            patient.Names.Add(new PatientName
            {
                Id = Guid.CreateVersion7(),
                PatientId = patient.Id,
                Use = NameUse.Nickname,
                Text = nomeSocial,
            });
        }
        else
        {
            atual.Text = nomeSocial;
        }
        patient.UseSocialName = true;
    }

    private void SubstituirEnderecoHome(Patient patient, Common.Dtos.EnderecoDto? dto)
    {
        var existentes = patient.Addresses.Where(a => a.Use == AddressUse.Home).ToList();
        foreach (var e in existentes) patient.Addresses.Remove(e);
        if (dto is null) return;

        var addr = PacientesMapper.ParaPatientAddress(dto);
        addr.PatientId = patient.Id;
        patient.Addresses.Add(addr);
    }

    private void SubstituirTelecoms(Patient patient, string? principal, string? celular, string? residencial, string? email)
    {
        var paraRemover = patient.Telecoms
            .Where(t => t.System is ContactPointSystem.Phone or ContactPointSystem.Email)
            .ToList();
        foreach (var t in paraRemover) patient.Telecoms.Remove(t);

        var rank = 1;
        if (!string.IsNullOrWhiteSpace(principal))
        {
            patient.Telecoms.Add(NovoTelecom(patient.Id, ContactPointSystem.Phone, ContactPointUse.Home, principal, rank++));
        }
        if (!string.IsNullOrWhiteSpace(celular))
        {
            patient.Telecoms.Add(NovoTelecom(patient.Id, ContactPointSystem.Phone, ContactPointUse.Mobile, celular, rank++));
        }
        if (!string.IsNullOrWhiteSpace(residencial))
        {
            patient.Telecoms.Add(NovoTelecom(patient.Id, ContactPointSystem.Phone, ContactPointUse.Home, residencial, rank++));
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            patient.Telecoms.Add(NovoTelecom(patient.Id, ContactPointSystem.Email, ContactPointUse.Home, email.Trim().ToLowerInvariant(), 1));
        }
    }

    private void SubstituirContatoEmergencia(Patient patient, ContatoEmergenciaDto? dto)
    {
        var existentes = patient.Contacts.Where(c => c.Relationship == PatientContactRelationship.Emergency).ToList();
        foreach (var c in existentes) patient.Contacts.Remove(c);
        if (dto is null) return;

        var contato = PacientesMapper.ParaPatientContact(dto);
        contato.PatientId = patient.Id;
        patient.Contacts.Add(contato);
    }

    private void SubstituirFotoPrincipal(Patient patient, string? base64, DateTime agora)
    {
        var existentes = patient.Photos.Where(f => f.IsPrimary).ToList();
        foreach (var f in existentes) patient.Photos.Remove(f);
        if (base64 is null) return;

        patient.Photos.Add(new PatientPhoto
        {
            Id = Guid.CreateVersion7(),
            PatientId = patient.Id,
            ContentType = "image/jpeg",
            DataBase64 = base64,
            CreatedAt = agora,
            IsPrimary = true,
            AuthorizedDisplay = false,
        });
    }

    private static string NormalizarDigitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new([.. valor.Where(char.IsDigit)]);

    private static string? NormalizaOpcional(string? valor, bool soDigitos)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var v = valor.Trim();
        return soDigitos ? NormalizarDigitos(v) : v;
    }
}
