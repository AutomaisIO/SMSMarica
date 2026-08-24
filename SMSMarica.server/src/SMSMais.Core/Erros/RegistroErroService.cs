using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Erros.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Erros;

public sealed class RegistroErroService(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual) : IRegistroErroService
{
    private const int TamanhoMaximo = 200;

    // Alfabeto sem caracteres ambíguos (0/O, 1/I/L) — código fácil de ditar por telefone.
    private const string Alfabeto = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    public async Task<RegistroErroResultado> RegistrarAsync(RegistrarErroDados dados, CancellationToken cancellationToken = default)
    {
        var agora = DateTime.UtcNow;
        var assinatura = Assinar(dados);

        // Dedup: se já existe um erro IGUAL e ainda EM ABERTO, reusa o código e só incrementa as
        // ocorrências (não gera código novo). Erros resolvidos não absorvem — reincidência vira
        // registro novo (sinal de regressão).
        var existente = await db.RegistrosErro
            .Where(e => e.Assinatura == assinatura && e.ResolvidoEm == null)
            .OrderByDescending(e => e.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken);

        if (existente is not null)
        {
            existente.Ocorrencias += 1;
            existente.UltimaOcorrenciaEm = agora;
            await db.SaveChangesAsync(cancellationToken);
            return new RegistroErroResultado(existente.CodigoReferencia, JaReportado: true, existente.Ocorrencias);
        }

        var usuarioId = usuarioAtual.UsuarioId;
        var usuarioNome = usuarioId is null
            ? null
            : await db.Usuarios.AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => u.NomeCompleto)
                .FirstOrDefaultAsync(cancellationToken);

        var codigo = GerarCodigo();

        db.RegistrosErro.Add(new RegistroErro
        {
            Id = Guid.CreateVersion7(),
            CodigoReferencia = codigo,
            Assinatura = assinatura,
            Ocorrencias = 1,
            CriadoEm = agora,
            UltimaOcorrenciaEm = agora,
            Metodo = Limitar(dados.Metodo, 10),
            Caminho = Limitar(dados.Caminho, 400),
            QueryString = Limitar(dados.QueryString, 2000),
            StatusCode = dados.StatusCode,
            TipoExcecao = Limitar(dados.TipoExcecao, 300),
            Mensagem = dados.Mensagem,
            StackTrace = dados.StackTrace,
            Interna = dados.Interna,
            TraceId = Limitar(dados.TraceId, 120),
            UsuarioId = usuarioId,
            UsuarioNome = usuarioNome,
            UserAgent = Limitar(dados.UserAgent, 500),
        });

        await db.SaveChangesAsync(cancellationToken);
        return new RegistroErroResultado(codigo, JaReportado: false, 1);
    }

    /// <summary>Assinatura estável do erro — o que define "o mesmo erro" para dedup.</summary>
    private static string Assinar(RegistrarErroDados d)
    {
        var bruto = $"{d.Metodo}|{d.Caminho}|{d.StatusCode}|{d.TipoExcecao}|{d.Mensagem}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(bruto));
        return Convert.ToHexString(hash); // 64 chars
    }

    public async Task<RegistroErroDto?> ResolverAsync(
        string codigo, ResolverErroRequest request, CancellationToken cancellationToken = default)
    {
        var alvo = codigo.Trim();
        var erro = await db.RegistrosErro
            .Where(e => e.CodigoReferencia == alvo)
            .OrderByDescending(e => e.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken);
        if (erro is null) return null;

        erro.ResolvidoEm = DateTime.UtcNow;
        erro.ResolvidoPor = Limitar(request.ResolvidoPor, 200);
        erro.ResolucaoNota = request.Nota;
        await db.SaveChangesAsync(cancellationToken);
        return Projetar(erro);
    }

    public async Task<RegistroErroDto?> ReabrirAsync(string codigo, CancellationToken cancellationToken = default)
    {
        var alvo = codigo.Trim();
        var erro = await db.RegistrosErro
            .Where(e => e.CodigoReferencia == alvo)
            .OrderByDescending(e => e.CriadoEm)
            .FirstOrDefaultAsync(cancellationToken);
        if (erro is null) return null;

        erro.ResolvidoEm = null;
        erro.ResolvidoPor = null;
        erro.ResolucaoNota = null;
        await db.SaveChangesAsync(cancellationToken);
        return Projetar(erro);
    }

    private static RegistroErroDto Projetar(RegistroErro e) => new(
        e.Id, e.CodigoReferencia, e.CriadoEm, e.Metodo, e.Caminho, e.QueryString,
        e.StatusCode, e.TipoExcecao, e.Mensagem, e.StackTrace, e.Interna,
        e.TraceId, e.UsuarioId, e.UsuarioNome, e.UserAgent,
        e.Ocorrencias, e.UltimaOcorrenciaEm, e.ResolvidoEm, e.ResolvidoPor, e.ResolucaoNota);

    public async Task<PaginaErrosDto> BuscarAsync(ErroFiltroDto filtro, CancellationToken cancellationToken = default)
    {
        var q = db.RegistrosErro.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            var codigo = filtro.Codigo.Trim();
            q = q.Where(e => e.CodigoReferencia == codigo);
        }
        if (filtro.De is { } de)
            q = q.Where(e => e.CriadoEm >= de);
        if (filtro.Ate is { } ate)
            q = q.Where(e => e.CriadoEm <= ate);
        if (!string.IsNullOrWhiteSpace(filtro.Texto))
        {
            var padrao = $"%{filtro.Texto.Trim()}%";
            q = q.Where(e =>
                EF.Functions.ILike(e.Mensagem, padrao) ||
                EF.Functions.ILike(e.Caminho, padrao) ||
                EF.Functions.ILike(e.TipoExcecao, padrao) ||
                EF.Functions.ILike(e.UsuarioNome ?? string.Empty, padrao) ||
                EF.Functions.ILike(e.CodigoReferencia, padrao));
        }

        var total = await q.CountAsync(cancellationToken);

        var pagina = filtro.Pagina < 1 ? 1 : filtro.Pagina;
        var tamanho = Math.Clamp(filtro.Tamanho, 1, TamanhoMaximo);

        var itens = await q
            .OrderByDescending(e => e.CriadoEm)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(e => new RegistroErroListItemDto(
                e.Id, e.CodigoReferencia, e.CriadoEm, e.Metodo, e.Caminho,
                e.StatusCode, e.TipoExcecao, e.Mensagem, e.UsuarioNome,
                e.Ocorrencias, e.UltimaOcorrenciaEm, e.ResolvidoEm))
            .ToListAsync(cancellationToken);

        return new PaginaErrosDto(itens, total, pagina, tamanho);
    }

    public async Task<RegistroErroDto?> ObterPorCodigoAsync(string codigo, CancellationToken cancellationToken = default)
    {
        var alvo = codigo.Trim();
        return await db.RegistrosErro.AsNoTracking()
            .Where(e => e.CodigoReferencia == alvo)
            .OrderByDescending(e => e.CriadoEm)
            .Select(e => new RegistroErroDto(
                e.Id, e.CodigoReferencia, e.CriadoEm, e.Metodo, e.Caminho, e.QueryString,
                e.StatusCode, e.TipoExcecao, e.Mensagem, e.StackTrace, e.Interna,
                e.TraceId, e.UsuarioId, e.UsuarioNome, e.UserAgent,
                e.Ocorrencias, e.UltimaOcorrenciaEm, e.ResolvidoEm, e.ResolvidoPor, e.ResolucaoNota))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string GerarCodigo()
    {
        Span<byte> bytes = stackalloc byte[6];
        RandomNumberGenerator.Fill(bytes);
        Span<char> chars = stackalloc char[6];
        for (var i = 0; i < 6; i++)
            chars[i] = Alfabeto[bytes[i] % Alfabeto.Length];
        return $"ERRO-{new string(chars)}";
    }

    private static string Limitar(string? valor, int max)
    {
        if (string.IsNullOrEmpty(valor)) return valor ?? string.Empty;
        return valor.Length <= max ? valor : valor[..max];
    }
}
