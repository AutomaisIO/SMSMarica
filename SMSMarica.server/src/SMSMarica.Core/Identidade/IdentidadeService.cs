using System.Security.Cryptography;
using System.Text.Json;
using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade.Dtos;
using SMSMarica.Core.Medicos;
using SMSMarica.Core.Medicos.Fhir;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Identidade;

public sealed class IdentidadeService(
    SmsMaricaDbContext db,
    IPasswordHasher<Usuario> hasher,
    ITokenService tokenService,
    IUsuarioAtualAccessor atual,
    IPractitionerFhirClient practitioner,
    ILogger<IdentidadeService> logger) : IIdentidadeService
{
    /// <summary>Hash de senha "PENDENTE" — bloqueia login até o admin definir a senha real.</summary>
    private const string SenhaHashPlaceholder = "PENDENTE_AUTH";

    private readonly SmsMaricaDbContext _db = db;
    private readonly IPasswordHasher<Usuario> _hasher = hasher;
    private readonly ITokenService _tokenService = tokenService;
    private readonly IUsuarioAtualAccessor _atual = atual;
    private readonly IPractitionerFhirClient _practitioner = practitioner;
    private readonly ILogger<IdentidadeService> _logger = logger;

    public async Task<LoginRespostaDto> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // Identificador único de login: aceita e-mail, CPF OU nome de usuário (tanto faz).
        // O nome de usuário não diferencia maiúsculas — "Bernardo" e "bernardo" entram igual.
        var ident = (request.Email ?? string.Empty).Trim();
        var emailCand = ident.ToLowerInvariant();
        var digitos = NormalizarDigitos(ident);
        var cpfCand = digitos.Length == 11 ? digitos : null;
        var loginCand = LoginValido(ident) ? emailCand : null;

        var usuario = await _db.Usuarios
            .Include(u => u.UsuariosPerfis)
            .FirstOrDefaultAsync(
                u => (u.Email != null && u.Email == emailCand)
                     || (cpfCand != null && u.Cpf == cpfCand)
                     || (loginCand != null && u.Login != null && u.Login.ToLower() == loginCand),
                cancellationToken);

        if (usuario is null || !usuario.Ativo || usuario.SenhaHash == SenhaHashPlaceholder)
        {
            throw new ValidacaoException("identidade.credenciais_invalidas", "Usuário/e-mail/CPF ou senha inválidos.");
        }

        var verif = _hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, request.Senha ?? string.Empty);
        if (verif == PasswordVerificationResult.Failed)
        {
            throw new ValidacaoException("identidade.credenciais_invalidas", "Usuário/e-mail/CPF ou senha inválidos.");
        }

        if (verif == PasswordVerificationResult.SuccessRehashNeeded)
        {
            usuario.SenhaHash = _hasher.HashPassword(usuario, request.Senha!);
        }

        usuario.UltimoAcessoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var (token, expira) = _tokenService.GerarToken(usuario);
        var resolvidas = await ObterPermissoesResolvidasAsync(usuario.Id, cancellationToken);
        var medico = await ResolverMedicoAsync(usuario.Cpf, cancellationToken);
        // Acesso global: vínculo implícito a todas as unidades ativas, sem precisar
        // de linhas em usuario_unidade (nenhuma vira "principal" — entra vendo tudo).
        var unidades = usuario.AcessoGlobal
            ? await _db.Unidades.AsNoTracking()
                .Where(u => u.Ativo)
                .OrderBy(u => u.Nome)
                .Select(u => new UnidadeVinculadaDto(u.Id, u.Nome, false))
                .ToListAsync(cancellationToken)
            : await _db.UsuarioUnidades.AsNoTracking()
                .Where(v => v.UsuarioId == usuario.Id && v.Unidade!.Ativo)
                .OrderByDescending(v => v.Principal)
                .ThenBy(v => v.Unidade!.Nome)
                .Select(v => new UnidadeVinculadaDto(v.UnidadeId, v.Unidade!.Nome, v.Principal))
                .ToListAsync(cancellationToken);
        return new LoginRespostaDto(token, expira, IdentidadeMapper.ParaDto(usuario, medico: medico), resolvidas.Resolvidas, unidades);
    }

    /// <summary>
    /// Formato do nome de usuário: 3 a 40 caracteres, letras/números/ponto/hífen/underscore.
    /// NUNCA só números (colidiria com o CPF) nem com "@" (colidiria com o e-mail): os três
    /// identificadores entram pelo MESMO campo na tela de login, e a ambiguidade tornaria uma
    /// das formas inalcançável.
    /// </summary>
    private static bool LoginValido(string valor) =>
        valor.Length is >= 3 and <= 40
        && valor.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
        && !valor.All(char.IsAsciiDigit);

    /// <summary>Valida formato e unicidade (ignorando maiúsculas). Vazio = sem login.</summary>
    private async Task<string?> ResolverLoginAsync(string? bruto, Guid? idAtual, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bruto)) return null;

        var login = bruto.Trim();
        if (!LoginValido(login))
        {
            throw new ValidacaoException(
                "usuario.login_invalido",
                "O nome de usuário deve ter de 3 a 40 caracteres (letras, números, ponto, hífen "
                + "ou _), não pode ser só números nem conter '@'.");
        }

        var chave = login.ToLowerInvariant();
        var duplicado = await _db.Usuarios.AsNoTracking().AnyAsync(
            u => u.Login != null && u.Login.ToLower() == chave && (idAtual == null || u.Id != idAtual),
            ct);
        if (duplicado)
        {
            throw new ConflitoException("usuario.login_duplicado", "Já existe usuário com este nome de usuário.");
        }
        return login;
    }

    /// <summary>
    /// Conceder acesso global é elevar privilégio: só quem já o tem pode dar a outro. Sem essa
    /// trava, qualquer perfil com edição de usuários viraria caminho para ver todas as unidades.
    /// </summary>
    private async Task GarantirPodeConcederAcessoGlobalAsync(CancellationToken ct)
    {
        if (!await AcessoGlobalUsuario.TemAsync(_db, _atual.UsuarioId, ct))
        {
            throw new ValidacaoException(
                "usuario.acesso_global_negado",
                "Só um usuário com acesso global pode conceder ou revogar acesso global.");
        }
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

    public async Task<IReadOnlyList<UsuarioListItemDto>> ListarAsync(
        FiltroUsuariosDto? filtro = null, CancellationToken cancellationToken = default)
    {
        filtro ??= new FiltroUsuariosDto();

        // ADR-0006: lista só usuários sem papel (médicos/motoristas/pacientes têm tela própria)
        // e não excluídos. Ativo=false continua aparecendo — é estado temporário, não exclusão.
        var query = _db.Usuarios.AsNoTracking()
            .Where(u => u.ExcluidoEm == null && u.Motorista == null);

        if (!string.IsNullOrWhiteSpace(filtro.Busca))
        {
            var termo = filtro.Busca.Trim();
            var padraoNome = $"%{termo}%";
            var digitos = NormalizarDigitos(termo);
            query = query.Where(u =>
                EF.Functions.ILike(u.NomeCompleto, padraoNome)
                || (digitos.Length > 0 && u.Cpf != null && u.Cpf.Contains(digitos)));
        }

        if (filtro.UnidadeId is { } unidadeId)
        {
            query = query.Where(u => _db.UsuarioUnidades.Any(v => v.UsuarioId == u.Id && v.UnidadeId == unidadeId));
        }

        // A lista tende a crescer — limita (clamp 1..500, default 50).
        var limite = filtro.Limite is <= 0 or > 500 ? 50 : filtro.Limite;
        var usuarios = await query
            .OrderBy(u => u.NomeCompleto)
            .Take(limite)
            .ToListAsync(cancellationToken);
        return [.. usuarios.Select(IdentidadeMapper.ParaListItem)];
    }

    public async Task<UsuarioDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios.AsNoTracking()
            .Include(x => x.UsuariosPerfis)
            .Include(x => x.Motorista)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), id);
        var medico = await ResolverMedicoAsync(u.Cpf, cancellationToken);
        return IdentidadeMapper.ParaDto(u, medico: medico);
    }

    public async Task<UsuarioDto?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        var cpfNormalizado = NormalizarDigitos(cpf ?? string.Empty);
        if (cpfNormalizado.Length != 11) return null;
        var u = await _db.Usuarios.AsNoTracking()
            .Include(x => x.UsuariosPerfis)
            .Include(x => x.Motorista)
            .FirstOrDefaultAsync(x => x.Cpf == cpfNormalizado, cancellationToken);
        if (u is null) return null;
        var medico = await ResolverMedicoAsync(u.Cpf, cancellationToken);
        return IdentidadeMapper.ParaDto(u, medico: medico);
    }

    /// <summary>
    /// Resolve o vínculo médico do usuário buscando um Practitioner com o mesmo CPF no
    /// hub FHIR. Médico não é mais linha em <c>usuario</c> (ADR-0010), então o papel
    /// "Medico" é resolvido aqui. Falha do hub NÃO quebra login/consulta — apenas
    /// devolve null (sem papel médico).
    /// </summary>
    private async Task<VinculoMedico?> ResolverMedicoAsync(string? cpf, CancellationToken cancellationToken)
    {
        var cpfDigits = NormalizarDigitos(cpf ?? string.Empty);
        if (cpfDigits.Length != 11) return null;
        try
        {
            var bundle = await _practitioner.BuscarAsync(identifier: cpfDigits, ct: cancellationToken);
            var p = bundle.Entry.Select(e => e.Resource).OfType<Practitioner>().FirstOrDefault();
            if (p is null) return null;
            var dto = MedicoFhirMapper.ParaDto(p);
            return new VinculoMedico(dto.Conselho, dto.Registro, dto.UfConselho);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao resolver vínculo médico (FHIR) por CPF — papel médico ignorado.");
            return null;
        }
    }

    public async Task<Guid> CadastrarAsync(CadastrarUsuarioRequest request, CancellationToken cancellationToken = default)
    {
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim().ToLowerInvariant();
        var cpfNormalizado = string.IsNullOrWhiteSpace(request.Cpf) ? null : NormalizarDigitos(request.Cpf);

        // Sem e-mail e sem CPF não há como o usuário fazer login.
        if (email is null && cpfNormalizado is null)
        {
            throw new ValidacaoException("usuario.sem_identificador", "Informe e-mail ou CPF para o login.");
        }

        if (email is not null
            && await _db.Usuarios.AsNoTracking().AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw new ConflitoException("usuario.email_duplicado", "Já existe usuário com este email.");
        }

        if (cpfNormalizado is not null
            && await _db.Usuarios.AsNoTracking().AnyAsync(u => u.Cpf == cpfNormalizado, cancellationToken))
        {
            throw new ConflitoException("usuario.cpf_duplicado", "Já existe usuário com este CPF.");
        }

        var login = await ResolverLoginAsync(request.Login, idAtual: null, cancellationToken);

        var u = new Usuario
        {
            Id = Guid.CreateVersion7(),
            NomeCompleto = request.NomeCompleto.Trim(),
            Email = email,
            Login = login,
            Cpf = cpfNormalizado,
            DataNascimento = request.DataNascimento,
            Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim(),
            Endereco = request.Endereco?.ParaEntidade(),
            FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64,
            SenhaHash = SenhaHashPlaceholder,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _atual.UsuarioId,
        };

        if (!string.IsNullOrWhiteSpace(request.Senha))
        {
            u.SenhaHash = _hasher.HashPassword(u, request.Senha);
            // Só faz sentido exigir troca quando há senha real (sem senha o login já fica bloqueado).
            u.DeveTrocarSenha = request.DeveTrocarSenha;
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

        // E-mail é editável/inserível (médicos importados vêm sem e-mail). Em
        // branco = não mexe no atual. Quando informado, normaliza e valida unicidade.
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var novoEmail = request.Email.Trim().ToLowerInvariant();
            if (novoEmail != u.Email
                && await _db.Usuarios.AsNoTracking()
                    .AnyAsync(x => x.Email == novoEmail && x.Id != id, cancellationToken))
            {
                throw new ConflitoException("usuario.email_duplicado", "Já existe usuário com este email.");
            }
            u.Email = novoEmail;
        }

        // Mesma convenção do e-mail: em branco não mexe no atual.
        if (!string.IsNullOrWhiteSpace(request.Login))
        {
            u.Login = await ResolverLoginAsync(request.Login, id, cancellationToken);
        }

        if (request.AcessoGlobal is bool acessoGlobal && acessoGlobal != u.AcessoGlobal)
        {
            await GarantirPodeConcederAcessoGlobalAsync(cancellationToken);
            u.AcessoGlobal = acessoGlobal;
        }

        u.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        u.Endereco = request.Endereco?.ParaEntidade();
        u.FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64;
        u.AtualizadoEm = DateTime.UtcNow;
        u.AtualizadoPor = _atual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AtualizarMinhaContaAsync(Guid usuarioId, AtualizarMinhaContaRequest request, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), usuarioId);

        u.Telefone = string.IsNullOrWhiteSpace(request.Telefone) ? null : request.Telefone.Trim();
        u.Endereco = request.Endereco?.ParaEntidade();
        u.FotoBase64 = string.IsNullOrWhiteSpace(request.FotoBase64) ? null : request.FotoBase64;
        u.AtualizadoEm = DateTime.UtcNow;
        u.AtualizadoPor = usuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static readonly JsonSerializerOptions PreferenciasJson = new(JsonSerializerDefaults.Web);

    public async Task<PreferenciasUiDto> ObterPreferenciasUiAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var json = await _db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => u.PreferenciasUi)
            .FirstOrDefaultAsync(cancellationToken);

        return Desserializar(json);
    }

    private static PreferenciasUiDto Desserializar(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new PreferenciasUiDto(new());
        try
        {
            return JsonSerializer.Deserialize<PreferenciasUiDto>(json, PreferenciasJson) ?? new PreferenciasUiDto(new());
        }
        catch (JsonException)
        {
            return new PreferenciasUiDto(new());
        }
    }

    public async Task SalvarPreferenciasUiAsync(Guid usuarioId, PreferenciasUiDto preferencias, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios.FirstOrDefaultAsync(x => x.Id == usuarioId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), usuarioId);

        // Merge no servidor: cada campo não enviado (nulo) preserva o valor atual. Assim telas
        // independentes (menu, chat) gravam só a sua parte sem sobrescrever a das outras.
        var atual = Desserializar(u.PreferenciasUi);
        var mesclado = new PreferenciasUiDto(
            preferencias.MenuDefaults ?? atual.MenuDefaults ?? new(),
            preferencias.AlturaComposerChat ?? atual.AlturaComposerChat,
            preferencias.EnviarComEnter ?? atual.EnviarComEnter,
            preferencias.VerComoSolicitante ?? atual.VerComoSolicitante,
            preferencias.LargurasTabela ?? atual.LargurasTabela,
            preferencias.ExamesModalidades ?? atual.ExamesModalidades);
        u.PreferenciasUi = JsonSerializer.Serialize(mesclado, PreferenciasJson);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DesativarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var u = await _db.Usuarios
            .Include(x => x.Motorista)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Usuario), id);

        if (u.ExcluidoEm is not null)
        {
            throw new ConflitoException("usuario.ja_excluido", "Usuário já foi excluído.");
        }

        // Bloquear quando tem papel Motorista — exclusão pela tela do papel
        // (cascateia para o Usuario). Paciente/Médico migraram para o hub FHIR.
        if (u.Motorista is not null)
        {
            throw new ConflitoException(
                "usuario.tem_papel",
                "Usuário tem papel 'Motorista'. Exclua pela tela de Motoristas.");
        }

        u.ExcluidoEm = DateTime.UtcNow;
        u.ExcluidoPor = _atual.UsuarioId;
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
