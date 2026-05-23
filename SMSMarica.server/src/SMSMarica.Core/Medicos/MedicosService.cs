using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Medicos.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Medicos;

public sealed class MedicosService(SmsMaricaDbContext db) : IMedicosService
{
    private readonly SmsMaricaDbContext _db = db;

    private const string SenhaHashPlaceholder = "PENDENTE_AUTH";

    public async Task<IReadOnlyList<MedicoListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var medicos = await _db.Medicos.AsNoTracking()
            .Include(m => m.Usuario)
            .OrderBy(m => m.Usuario.NomeCompleto)
            .ToListAsync(cancellationToken);
        return [.. medicos.Select(MedicosMapper.ParaListItem)];
    }

    public async Task<MedicoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var m = await _db.Medicos.AsNoTracking()
            .Include(x => x.Usuario)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Medico), id);
        return MedicosMapper.ParaDto(m);
    }

    public async Task<Guid> CadastrarAsync(CadastrarMedicoRequest request, CancellationToken cancellationToken = default)
    {
        var cpf = NormalizarDigitos(request.Cpf);
        var crm = NormalizarDigitos(request.Crm);
        var uf = (request.UfCrm ?? string.Empty).Trim().ToUpperInvariant();

        var usuarioExistente = await _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Cpf == cpf, cancellationToken);
        if (usuarioExistente is not null)
        {
            throw new ConflitoException(
                "medico.cpf_ja_cadastrado",
                $"CPF já cadastrado como usuário '{usuarioExistente.NomeCompleto}'. Use /medicos/promover.");
        }

        if (await _db.Medicos.AsNoTracking().AnyAsync(x => x.Crm == crm && x.UfCrm == uf, cancellationToken))
        {
            throw new ConflitoException("medico.crm_duplicado", $"Já existe médico com CRM {crm}/{uf}.");
        }

        var agora = DateTime.UtcNow;
        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            NomeCompleto = request.NomeCompleto.Trim(),
            Cpf = cpf,
            Email = NormalizarEmail(request.Email, cpf),
            Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim(),
            Endereco = request.Endereco?.ParaEntidade(),
            FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64,
            SenhaHash = SenhaHashPlaceholder,
            DeveTrocarSenha = true,
            TipoPapel = TipoPapel.Medico,
            Ativo = true,
            CriadoEm = agora,
        };

        var medico = new Medico
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuario.Id,
            Crm = crm,
            UfCrm = uf,
            Especialidade = NormalizaOpcional(request.Especialidade),
            Rqe = NormalizaOpcional(request.Rqe),
            ValidadeCrm = request.ValidadeCrm,
            Ativo = true,
            CriadoEm = agora,
        };

        _db.Usuarios.Add(usuario);
        _db.Medicos.Add(medico);
        await _db.SaveChangesAsync(cancellationToken);
        return medico.Id;
    }

    public async Task<Guid> PromoverAsync(PromoverMedicoRequest request, CancellationToken cancellationToken = default)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == request.UsuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), request.UsuarioId);

        if (usuario.TipoPapel is not null)
        {
            throw new ConflitoException(
                "medico.usuario_ja_tem_papel",
                $"Usuário já tem papel '{usuario.TipoPapel}'. Elimine o papel atual antes de promover.");
        }

        if (string.IsNullOrWhiteSpace(usuario.Cpf))
        {
            throw new ConflitoException(
                "medico.usuario_sem_cpf",
                "Usuário precisa ter CPF cadastrado para virar médico.");
        }

        var crm = NormalizarDigitos(request.Crm);
        var uf = (request.UfCrm ?? string.Empty).Trim().ToUpperInvariant();

        if (await _db.Medicos.AsNoTracking().AnyAsync(x => x.Crm == crm && x.UfCrm == uf, cancellationToken))
        {
            throw new ConflitoException("medico.crm_duplicado", $"Já existe médico com CRM {crm}/{uf}.");
        }

        var agora = DateTime.UtcNow;
        var medico = new Medico
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuario.Id,
            Crm = crm,
            UfCrm = uf,
            Especialidade = NormalizaOpcional(request.Especialidade),
            Rqe = NormalizaOpcional(request.Rqe),
            ValidadeCrm = request.ValidadeCrm,
            Ativo = true,
            CriadoEm = agora,
        };

        usuario.TipoPapel = TipoPapel.Medico;
        _db.Medicos.Add(medico);
        await _db.SaveChangesAsync(cancellationToken);
        return medico.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarMedicoRequest request, CancellationToken cancellationToken = default)
    {
        var m = await _db.Medicos
            .Include(x => x.Usuario)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Medico), id);

        var crm = NormalizarDigitos(request.Crm);
        var uf = (request.UfCrm ?? string.Empty).Trim().ToUpperInvariant();
        if ((crm != m.Crm || uf != m.UfCrm) &&
            await _db.Medicos.AsNoTracking().AnyAsync(x => x.Crm == crm && x.UfCrm == uf && x.Id != id, cancellationToken))
        {
            throw new ConflitoException("medico.crm_duplicado", $"Já existe médico com CRM {crm}/{uf}.");
        }

        m.Crm = crm;
        m.UfCrm = uf;
        m.Especialidade = NormalizaOpcional(request.Especialidade);
        m.Rqe = NormalizaOpcional(request.Rqe);
        m.ValidadeCrm = request.ValidadeCrm;
        m.AtualizadoEm = DateTime.UtcNow;

        m.Usuario.NomeCompleto = request.NomeCompleto.Trim();
        m.Usuario.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        m.Usuario.Endereco = request.Endereco?.ParaEntidade();
        m.Usuario.FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var m = await _db.Medicos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Medico), id);

        if (!m.Ativo)
        {
            throw new ConflitoException("medico.ja_inativo", "Médico já está inativo.");
        }

        m.Ativo = false;
        m.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizarDigitos(string valor) =>
        new([.. valor.Where(char.IsDigit)]);

    private static string? NormalizaOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string NormalizarEmail(string? email, string cpf) =>
        string.IsNullOrWhiteSpace(email)
            ? $"medico-{cpf}@local.smsmarica"
            : email.Trim().ToLowerInvariant();
}
