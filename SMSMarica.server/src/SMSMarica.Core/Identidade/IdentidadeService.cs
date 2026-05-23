using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Identidade;

public sealed class IdentidadeService(
    SmsMaricaDbContext db,
    IPasswordHasher<Usuario> hasher,
    ITokenService tokenService) : IIdentidadeService
{
    /// <summary>Hash de senha "PENDENTE" — bloqueia login até o admin definir a senha real.</summary>
    private const string SenhaHashPlaceholder = "PENDENTE_AUTH";

    private readonly SmsMaricaDbContext _db = db;
    private readonly IPasswordHasher<Usuario> _hasher = hasher;
    private readonly ITokenService _tokenService = tokenService;

    public async Task<LoginRespostaDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var usuario = await _db.Usuarios
            .Include(u => u.UsuariosPerfis)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (usuario is null || !usuario.Ativo || usuario.SenhaHash == SenhaHashPlaceholder)
        {
            throw new ValidacaoException("identidade.credenciais_invalidas", "Email ou senha inválidos.");
        }

        var verif = _hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, request.Senha ?? string.Empty);
        if (verif == PasswordVerificationResult.Failed)
        {
            throw new ValidacaoException("identidade.credenciais_invalidas", "Email ou senha inválidos.");
        }

        if (verif == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.SenhaHash = _hasher.HashPassword(usuario, request.Senha!);
        }

        usuario.UltimoAcessoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var (token, expira) = _tokenService.GerarToken(usuario);
        var resolvidas = await ObterPermissoesResolvidasAsync(usuario.Id, cancellationToken);
        return new LoginRespostaDto(token, expira, IdentidadeMapper.ParaDto(usuario), resolvidas.Resolvidas);
    }

    public async Task<PermissoesResolvidasDto> ObterPermissoesResolvidasAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var existe = await _db.Usuarios.AsNoTracking().AnyAsync(u => u.Id == usuarioId, cancellationToken);
        if (!existe) throw new NaoEncontradoException(nameof(Usuario), usuarioId);

        // Permissões herdadas: união por módulo das permissões dos perfis do usuário.
        var herdadasRaw = await _db.UsuariosPerfis.AsNoTracking()
            .Where(up => up.UsuarioId == usuarioId)
            .SelectMany(up => up.Perfil.Permissoes)
            .Select(pp => new { pp.Modulo, pp.Acoes })
            .ToListAsync(cancellationToken);

        var herdadas = herdadasRaw
            .GroupBy(p => p.Modulo)
            .Select(g => new PermissaoModuloDto(g.Key, g.Aggregate(AcoesPermissao.Nenhuma, (acc, x) => acc | x.Acoes)))
            .OrderBy(p => p.Modulo)
            .ToList();

        var overrides = await _db.PermissoesUsuario.AsNoTracking()
            .Where(p => p.UsuarioId == usuarioId)
            .OrderBy(p => p.Modulo)
            .Select(p => new PermissaoModuloDto(p.Modulo, p.Acoes))
            .ToListAsync(cancellationToken);

        // Resolvidas: união herdada + override por módulo.
        var resolvidas = herdadas
            .Concat(overrides)
            .GroupBy(p => p.Modulo)
            .Select(g => new PermissaoModuloDto(g.Key, g.Aggregate(AcoesPermissao.Nenhuma, (acc, x) => acc | x.Acoes)))
            .OrderBy(p => p.Modulo)
            .ToList();

        return new PermissoesResolvidasDto(herdadas, overrides, resolvidas);
    }

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
            .Include(x => x.UsuariosPerfis)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), id);
        return IdentidadeMapper.ParaDto(u);
    }

    public async Task<UsuarioDto?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        var cpfNormalizado = NormalizarDigitos(cpf ?? string.Empty);
        if (cpfNormalizado.Length != 11) return null;
        var u = await _db.Usuarios.AsNoTracking()
            .Include(x => x.UsuariosPerfis)
            .FirstOrDefaultAsync(x => x.Cpf == cpfNormalizado, cancellationToken);
        return u is null ? null : IdentidadeMapper.ParaDto(u);
    }

    public async Task<Guid> CadastrarAsync(CadastrarUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Usuarios.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflitoException("usuario.email_duplicado", "Já existe usuário com este email.");
        }

        var cpfNormalizado = string.IsNullOrWhiteSpace(request.Cpf) ? null : NormalizarDigitos(request.Cpf);
        if (cpfNormalizado is not null
            && await _db.Usuarios.AsNoTracking().AnyAsync(u => u.Cpf == cpfNormalizado, cancellationToken))
        {
            throw new ConflitoException("usuario.cpf_duplicado", "Já existe usuário com este CPF.");
        }

        var u = new Usuario
        {
            Id = Guid.CreateVersion7(),
            NomeCompleto = request.NomeCompleto.Trim(),
            Email = email,
            Cpf = cpfNormalizado,
            DataNascimento = request.DataNascimento,
            Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim(),
            Endereco = request.Endereco?.ParaEntidade(),
            FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64,
            SenhaHash = SenhaHashPlaceholder,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };

        if (!string.IsNullOrWhiteSpace(request.Senha))
        {
            u.SenhaHash = _hasher.HashPassword(u, request.Senha);
        }

        if (request.PerfilIds is { Count: > 0 })
        {
            await ValidarPerfisExistemAsync(request.PerfilIds, cancellationToken);
            foreach (var perfilId in request.PerfilIds.Distinct())
            {
                u.UsuariosPerfis.Add(new UsuarioPerfil { UsuarioId = u.Id, PerfilId = perfilId });
            }
        }

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
        u.DataNascimento = request.DataNascimento;
        u.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        u.Endereco = request.Endereco?.ParaEntidade();
        u.FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AtualizarMinhaContaAsync(Guid usuarioId, AtualizarMinhaContaRequest request, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), usuarioId);

        u.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        u.Endereco = request.Endereco?.ParaEntidade();
        u.FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64;

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

    public async Task AtualizarPerfisDoUsuarioAsync(Guid usuarioId, AtualizarPerfisDoUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios
            .Include(x => x.UsuariosPerfis)
            .FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), usuarioId);

        var alvo = (request.PerfilIds ?? []).Distinct().ToList();
        if (alvo.Count > 0)
        {
            await ValidarPerfisExistemAsync(alvo, cancellationToken);
        }

        // Sincroniza: remove os que saíram, adiciona os novos.
        var atuais = u.UsuariosPerfis.Select(up => up.PerfilId).ToHashSet();
        var alvoSet = alvo.ToHashSet();

        foreach (var up in u.UsuariosPerfis.Where(up => !alvoSet.Contains(up.PerfilId)).ToList())
        {
            u.UsuariosPerfis.Remove(up);
        }
        foreach (var pid in alvo.Where(pid => !atuais.Contains(pid)))
        {
            u.UsuariosPerfis.Add(new UsuarioPerfil { UsuarioId = usuarioId, PerfilId = pid });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AtualizarOverridesDoUsuarioAsync(Guid usuarioId, AtualizarOverridesDoUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        var existe = await _db.Usuarios.AsNoTracking().AnyAsync(u => u.Id == usuarioId, cancellationToken);
        if (!existe) throw new NaoEncontradoException(nameof(Usuario), usuarioId);

        // Estratégia simples: substitui todos os overrides do usuário.
        var existentes = await _db.PermissoesUsuario
            .Where(p => p.UsuarioId == usuarioId)
            .ToListAsync(cancellationToken);
        _db.PermissoesUsuario.RemoveRange(existentes);

        var novos = (request.Overrides ?? [])
            .Where(p => p.Acoes != AcoesPermissao.Nenhuma)
            .GroupBy(p => p.Modulo)
            .Select(g => new PermissaoUsuario
            {
                UsuarioId = usuarioId,
                Modulo = g.Key,
                Acoes = g.Aggregate(AcoesPermissao.Nenhuma, (acc, x) => acc | x.Acoes),
            });
        await _db.PermissoesUsuario.AddRangeAsync(novos, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AlterarSenhaAsync(Guid usuarioId, AlterarSenhaRequest request, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), usuarioId);

        if (string.IsNullOrWhiteSpace(request.SenhaNova) || request.SenhaNova.Length < 8)
        {
            throw new ValidacaoException("usuario.senha_invalida", "Senha deve ter pelo menos 8 caracteres.");
        }

        u.SenhaHash = _hasher.HashPassword(u, request.SenhaNova);
        u.DeveTrocarSenha = request.DeveTrocarNoProximoLogin;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SenhaGeradaDto> GerarNovaSenhaAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), usuarioId);

        var senha = GerarSenhaAleatoria(12);
        u.SenhaHash = _hasher.HashPassword(u, senha);
        u.DeveTrocarSenha = true; // sempre força troca quando admin gerou.
        await _db.SaveChangesAsync(cancellationToken);
        return new SenhaGeradaDto(senha, true);
    }

    public async Task AlterarMinhaSenhaAsync(Guid usuarioId, AlterarMinhaSenhaRequest request, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), usuarioId);

        if (string.IsNullOrWhiteSpace(request.SenhaNova) || request.SenhaNova.Length < 8)
        {
            throw new ValidacaoException("usuario.senha_invalida", "Senha deve ter pelo menos 8 caracteres.");
        }

        var verif = _hasher.VerifyHashedPassword(u, u.SenhaHash, request.SenhaAtual ?? string.Empty);
        if (verif == PasswordVerificationResult.Failed)
        {
            throw new ValidacaoException("identidade.senha_atual_invalida", "Senha atual incorreta.");
        }

        u.SenhaHash = _hasher.HashPassword(u, request.SenhaNova);
        u.DeveTrocarSenha = false;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Gera senha forte com ao menos 1 caractere de cada categoria (maiúscula,
    /// minúscula, dígito, símbolo). Evita ambiguidades (I/l/1, O/0).
    /// </summary>
    private static string GerarSenhaAleatoria(int comprimento)
    {
        const string maiusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string minusculas = "abcdefghijkmnpqrstuvwxyz";
        const string digitos = "23456789";
        const string especiais = "!@#$%&*?";
        const string todos = maiusculas + minusculas + digitos + especiais;

        var chars = new char[comprimento];
        chars[0] = maiusculas[RandomNumberGenerator.GetInt32(maiusculas.Length)];
        chars[1] = minusculas[RandomNumberGenerator.GetInt32(minusculas.Length)];
        chars[2] = digitos[RandomNumberGenerator.GetInt32(digitos.Length)];
        chars[3] = especiais[RandomNumberGenerator.GetInt32(especiais.Length)];
        for (var i = 4; i < comprimento; i++)
        {
            chars[i] = todos[RandomNumberGenerator.GetInt32(todos.Length)];
        }
        // Fisher-Yates para não denunciar as posições fixas.
        for (var i = comprimento - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars);
    }

    private async Task ValidarPerfisExistemAsync(IReadOnlyCollection<Guid> perfilIds, CancellationToken cancellationToken)
    {
        var encontrados = await _db.Perfis.AsNoTracking()
            .Where(p => perfilIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        var faltando = perfilIds.Except(encontrados).ToList();
        if (faltando.Count > 0)
        {
            throw new ValidacaoException(
                "usuario.perfis_invalidos",
                $"Perfis não encontrados: {string.Join(", ", faltando)}");
        }
    }

    private static string NormalizarDigitos(string valor) =>
        new([.. valor.Where(char.IsDigit)]);
}
