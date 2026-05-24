using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Motoristas.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Motoristas;

public sealed class MotoristasService(SmsMaricaDbContext db) : IMotoristasService
{
    private readonly SmsMaricaDbContext _db = db;

    private const string SenhaHashPlaceholder = "PENDENTE_AUTH";

    public async Task<IReadOnlyList<MotoristaListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var motoristas = await _db.Motoristas.AsNoTracking()
            .Include(m => m.Usuario)
            .OrderBy(m => m.Usuario.NomeCompleto)
            .ToListAsync(cancellationToken);
        return [.. motoristas.Select(MotoristasMapper.ParaListItem)];
    }

    public async Task<MotoristaDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var m = await _db.Motoristas.AsNoTracking()
            .Include(x => x.Usuario)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Motorista), id);
        return MotoristasMapper.ParaDto(m);
    }

    public async Task<Guid> CadastrarAsync(CadastrarMotoristaRequest request, CancellationToken cancellationToken = default)
    {
        var cpf = NormalizarDigitos(request.Cpf);
        var cnh = request.Cnh.Trim();

        var usuarioExistente = await _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Cpf == cpf, cancellationToken);
        if (usuarioExistente is not null)
        {
            throw new ConflitoException(
                "motorista.cpf_ja_cadastrado",
                $"CPF já cadastrado como usuário '{usuarioExistente.NomeCompleto}'. Use /motoristas/promover.");
        }

        if (await _db.Motoristas.AsNoTracking().AnyAsync(x => x.Cnh == cnh, cancellationToken))
        {
            throw new ConflitoException("motorista.cnh_duplicada", "Já existe motorista com esta CNH.");
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
            TipoPapel = TipoPapel.Motorista,
            Ativo = true,
            CriadoEm = agora,
        };

        var motorista = new Motorista
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuario.Id,
            Cnh = cnh,
            Ativo = true,
            CriadoEm = agora,
        };

        _db.Usuarios.Add(usuario);
        _db.Motoristas.Add(motorista);
        await _db.SaveChangesAsync(cancellationToken);
        return motorista.Id;
    }

    public async Task<Guid> PromoverAsync(PromoverMotoristaRequest request, CancellationToken cancellationToken = default)
    {
        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == request.UsuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), request.UsuarioId);

        if (usuario.TipoPapel is not null)
        {
            throw new ConflitoException(
                "motorista.usuario_ja_tem_papel",
                $"Usuário já tem papel '{usuario.TipoPapel}'. Elimine o papel atual antes de promover.");
        }

        if (string.IsNullOrWhiteSpace(usuario.Cpf))
        {
            throw new ConflitoException(
                "motorista.usuario_sem_cpf",
                "Usuário precisa ter CPF cadastrado para virar motorista.");
        }

        var cnh = request.Cnh.Trim();
        if (await _db.Motoristas.AsNoTracking().AnyAsync(x => x.Cnh == cnh, cancellationToken))
        {
            throw new ConflitoException("motorista.cnh_duplicada", "Já existe motorista com esta CNH.");
        }

        var agora = DateTime.UtcNow;
        var motorista = new Motorista
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuario.Id,
            Cnh = cnh,
            Ativo = true,
            CriadoEm = agora,
        };

        usuario.TipoPapel = TipoPapel.Motorista;
        _db.Motoristas.Add(motorista);
        await _db.SaveChangesAsync(cancellationToken);
        return motorista.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarMotoristaRequest request, CancellationToken cancellationToken = default)
    {
        var m = await _db.Motoristas
            .Include(x => x.Usuario)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Motorista), id);

        var cnh = request.Cnh.Trim();
        if (cnh != m.Cnh &&
            await _db.Motoristas.AsNoTracking().AnyAsync(x => x.Cnh == cnh && x.Id != id, cancellationToken))
        {
            throw new ConflitoException("motorista.cnh_duplicada", "Já existe motorista com esta CNH.");
        }

        m.Cnh = cnh;
        m.AtualizadoEm = DateTime.UtcNow;

        // Nome e CPF do Usuario são imutáveis — não tocamos aqui.
        m.Usuario.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        m.Usuario.Endereco = request.Endereco?.ParaEntidade();
        m.Usuario.FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var m = await _db.Motoristas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Motorista), id);

        if (!m.Ativo)
        {
            throw new ConflitoException("motorista.ja_inativo", "Motorista já está inativo.");
        }

        m.Ativo = false;
        m.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizarDigitos(string valor) =>
        new([.. valor.Where(char.IsDigit)]);

    private static string NormalizarEmail(string? email, string cpf) =>
        string.IsNullOrWhiteSpace(email)
            ? $"motorista-{cpf}@local.smsmarica"
            : email.Trim().ToLowerInvariant();
}
