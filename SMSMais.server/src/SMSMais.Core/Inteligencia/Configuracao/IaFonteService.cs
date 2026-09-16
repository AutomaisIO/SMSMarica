using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Inteligencia.Dtos;
using SMSMais.Core.Inteligencia.Fontes;
using SMSMais.Core.Inteligencia.Seguranca;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ia;

namespace SMSMais.Core.Inteligencia.Configuracao;

/// <summary>
/// CRUD das bases de dados (fontes) do módulo IA + teste de conexão. A senha da conta
/// read-only é cifrada em repouso e nunca devolvida (write-only; só a flag <c>SenhaDefinida</c>).
/// Exclusão é lógica (<c>ExcluidoEm</c>).
/// </summary>
public sealed class IaFonteService(
    SmsMaisDbContext db,
    IProtetorSegredos protetor,
    IFonteDadosFactory fonteDadosFactory,
    Fontes.Agente.IAgenteSqlRegistry agenteRegistry,
    Microsoft.Extensions.Configuration.IConfiguration configuracao,
    IUsuarioAtualAccessor usuarioAtual) : IIaFonteService
{
    private readonly SmsMaisDbContext _db = db;
    private readonly IProtetorSegredos _protetor = protetor;
    private readonly IFonteDadosFactory _fonteDadosFactory = fonteDadosFactory;
    private readonly Fontes.Agente.IAgenteSqlRegistry _agenteRegistry = agenteRegistry;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _configuracao = configuracao;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task<IReadOnlyList<FonteDetalheDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var fontes = await _db.IaFontes.AsNoTracking()
            .Where(f => f.ExcluidoEm == null)
            .OrderBy(f => f.Nome)
            .ToListAsync(cancellationToken);
        return fontes.Select(ParaDto).ToList();
    }

    public async Task<Guid> CadastrarAsync(CadastrarFonteRequest request, CancellationToken cancellationToken = default)
    {
        var slug = NormalizarSlug(request.Slug);
        await GarantirSlugUnicoAsync(slug, null, cancellationToken);

        var fonte = new IaFonte
        {
            Id = Guid.CreateVersion7(),
            Nome = request.Nome.Trim(),
            Slug = slug,
            Tipo = ParseTipo(request.Tipo),
            Dialeto = ParseDialeto(request.Dialeto),
            Ambiente = ParseAmbiente(request.Ambiente),
            Host = Normalizar(request.Host),
            Porta = request.Porta,
            Servico = Normalizar(request.Servico),
            Usuario = Normalizar(request.Usuario),
            BaseUrl = Normalizar(request.BaseUrl),
            ViaAgente = request.ViaAgente,
            Familia = Normalizar(request.Familia),
            ParametrosJson = Normalizar(request.ParametrosJson),
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        if (request.ViaAgente && string.IsNullOrWhiteSpace(slug))
        {
            throw new ValidacaoException(
                "ia.fonte.slug", "Base via agente precisa de um slug — é o identificador do agente.");
        }

        // Base via agente NÃO guarda credenciais do banco: elas vivem no .env do destino.
        if (!request.ViaAgente && !string.IsNullOrWhiteSpace(request.Senha))
        {
            fonte.SenhaCifrada = _protetor.Proteger(request.Senha);
        }

        _db.IaFontes.Add(fonte);
        await _db.SaveChangesAsync(cancellationToken);
        return fonte.Id;
    }

    /// <summary>
    /// Gera (ou rotaciona) o token de conexão do agente de uma base via agente. Devolve o token em
    /// claro UMA vez — guardamos só o hash. Ver ADR-0023.
    /// </summary>
    public async Task<TokenAgenteGerado> GerarTokenAgenteAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var fonte = await _db.IaFontes
            .FirstOrDefaultAsync(f => f.Id == id && f.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException("Fonte", id);

        if (!fonte.ViaAgente || string.IsNullOrWhiteSpace(fonte.Slug))
        {
            throw new ValidacaoException(
                "ia.fonte.viaAgente", "Só bases via agente (com slug) têm token de agente.");
        }

        var token = Fontes.Agente.TokenAgente.Gerar();
        fonte.AgenteTokenHash = Fontes.Agente.TokenAgente.Hash(token);
        fonte.AtualizadoEm = DateTime.UtcNow;
        fonte.AtualizadoPor = _usuarioAtual.UsuarioId;
        await _db.SaveChangesAsync(cancellationToken);

        return MontarToken(fonte.Slug, token);
    }

    public async Task<TokenAgenteGerado> ProvisionarAgenteAsync(
        string slug, string? nome, CancellationToken cancellationToken = default)
    {
        var slugNorm = NormalizarSlug(slug);
        if (string.IsNullOrWhiteSpace(slugNorm))
        {
            throw new ValidacaoException("ia.fonte.slug", "Informe um slug para o agente.");
        }

        var fonte = await _db.IaFontes
            .FirstOrDefaultAsync(f => f.Slug == slugNorm && f.ExcluidoEm == null, cancellationToken);

        if (fonte is null)
        {
            // Primeiro run do agente: cria a base via agente já apontando para SQL Server.
            fonte = new IaFonte
            {
                Id = Guid.CreateVersion7(),
                Nome = string.IsNullOrWhiteSpace(nome) ? slugNorm : nome!.Trim(),
                Slug = slugNorm,
                Tipo = TipoFonte.SqlServer,
                Dialeto = DialetoSql.SqlServer,
                Ambiente = AmbienteFonte.Producao,
                ViaAgente = true,
                Ativo = true,
                CriadoEm = DateTime.UtcNow,
                CriadoPor = _usuarioAtual.UsuarioId,
            };
            _db.IaFontes.Add(fonte);
        }
        else if (!fonte.ViaAgente)
        {
            throw new ConflitoException(
                "ia.fonte.slug",
                $"Já existe uma base '{slugNorm}' que não é via agente. Use outro slug para o agente.");
        }

        var token = Fontes.Agente.TokenAgente.Gerar();
        fonte.AgenteTokenHash = Fontes.Agente.TokenAgente.Hash(token);
        fonte.AtualizadoEm = DateTime.UtcNow;
        fonte.AtualizadoPor = _usuarioAtual.UsuarioId;
        await _db.SaveChangesAsync(cancellationToken);

        return MontarToken(slugNorm, token);
    }

    private TokenAgenteGerado MontarToken(string slug, string token)
    {
        var baseUrl = _configuracao["Ia:AgenteWssBaseUrl"] ?? "wss://api.smsmarica.online";
        var wss = $"{baseUrl.TrimEnd('/')}/agentes/sql";
        return new TokenAgenteGerado(slug, token, wss);
    }

    public async Task AtualizarAsync(Guid id, AtualizarFonteRequest request, CancellationToken cancellationToken = default)
    {
        var fonte = await ObterAtivaAsync(id, cancellationToken);

        var slug = NormalizarSlug(request.Slug);
        await GarantirSlugUnicoAsync(slug, id, cancellationToken);

        fonte.Nome = request.Nome.Trim();
        fonte.Slug = slug;
        fonte.Ambiente = ParseAmbiente(request.Ambiente);
        fonte.Host = Normalizar(request.Host);
        fonte.Porta = request.Porta;
        fonte.Servico = Normalizar(request.Servico);
        fonte.Usuario = Normalizar(request.Usuario);
        fonte.BaseUrl = Normalizar(request.BaseUrl);
        fonte.Familia = Normalizar(request.Familia);
        fonte.ParametrosJson = Normalizar(request.ParametrosJson);
        fonte.Ativo = request.Ativo;

        // Senha nula/vazia = mantém a atual; preenchida = cifra e substitui.
        if (!string.IsNullOrWhiteSpace(request.Senha))
        {
            fonte.SenhaCifrada = _protetor.Proteger(request.Senha);
        }

        fonte.AtualizadoEm = DateTime.UtcNow;
        fonte.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoverAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fonte = await ObterAtivaAsync(id, cancellationToken);

        fonte.ExcluidoEm = DateTime.UtcNow;
        fonte.ExcluidoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TestarConexaoResultado> TestarConexaoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var fonte = await ObterAtivaAsync(id, cancellationToken);

        try
        {
            var ok = await _fonteDadosFactory.Criar(fonte).TestarConexaoAsync(cancellationToken);
            return new TestarConexaoResultado(ok, ok ? "Conexão estabelecida com sucesso." : "Falha ao conectar à base.");
        }
        catch (Exception ex)
        {
            return new TestarConexaoResultado(false, ex.Message);
        }
    }

    private async Task<IaFonte> ObterAtivaAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.IaFontes.FirstOrDefaultAsync(f => f.Id == id && f.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(IaFonte), id);

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Normaliza o slug: minúsculas, troca espaços/inválidos por '-', valida formato.</summary>
    private static string? NormalizarSlug(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var s = System.Text.RegularExpressions.Regex.Replace(valor.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        if (s.Length == 0)
            throw new ValidacaoException("iaFonte.slug_invalido", "Slug inválido: use letras, números e hífen (ex.: 'salux-hcml').");
        return s;
    }

    private async Task GarantirSlugUnicoAsync(string? slug, Guid? exceto, CancellationToken cancellationToken)
    {
        if (slug is null) return;
        var existe = await _db.IaFontes.AsNoTracking()
            .AnyAsync(f => f.ExcluidoEm == null && f.Slug == slug && (exceto == null || f.Id != exceto), cancellationToken);
        if (existe)
            throw new ConflitoException("iaFonte.slug_duplicado", $"Já existe uma base com o slug '{slug}'.");
    }

    private FonteDetalheDto ParaDto(IaFonte f) => new(
        f.Id,
        f.Nome,
        f.Slug,
        f.Tipo.ToString(),
        f.Dialeto.ToString(),
        f.Ambiente.ToString(),
        f.Host,
        f.Porta,
        f.Servico,
        f.Usuario,
        f.BaseUrl,
        !string.IsNullOrEmpty(f.SenhaCifrada),
        f.Ativo,
        f.ViaAgente,
        f.ViaAgente && f.Slug is not null && _agenteRegistry.EstaConectado(f.Slug),
        !string.IsNullOrEmpty(f.AgenteTokenHash),
        f.Familia,
        f.ParametrosJson);

    private static TipoFonte ParseTipo(string valor) =>
        Enum.TryParse<TipoFonte>(valor, ignoreCase: true, out var v)
            ? v
            : throw new ValidacaoException("iaFonte.tipo_invalido", $"Tipo de fonte inválido: '{valor}'.");

    private static DialetoSql ParseDialeto(string valor) =>
        Enum.TryParse<DialetoSql>(valor, ignoreCase: true, out var v)
            ? v
            : throw new ValidacaoException("iaFonte.dialeto_invalido", $"Dialeto SQL inválido: '{valor}'.");

    private static AmbienteFonte ParseAmbiente(string valor) =>
        Enum.TryParse<AmbienteFonte>(valor, ignoreCase: true, out var v)
            ? v
            : throw new ValidacaoException("iaFonte.ambiente_invalido", $"Ambiente inválido: '{valor}'.");
}
