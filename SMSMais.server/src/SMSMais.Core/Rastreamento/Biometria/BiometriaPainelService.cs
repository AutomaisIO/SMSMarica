using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Rastreamento.Biometria;

/// <summary>
/// Lado do painel do login pelo rosto (ADR-0073): situação e foto de referência do motorista, apagar o
/// cadastro (o motorista refaz o primeiro acesso), gerar o código de liberação da central e consultar
/// a trilha de conferências com a selfie.
/// </summary>
public interface IBiometriaPainelService
{
    Task<BiometriaMotoristaDto> SituacaoAsync(Guid motoristaId, CancellationToken ct = default);
    Task<FotoBiometriaDto> FotoReferenciaAsync(Guid motoristaId, CancellationToken ct = default);
    Task ApagarAsync(Guid motoristaId, CancellationToken ct = default);

    /// <summary>Código de 6 números para o motorista entrar no tablet do veículo. Gerar outro anula
    /// o anterior do mesmo veículo.</summary>
    Task<LiberacaoGeradaDto> GerarLiberacaoAsync(
        Guid veiculoId, GerarLiberacaoRequest request, CancellationToken ct = default);

    /// <summary>Mais recentes primeiro; sem dia, os últimos 7 dias. No máximo 200.</summary>
    Task<IReadOnlyList<VerificacaoFacialDto>> ListarVerificacoesAsync(
        FiltroVerificacoesFaciais filtro, CancellationToken ct = default);

    Task<FotoBiometriaDto> SelfieAsync(Guid verificacaoId, CancellationToken ct = default);
}

public sealed class BiometriaPainelService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual) : IBiometriaPainelService
{
    public static readonly TimeSpan ValidadeLiberacao = TimeSpan.FromMinutes(15);
    private const int MaximoVerificacoes = 200;

    public async Task<BiometriaMotoristaDto> SituacaoAsync(Guid motoristaId, CancellationToken ct = default)
    {
        await GarantirMotoristaAsync(motoristaId, ct);
        var f = await (
            from r in db.ReferenciasFaciaisMotorista.AsNoTracking()
            where r.MotoristaId == motoristaId
            join v in db.Veiculos on r.VeiculoId equals (Guid?)v.Id into vs
            from v in vs.DefaultIfEmpty()
            select new { r.CadastradaEm, Placa = v == null ? null : v.Placa, r.TermoVersao }).FirstOrDefaultAsync(ct);
        return f is null
            ? new BiometriaMotoristaDto(false, null, null, null)
            : new BiometriaMotoristaDto(true, f.CadastradaEm, f.Placa, f.TermoVersao);
    }

    public async Task<FotoBiometriaDto> FotoReferenciaAsync(Guid motoristaId, CancellationToken ct = default)
    {
        var foto = await db.ReferenciasFaciaisMotorista.AsNoTracking()
            .Where(r => r.MotoristaId == motoristaId).Select(r => r.FotoBase64).FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException("Referência facial", motoristaId);
        return new FotoBiometriaDto(foto);
    }

    public async Task ApagarAsync(Guid motoristaId, CancellationToken ct = default)
    {
        var f = await db.ReferenciasFaciaisMotorista.FirstOrDefaultAsync(r => r.MotoristaId == motoristaId, ct)
            ?? throw new NaoEncontradoException("Referência facial", motoristaId);
        db.ReferenciasFaciaisMotorista.Remove(f);
        await db.SaveChangesAsync(ct);
    }

    public async Task<LiberacaoGeradaDto> GerarLiberacaoAsync(
        Guid veiculoId, GerarLiberacaoRequest request, CancellationToken ct = default)
    {
        var placa = await db.Veiculos.AsNoTracking().Where(v => v.Id == veiculoId)
            .Select(v => v.Placa).FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException(nameof(Veiculo), veiculoId);
        var nome = await db.Motoristas.AsNoTracking()
            .Where(m => m.Id == request.MotoristaId && m.ExcluidoEm == null
                && m.Usuario.Ativo && m.Usuario.ExcluidoEm == null)
            .Select(m => m.Usuario.NomeCompleto).FirstOrDefaultAsync(ct)
            ?? throw new ValidacaoException("motoristaId", "Escolha um motorista ativo.");

        var agora = DateTime.UtcNow;
        // Um código valendo por veículo: o novo anula o que ainda não foi usado.
        var abertas = await db.LiberacoesMotorista
            .Where(l => l.VeiculoId == veiculoId && l.UsadaEm == null && l.ExpiraEm > agora)
            .ToListAsync(ct);
        foreach (var l in abertas) l.ExpiraEm = agora;

        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var liberacao = new LiberacaoMotorista
        {
            Id = Guid.NewGuid(),
            VeiculoId = veiculoId,
            MotoristaId = request.MotoristaId,
            CodigoHash = BiometriaTabletService.HashLiberacao(veiculoId, codigo),
            CriadaEm = agora,
            CriadaPor = usuarioAtual.UsuarioId,
            ExpiraEm = agora + ValidadeLiberacao,
        };
        db.LiberacoesMotorista.Add(liberacao);
        await db.SaveChangesAsync(ct);
        return new LiberacaoGeradaDto(codigo, liberacao.ExpiraEm, nome, placa);
    }

    public async Task<IReadOnlyList<VerificacaoFacialDto>> ListarVerificacoesAsync(
        FiltroVerificacoesFaciais filtro, CancellationToken ct = default)
    {
        var q = db.VerificacoesFaciaisMotorista.AsNoTracking();
        if (filtro.Dia is { } dia)
        {
            var de = FusoBrasilia.DeBrasiliaParaUtc(dia.ToDateTime(TimeOnly.MinValue));
            var ate = FusoBrasilia.DeBrasiliaParaUtc(dia.AddDays(1).ToDateTime(TimeOnly.MinValue));
            q = q.Where(v => v.RecebidaEm >= de && v.RecebidaEm < ate);
        }
        else
        {
            var desde = DateTime.UtcNow.AddDays(-7);
            q = q.Where(v => v.RecebidaEm >= desde);
        }
        if (filtro.VeiculoId is { } veiculoId) q = q.Where(v => v.VeiculoId == veiculoId);
        if (filtro.MotoristaId is { } motoristaId) q = q.Where(v => v.MotoristaId == motoristaId);

        return await (
            from v in q
            join ve in db.Veiculos on v.VeiculoId equals ve.Id
            join m in db.Motoristas on v.MotoristaId equals (Guid?)m.Id into ms
            from m in ms.DefaultIfEmpty()
            join l in db.LiberacoesMotorista on v.LiberacaoId equals (Guid?)l.Id into ls
            from l in ls.DefaultIfEmpty()
            join u in db.Usuarios on l.CriadaPor equals (Guid?)u.Id into us
            from u in us.DefaultIfEmpty()
            orderby v.RecebidaEm descending
            select new VerificacaoFacialDto(
                v.Id, v.VeiculoId, ve.Placa, v.MotoristaId,
                m == null ? null : m.Usuario.NomeCompleto,
                v.Momento, v.Meio, v.Aprovada, v.Nota, v.Limiar, v.ProvaDeVida,
                v.Latitude, v.Longitude, v.CapturadaEm, v.RecebidaEm,
                v.SelfieBase64 != null,
                u == null ? null : u.NomeCompleto)).Take(MaximoVerificacoes).ToListAsync(ct);
    }

    public async Task<FotoBiometriaDto> SelfieAsync(Guid verificacaoId, CancellationToken ct = default)
    {
        var selfie = await db.VerificacoesFaciaisMotorista.AsNoTracking()
            .Where(v => v.Id == verificacaoId).Select(v => v.SelfieBase64).FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException("Selfie da conferência", verificacaoId);
        return new FotoBiometriaDto(selfie);
    }

    private async Task GarantirMotoristaAsync(Guid motoristaId, CancellationToken ct)
    {
        if (!await db.Motoristas.AnyAsync(m => m.Id == motoristaId, ct))
            throw new NaoEncontradoException(nameof(Motorista), motoristaId);
    }
}
