using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Medicos.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Fhir;
using SMSMarica.Data.Entities.Fhir.Enums;

namespace SMSMarica.Core.Medicos;

/// <summary>
/// Serviço de médicos operando sobre o agregado FHIR <see cref="Practitioner"/>.
/// Em Fatia 3 do refator FHIR, a entidade <c>smsmarica.Medico</c> foi removida —
/// "Médico" passa a ser um Practitioner com Qualification CouncilCode='CRM',
/// vinculado a um <see cref="Usuario"/> via <c>Usuario.PractitionerId</c>.
/// </summary>
public sealed class MedicosService(SmsMaricaDbContext db, IUsuarioAtualAccessor atual) : IMedicosService
{
    private const string SenhaHashPlaceholder = "PENDENTE_AUTH";
    private readonly SmsMaricaDbContext _db = db;
    private readonly IUsuarioAtualAccessor _atual = atual;

    public async Task<IReadOnlyList<MedicoListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        // Lista Practitioners que têm qualification CRM (=médicos).
        var practitioners = await QueryAgregado()
            .AsNoTracking()
            .Where(p => p.DeletedAt == null
                && p.Qualifications.Any(q => q.CouncilCode == MedicosMapper.CouncilCrm))
            .ToListAsync(cancellationToken);

        var usuariosPorPractitionerId = await CarregarUsuariosAsync(
            practitioners.Select(p => p.Id).ToList(), cancellationToken);

        return [.. practitioners
            .OrderBy(p => MedicosMapper.NomeOficial(p))
            .Select(p => MedicosMapper.ParaListItem(p, usuariosPorPractitionerId.GetValueOrDefault(p.Id)))];
    }

    public async Task<MedicoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var practitioner = await QueryAgregado()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Practitioner), id);

        var usuario = await _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.PractitionerId == practitioner.Id, cancellationToken);

        return MedicosMapper.ParaDto(practitioner, usuario);
    }

    public async Task<Guid> CadastrarAsync(CadastrarMedicoRequest request, CancellationToken cancellationToken = default)
    {
        var cpf = NormalizarDigitos(request.Cpf);
        var crm = NormalizarDigitos(request.Crm);
        var uf = (request.UfCrm ?? string.Empty).Trim().ToUpperInvariant();

        // CPF duplicado em outro Practitioner.
        if (await _db.Practitioners.AsNoTracking()
            .AnyAsync(p => p.DeletedAt == null && p.Identifiers.Any(i =>
                i.Type == IdentifierTypeCode.Cpf && i.Value == cpf), cancellationToken))
        {
            throw new ConflitoException("medico.cpf_duplicado", "Já existe profissional com este CPF.");
        }

        // CRM/UF duplicado (constraint única em PractitionerQualification cuida no banco).
        if (await _db.PractitionerQualifications.AsNoTracking()
            .AnyAsync(q => q.CouncilCode == MedicosMapper.CouncilCrm
                        && q.CouncilNumber == crm
                        && q.CouncilState == uf
                        && q.Practitioner.DeletedAt == null, cancellationToken))
        {
            throw new ConflitoException("medico.crm_duplicado", $"Já existe médico com CRM {crm}/{uf}.");
        }

        var email = NormalizarEmail(request.Email, cpf);
        if (await _db.Usuarios.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflitoException("medico.email_duplicado", "Já existe usuário com este e-mail.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;

        var practitioner = MontarPractitioner(request, cpf, crm, uf, agora, atualId);

        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            Email = email,
            SenhaHash = SenhaHashPlaceholder,
            DeveTrocarSenha = true,
            Ativo = true,
            NomeExibicao = request.NomeCompleto.Trim(),
            PractitionerId = practitioner.Id,
            CriadoEm = agora,
            CriadoPor = atualId,
        };

        _db.Practitioners.Add(practitioner);
        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync(cancellationToken);
        return practitioner.Id;
    }

    public Task<Guid> PromoverAsync(PromoverMedicoRequest request, CancellationToken cancellationToken = default)
    {
        // Fluxo de "promover Usuario sem papel a Médico" foi reformulado na
        // Fatia 2 do refator FHIR: Usuario não carrega mais identidade clínica
        // (CPF/nome/data de nascimento), então não dá pra reaproveitar. Front
        // deve usar Cadastrar normal.
        throw new ConflitoException(
            "medico.promover_indisponivel",
            "Promoção descontinuada — Usuario sem papel não carrega mais identidade clínica. Use POST /medicos.");
    }

    public async Task AtualizarAsync(Guid id, AtualizarMedicoRequest request, CancellationToken cancellationToken = default)
    {
        var practitioner = await QueryAgregado()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Practitioner), id);

        if (practitioner.DeletedAt is not null)
        {
            throw new ConflitoException("medico.excluido", "Médico excluído não pode ser editado.");
        }

        var crm = NormalizarDigitos(request.Crm);
        var uf = (request.UfCrm ?? string.Empty).Trim().ToUpperInvariant();
        var qualificacao = practitioner.Qualifications.FirstOrDefault(q => q.CouncilCode == MedicosMapper.CouncilCrm);

        // Mudança de CRM exige checar unicidade contra outros Practitioners.
        if (qualificacao is null || qualificacao.CouncilNumber != crm || qualificacao.CouncilState != uf)
        {
            if (await _db.PractitionerQualifications.AsNoTracking()
                .AnyAsync(q => q.CouncilCode == MedicosMapper.CouncilCrm
                            && q.CouncilNumber == crm
                            && q.CouncilState == uf
                            && q.PractitionerId != id
                            && q.Practitioner.DeletedAt == null, cancellationToken))
            {
                throw new ConflitoException("medico.crm_duplicado", $"Já existe médico com CRM {crm}/{uf}.");
            }
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;

        // CRM / Especialidade / Validade
        if (qualificacao is null)
        {
            practitioner.Qualifications.Add(new PractitionerQualification
            {
                Id = Guid.CreateVersion7(),
                PractitionerId = practitioner.Id,
                CouncilCode = MedicosMapper.CouncilCrm,
                CouncilNumber = crm,
                CouncilState = uf,
                SpecialtyName = NormalizaOpcional(request.Especialidade),
                PeriodEnd = request.ValidadeCrm,
            });
        }
        else
        {
            qualificacao.CouncilNumber = crm;
            qualificacao.CouncilState = uf;
            qualificacao.SpecialtyName = NormalizaOpcional(request.Especialidade);
            qualificacao.PeriodEnd = request.ValidadeCrm;
        }

        // RQE como PractitionerIdentifier (System=urn:br:rqe)
        SubstituirIdentifier(practitioner, MedicosMapper.SystemRqe, IdentifierTypeCode.Other, NormalizaOpcional(request.Rqe));

        // Telefone (Phone, Rank=1)
        SubstituirTelefone(practitioner, request.Telefone);

        // Endereço
        SubstituirEndereco(practitioner, request.Endereco);

        practitioner.UpdatedAt = agora;
        practitioner.UpdatedBy = atualId;
        practitioner.LastUpdated = agora;
        practitioner.VersionId += 1;

        // Sincroniza NomeExibicao no Usuario (caso exista).
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.PractitionerId == practitioner.Id, cancellationToken);
        if (usuario is not null)
        {
            usuario.NomeExibicao = MedicosMapper.NomeOficial(practitioner);
            usuario.AtualizadoEm = agora;
            usuario.AtualizadoPor = atualId;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var practitioner = await _db.Practitioners.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Practitioner), id);

        if (practitioner.DeletedAt is not null)
        {
            throw new ConflitoException("medico.ja_excluido", "Médico já foi excluído.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;

        practitioner.DeletedAt = agora;
        practitioner.DeletedBy = atualId;
        practitioner.Active = false;
        practitioner.LastUpdated = agora;
        practitioner.VersionId += 1;

        // Exclusão de papel cascateia pra Usuario vinculado.
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.PractitionerId == practitioner.Id, cancellationToken);
        if (usuario is not null)
        {
            usuario.ExcluidoEm = agora;
            usuario.ExcluidoPor = atualId;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---------------- helpers ----------------

    private IQueryable<Practitioner> QueryAgregado() => _db.Practitioners
        .Include(p => p.Names)
        .Include(p => p.Identifiers)
        .Include(p => p.Addresses).ThenInclude(a => a.Municipio)
        .Include(p => p.Telecoms)
        .Include(p => p.Qualifications);

    private async Task<Dictionary<Guid, Usuario>> CarregarUsuariosAsync(
        IReadOnlyList<Guid> practitionerIds, CancellationToken ct)
    {
        if (practitionerIds.Count == 0) return [];
        var usuarios = await _db.Usuarios.AsNoTracking()
            .Where(u => u.PractitionerId.HasValue && practitionerIds.Contains(u.PractitionerId.Value))
            .ToListAsync(ct);
        return usuarios.ToDictionary(u => u.PractitionerId!.Value);
    }

    private static Practitioner MontarPractitioner(
        CadastrarMedicoRequest req, string cpf, string crm, string uf, DateTime agora, Guid? atualId)
    {
        var practitioner = new Practitioner
        {
            Id = Guid.CreateVersion7(),
            Active = true,
            Gender = AdministrativeGender.Unknown,
            BirthDate = req.DataNascimento,
            CreatedAt = agora,
            CreatedBy = atualId,
            LastUpdated = agora,
            VersionId = 1,
        };

        var nome = req.NomeCompleto.Trim();
        var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        practitioner.Names.Add(new PractitionerName
        {
            Id = Guid.CreateVersion7(),
            PractitionerId = practitioner.Id,
            Use = NameUse.Official,
            Text = nome,
            Family = partes.Length > 1 ? partes[^1] : null,
            Given = partes.Length > 1 ? partes[..^1] : partes,
        });

        practitioner.Identifiers.Add(new PractitionerIdentifier
        {
            Id = Guid.CreateVersion7(),
            PractitionerId = practitioner.Id,
            System = MedicosMapper.SystemCpf,
            Value = cpf,
            Type = IdentifierTypeCode.Cpf,
            Use = IdentifierUse.Official,
        });

        var rqe = NormalizaOpcional(req.Rqe);
        if (rqe is not null)
        {
            practitioner.Identifiers.Add(new PractitionerIdentifier
            {
                Id = Guid.CreateVersion7(),
                PractitionerId = practitioner.Id,
                System = MedicosMapper.SystemRqe,
                Value = rqe,
                Type = IdentifierTypeCode.Other,
                Use = IdentifierUse.Secondary,
            });
        }

        practitioner.Qualifications.Add(new PractitionerQualification
        {
            Id = Guid.CreateVersion7(),
            PractitionerId = practitioner.Id,
            CouncilCode = MedicosMapper.CouncilCrm,
            CouncilNumber = crm,
            CouncilState = uf,
            SpecialtyName = NormalizaOpcional(req.Especialidade),
            PeriodEnd = req.ValidadeCrm,
        });

        if (!string.IsNullOrWhiteSpace(req.Telefone))
        {
            practitioner.Telecoms.Add(new PractitionerTelecom
            {
                Id = Guid.CreateVersion7(),
                PractitionerId = practitioner.Id,
                System = ContactPointSystem.Phone,
                Use = ContactPointUse.Work,
                Value = req.Telefone.Trim(),
                Rank = 1,
            });
        }

        if (req.Endereco is not null)
        {
            practitioner.Addresses.Add(new PractitionerAddress
            {
                Id = Guid.CreateVersion7(),
                PractitionerId = practitioner.Id,
                Use = AddressUse.Work,
                Type = AddressType.Both,
                Line1 = JuntaLinha1(req.Endereco.Logradouro, req.Endereco.Numero),
                Line2 = NormalizaOpcional(req.Endereco.Complemento),
                District = NormalizaOpcional(req.Endereco.Bairro),
                State = string.IsNullOrWhiteSpace(req.Endereco.Uf) ? null : req.Endereco.Uf.Trim().ToUpperInvariant(),
                PostalCode = NormalizarDigitos(req.Endereco.Cep),
                Country = "BRA",
                Text = NormalizaOpcional(req.Endereco.Cidade),
            });
        }

        return practitioner;
    }

    private static void SubstituirIdentifier(Practitioner p, string system, IdentifierTypeCode tipo, string? valor)
    {
        var atual = p.Identifiers.FirstOrDefault(i => i.System == system);
        if (valor is null)
        {
            if (atual is not null) p.Identifiers.Remove(atual);
            return;
        }
        if (atual is null)
        {
            p.Identifiers.Add(new PractitionerIdentifier
            {
                Id = Guid.CreateVersion7(),
                PractitionerId = p.Id,
                System = system,
                Value = valor,
                Type = tipo,
                Use = IdentifierUse.Secondary,
            });
        }
        else
        {
            atual.Value = valor;
        }
    }

    private static void SubstituirTelefone(Practitioner p, string? telefone)
    {
        var existentes = p.Telecoms.Where(t => t.System == ContactPointSystem.Phone).ToList();
        foreach (var t in existentes) p.Telecoms.Remove(t);
        if (string.IsNullOrWhiteSpace(telefone)) return;
        p.Telecoms.Add(new PractitionerTelecom
        {
            Id = Guid.CreateVersion7(),
            PractitionerId = p.Id,
            System = ContactPointSystem.Phone,
            Use = ContactPointUse.Work,
            Value = telefone.Trim(),
            Rank = 1,
        });
    }

    private static void SubstituirEndereco(Practitioner p, Common.Dtos.EnderecoDto? dto)
    {
        var existentes = p.Addresses.ToList();
        foreach (var a in existentes) p.Addresses.Remove(a);
        if (dto is null) return;
        p.Addresses.Add(new PractitionerAddress
        {
            Id = Guid.CreateVersion7(),
            PractitionerId = p.Id,
            Use = AddressUse.Work,
            Type = AddressType.Both,
            Line1 = JuntaLinha1(dto.Logradouro, dto.Numero),
            Line2 = NormalizaOpcional(dto.Complemento),
            District = NormalizaOpcional(dto.Bairro),
            State = string.IsNullOrWhiteSpace(dto.Uf) ? null : dto.Uf.Trim().ToUpperInvariant(),
            PostalCode = NormalizarDigitos(dto.Cep),
            Country = "BRA",
            Text = NormalizaOpcional(dto.Cidade),
        });
    }

    private static string? JuntaLinha1(string? logradouro, string? numero)
    {
        var l = (logradouro ?? string.Empty).Trim();
        var n = (numero ?? string.Empty).Trim();
        if (l.Length == 0 && n.Length == 0) return null;
        return n.Length == 0 ? l : $"{l}, {n}";
    }

    private static string NormalizarDigitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new([.. valor.Where(char.IsDigit)]);

    private static string? NormalizaOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string NormalizarEmail(string? email, string cpf) =>
        string.IsNullOrWhiteSpace(email)
            ? $"medico-{cpf}@local.smsmarica"
            : email.Trim().ToLowerInvariant();
}
