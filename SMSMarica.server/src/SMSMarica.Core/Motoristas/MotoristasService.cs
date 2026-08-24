using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Motoristas.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Motoristas;

public sealed class MotoristasService(SmsMaisDbContext db, IUsuarioAtualAccessor atual) : IMotoristasService
{
    private readonly SmsMaisDbContext _db = db;
    private readonly IUsuarioAtualAccessor _atual = atual;

    private const string SenhaHashPlaceholder = "PENDENTE_AUTH";

    public async Task<IReadOnlyList<MotoristaListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var motoristas = await _db.Motoristas.AsNoTracking()
            .Include(m => m.Usuario)
            .Where(m => m.ExcluidoEm == null)
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

        if (await _db.Motoristas.AsNoTracking().AnyAsync(x => x.Cnh == cnh && x.ExcluidoEm == null, cancellationToken))
        {
            throw new ConflitoException("motorista.cnh_duplicada", "Já existe motorista com esta CNH.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;
        var usuario = new Usuario
        {
            Id = Guid.CreateVersion7(),
            NomeCompleto = request.NomeCompleto.Trim(),
            Cpf = cpf,
            DataNascimento = request.DataNascimento,
            Email = NormalizarEmail(request.Email, cpf),
            Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim(),
            Endereco = request.Endereco?.ParaEntidade(),
            FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64,
            SenhaHash = SenhaHashPlaceholder,
            DeveTrocarSenha = true,
            Ativo = true,
            CriadoEm = agora,
            CriadoPor = atualId,
        };

        var motorista = new Motorista
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuario.Id,
            Cnh = cnh,
            CriadoEm = agora,
            CriadoPor = atualId,
        };

        _db.Usuarios.Add(usuario);
        _db.Motoristas.Add(motorista);
        await _db.SaveChangesAsync(cancellationToken);
        return motorista.Id;
    }

    public async Task<Guid> PromoverAsync(PromoverMotoristaRequest request, CancellationToken cancellationToken = default)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Motorista)
            .FirstOrDefaultAsync(u => u.Id == request.UsuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), request.UsuarioId);

        var papelAtual = DetectarPapel(usuario);
        if (papelAtual is not null)
        {
            throw new ConflitoException(
                "motorista.usuario_ja_tem_papel",
                $"Usuário já tem papel '{papelAtual}'. Elimine o papel atual antes de promover.");
        }

        if (string.IsNullOrWhiteSpace(usuario.Cpf))
        {
            throw new ConflitoException(
                "motorista.usuario_sem_cpf",
                "Usuário precisa ter CPF cadastrado para virar motorista.");
        }

        var cnh = request.Cnh.Trim();
        if (await _db.Motoristas.AsNoTracking().AnyAsync(x => x.Cnh == cnh && x.ExcluidoEm == null, cancellationToken))
        {
            throw new ConflitoException("motorista.cnh_duplicada", "Já existe motorista com esta CNH.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;
        var motorista = new Motorista
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuario.Id,
            Cnh = cnh,
            CriadoEm = agora,
            CriadoPor = atualId,
        };

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

        if (m.ExcluidoEm is not null)
        {
            throw new ConflitoException("motorista.excluido", "Motorista excluído não pode ser editado.");
        }

        var cnh = request.Cnh.Trim();
        if (cnh != m.Cnh &&
            await _db.Motoristas.AsNoTracking().AnyAsync(x => x.Cnh == cnh && x.Id != id && x.ExcluidoEm == null, cancellationToken))
        {
            throw new ConflitoException("motorista.cnh_duplicada", "Já existe motorista com esta CNH.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;

        m.Cnh = cnh;
        m.AtualizadoEm = agora;
        m.AtualizadoPor = atualId;

        // Nome e CPF do Usuario são imutáveis — não tocamos aqui.
        m.Usuario.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        m.Usuario.Endereco = request.Endereco?.ParaEntidade();
        m.Usuario.FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64;
        m.Usuario.AtualizadoEm = agora;
        m.Usuario.AtualizadoPor = atualId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var m = await _db.Motoristas
            .Include(x => x.Usuario)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Motorista), id);

        if (m.ExcluidoEm is not null)
        {
            throw new ConflitoException("motorista.ja_excluido", "Motorista já foi excluído.");
        }

        var agora = DateTime.UtcNow;
        var atualId = _atual.UsuarioId;

        m.ExcluidoEm = agora;
        m.ExcluidoPor = atualId;

        // Exclusão de papel cascateia para o Usuario: a pessoa sai do sistema.
        m.Usuario.ExcluidoEm = agora;
        m.Usuario.ExcluidoPor = atualId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string? DetectarPapel(Usuario u)
    {
        // Paciente e Médico migraram para o hub FHIR — Usuário só tem papel Motorista.
        if (u.Motorista is not null) return "Motorista";
        return null;
    }

    private static string NormalizarDigitos(string valor) =>
        new([.. valor.Where(char.IsDigit)]);

    private static string NormalizarEmail(string? email, string cpf) =>
        string.IsNullOrWhiteSpace(email)
            ? $"motorista-{cpf}@local.smsmarica"
            : email.Trim().ToLowerInvariant();
}
