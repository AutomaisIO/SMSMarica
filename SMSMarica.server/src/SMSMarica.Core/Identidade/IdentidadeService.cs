using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Identidade;

public sealed class IdentidadeService(SmsMaricaDbContext db) : IIdentidadeService
{
    /// <summary>
    /// Placeholder temporário do hash de senha. Módulo de auth real substituirá
    /// este campo ao implementar endpoint POST /auth/definir-senha ou equivalente.
    /// </summary>
    private const string SenhaHashPlaceholder = "PENDENTE_AUTH";

    private readonly SmsMaricaDbContext _db = db;

    public async Task<IReadOnlyList<UsuarioListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var usuarios = await _db.Usuarios.AsNoTracking()
            .OrderBy(u => u.NomeCompleto)
            .ToListAsync(cancellationToken);
        return [.. usuarios.Select(IdentidadeMapper.ParaListItem)];
    }

    public async Task<UsuarioDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), id);
        return IdentidadeMapper.ParaDto(u);
    }

    public async Task<Guid> CadastrarAsync(CadastrarUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Usuarios.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflitoException("usuario.email_duplicado", "Já existe usuário com este email.");
        }

        var u = new Usuario
        {
            Id = Guid.CreateVersion7(),
            NomeCompleto = request.NomeCompleto.Trim(),
            Email = email,
            Cpf = string.IsNullOrWhiteSpace(request.Cpf) ? null : NormalizarDigitos(request.Cpf),
            Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim(),
            Endereco = request.Endereco?.ParaEntidade(),
            Perfil = request.Perfil,
            SenhaHash = SenhaHashPlaceholder,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };

        _db.Usuarios.Add(u);
        await _db.SaveChangesAsync(cancellationToken);
        return u.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), id);

        u.NomeCompleto = request.NomeCompleto.Trim();
        u.Cpf = string.IsNullOrWhiteSpace(request.Cpf) ? null : NormalizarDigitos(request.Cpf);
        u.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        u.Endereco = request.Endereco?.ParaEntidade();
        u.Perfil = request.Perfil;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), id);

        if (!u.Ativo)
        {
            throw new ConflitoException("usuario.ja_inativo", "Usuário já está inativo.");
        }

        u.Ativo = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizarDigitos(string valor) =>
        new([.. valor.Where(char.IsDigit)]);
}
