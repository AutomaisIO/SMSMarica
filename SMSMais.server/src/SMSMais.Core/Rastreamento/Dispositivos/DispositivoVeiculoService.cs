using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Rastreamento.Dispositivos;

// Contrato: docs/modulos/tfd/deslocamento-tablet.md.

public sealed record DispositivoVeiculoDto(
    Guid Id, bool Ativo, DateTime? AtivadoEm, DateTime? UltimoContatoEm, string? Modelo,
    bool CodigoPendente, DateTime? CodigoExpiraEm);

public sealed record CodigoAtivacaoDto(string Codigo, DateTime ExpiraEm);

public sealed record AtivarDispositivoRequest(string Codigo, string? Modelo, string? Identificador);

public sealed record DispositivoAtivadoDto(string Token, Guid VeiculoId, string VeiculoPlaca, string VeiculoModelo);

public sealed record VeiculoDoDispositivoDto(Guid VeiculoId, string VeiculoPlaca, string VeiculoModelo);

public sealed record PosicaoTabletRequest(
    double Latitude, double Longitude, double? VelocidadeKmh, double? Rumo, double? PrecisaoM, DateTime CapturadoEm);

public sealed record EnviarPosicoesRequest(IReadOnlyList<PosicaoTabletRequest>? Pontos);

public interface IDispositivoVeiculoService
{
    Task<DispositivoVeiculoDto?> ObterDoVeiculoAsync(Guid veiculoId, CancellationToken ct = default);
    Task<CodigoAtivacaoDto> GerarCodigoAsync(Guid veiculoId, CancellationToken ct = default);
    Task DesvincularAsync(Guid veiculoId, CancellationToken ct = default);

    /// <summary>Null = código inválido, vencido ou já usado.</summary>
    Task<DispositivoAtivadoDto?> AtivarAsync(AtivarDispositivoRequest request, CancellationToken ct = default);

    /// <summary>Null = token desconhecido ou revogado.</summary>
    Task<DispositivoVeiculo?> AutenticarAsync(string? token, CancellationToken ct = default);
    Task<VeiculoDoDispositivoDto> VeiculoDoDispositivoAsync(DispositivoVeiculo dispositivo, CancellationToken ct = default);
    Task RegistrarPosicoesAsync(DispositivoVeiculo dispositivo, EnviarPosicoesRequest request, CancellationToken ct = default);
}

public sealed class DispositivoVeiculoService(SmsMaisDbContext db) : IDispositivoVeiculoService
{
    private const int MaxPontosPorLote = 500;
    private static readonly TimeSpan ValidadeCodigo = TimeSpan.FromHours(24);
    // Sem 0/O/1/I/L: o código é digitado no tablet.
    private const string AlfabetoCodigo = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public async Task<DispositivoVeiculoDto?> ObterDoVeiculoAsync(Guid veiculoId, CancellationToken ct = default)
    {
        var d = await db.DispositivosVeiculo.AsNoTracking().FirstOrDefaultAsync(x => x.VeiculoId == veiculoId, ct);
        if (d is null) return null;
        var agora = DateTime.UtcNow;
        var pendente = d.CodigoHash is not null && d.CodigoExpiraEm > agora;
        return new DispositivoVeiculoDto(d.Id, d.TokenHash is not null, d.AtivadoEm, d.UltimoContatoEm, d.Modelo,
            pendente, pendente ? d.CodigoExpiraEm : null);
    }

    public async Task<CodigoAtivacaoDto> GerarCodigoAsync(Guid veiculoId, CancellationToken ct = default)
    {
        if (!await db.Veiculos.AnyAsync(v => v.Id == veiculoId, ct))
            throw new NaoEncontradoException(nameof(Veiculo), veiculoId);

        var agora = DateTime.UtcNow;
        var d = await db.DispositivosVeiculo.FirstOrDefaultAsync(x => x.VeiculoId == veiculoId, ct);
        if (d is null)
        {
            d = new DispositivoVeiculo { Id = Guid.NewGuid(), VeiculoId = veiculoId, CriadoEm = agora };
            db.DispositivosVeiculo.Add(d);
        }

        var codigo = RandomNumberGenerator.GetString(AlfabetoCodigo, 8);
        d.CodigoHash = Hash(codigo);
        d.CodigoExpiraEm = agora + ValidadeCodigo;
        d.AtualizadoEm = agora;
        await db.SaveChangesAsync(ct);
        return new CodigoAtivacaoDto(codigo, d.CodigoExpiraEm.Value);
    }

    public async Task DesvincularAsync(Guid veiculoId, CancellationToken ct = default)
    {
        var d = await db.DispositivosVeiculo.FirstOrDefaultAsync(x => x.VeiculoId == veiculoId, ct);
        if (d is null) return;
        db.DispositivosVeiculo.Remove(d);
        await db.SaveChangesAsync(ct);
    }

    public async Task<DispositivoAtivadoDto?> AtivarAsync(AtivarDispositivoRequest request, CancellationToken ct = default)
    {
        var codigo = (request.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (codigo.Length != 8) return null;

        var agora = DateTime.UtcNow;
        var hash = Hash(codigo);
        var d = await db.DispositivosVeiculo.Include(x => x.Veiculo)
            .FirstOrDefaultAsync(x => x.CodigoHash == hash && x.CodigoExpiraEm > agora, ct);
        if (d?.Veiculo is null) return null;

        // Ativar troca o token: o tablet anterior (se havia) para de ser aceito na hora.
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        d.TokenHash = Hash(token);
        d.CodigoHash = null;
        d.CodigoExpiraEm = null;
        d.Modelo = Cortar(request.Modelo, 120);
        d.Identificador = Cortar(request.Identificador, 120);
        d.AtivadoEm = agora;
        d.UltimoContatoEm = agora;
        d.AtualizadoEm = agora;
        await db.SaveChangesAsync(ct);
        return new DispositivoAtivadoDto(token, d.VeiculoId, d.Veiculo.Placa, d.Veiculo.Modelo);
    }

    public async Task<DispositivoVeiculo?> AutenticarAsync(string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var hash = Hash(token.Trim());
        return await db.DispositivosVeiculo.FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
    }

    public async Task<VeiculoDoDispositivoDto> VeiculoDoDispositivoAsync(DispositivoVeiculo dispositivo, CancellationToken ct = default)
    {
        var v = await db.Veiculos.AsNoTracking().FirstAsync(x => x.Id == dispositivo.VeiculoId, ct);
        return new VeiculoDoDispositivoDto(v.Id, v.Placa, v.Modelo);
    }

    public async Task RegistrarPosicoesAsync(DispositivoVeiculo dispositivo, EnviarPosicoesRequest request, CancellationToken ct = default)
    {
        var pontos = request.Pontos ?? [];
        if (pontos.Count > MaxPontosPorLote)
            throw new ValidacaoException("pontos", $"Máximo de {MaxPontosPorLote} pontos por envio.");

        var agora = DateTime.UtcNow;
        foreach (var p in pontos)
        {
            var capturado = p.CapturadoEm.Kind == DateTimeKind.Utc ? p.CapturadoEm : DateTime.SpecifyKind(p.CapturadoEm, DateTimeKind.Utc);
            // Ponto inválido (coordenada fora do mundo, relógio do tablet adiantado) é descartado, não derruba o lote.
            if (p.Latitude is < -90 or > 90 || p.Longitude is < -180 or > 180) continue;
            if (p.Latitude == 0 && p.Longitude == 0) continue;
            if (capturado > agora.AddMinutes(5)) continue;

            db.PosicoesVeiculo.Add(new PosicaoVeiculo
            {
                Id = Guid.NewGuid(),
                VeiculoId = dispositivo.VeiculoId,
                DispositivoId = dispositivo.Id,
                Coordenada = new Gps(p.Latitude, p.Longitude),
                VelocidadeKmh = p.VelocidadeKmh is >= 0 and < 400 ? p.VelocidadeKmh : null,
                Rumo = p.Rumo is >= 0 and <= 360 ? p.Rumo : null,
                PrecisaoM = p.PrecisaoM is >= 0 ? p.PrecisaoM : null,
                CapturadoEm = capturado,
                CriadoEm = agora,
            });
        }

        dispositivo.UltimoContatoEm = agora;
        await db.SaveChangesAsync(ct);
    }

    private static string Hash(string valor) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));

    private static string? Cortar(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length <= max ? s.Trim() : s.Trim()[..max]);
}
