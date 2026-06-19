using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Faturamento.Dtos;
using SMSMarica.Core.Geo;
using SMSMarica.Core.Pacientes;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Tfd;

namespace SMSMarica.Core.Faturamento;

public sealed class FaturamentoService(
    SmsMaricaDbContext db,
    IGeocodificadorService geo,
    IDistanciaService distancia,
    IPacientesService pacientes,
    IPacienteResolver pacienteResolver,
    ILogger<FaturamentoService> logger) : IFaturamentoService
{
    public async Task<RegistroFaturamentoDto> ContabilizarAsync(Guid sessaoId, CancellationToken ct = default)
    {
        var sessao = await db.Sessoes.Include(s => s.Tratamento)
            .FirstOrDefaultAsync(s => s.Id == sessaoId, ct)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);
        var trat = sessao.Tratamento
            ?? throw new ConflitoException("sessao.sem_tratamento", "Sessão sem tratamento associado.");

        var unidade = await db.Unidades.AsNoTracking().FirstOrDefaultAsync(u => u.Id == trat.UnidadeId, ct);
        var (valorPorUnidade, kmPorUnidade, codigoSigtap) = await ObterContextoConfigAsync(ct);

        decimal km = 0;
        try
        {
            var pac = await pacientes.ObterPorIdAsync(trat.PacienteId, ct);
            var origem = await geo.GeocodificarAsync(pac.Endereco?.ParaEntidade(), ct);
            var destino = unidade?.Gps;
            if (origem is not null && destino is not null)
            {
                var metros = await distancia.DistanciaMetrosAsync(origem, new Coordenada(destino.Latitude, destino.Longitude), ct);
                km = (decimal)Math.Round(metros / 1000.0 * 2, 2); // ida + volta = km com o paciente a bordo
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Faturamento: não foi possível medir a distância da sessão {Id}.", sessaoId);
        }

        var unidades = kmPorUnidade > 0 ? Math.Round(km / kmPorUnidade, 2, MidpointRounding.AwayFromZero) : 0m;
        var valorTotal = Math.Round(unidades * valorPorUnidade, 2, MidpointRounding.AwayFromZero);
        var dataRef = sessao.RealizadaEm is { } r ? DateOnly.FromDateTime(r) : sessao.DataPrevista;

        var reg = await db.RegistrosFaturamento.FirstOrDefaultAsync(x => x.SessaoId == sessaoId, ct);
        if (reg is null)
        {
            reg = new RegistroFaturamento { Id = Guid.CreateVersion7(), SessaoId = sessaoId, CriadoEm = DateTime.UtcNow };
            db.RegistrosFaturamento.Add(reg);
        }
        else
        {
            reg.AtualizadoEm = DateTime.UtcNow;
        }

        reg.PacienteId = trat.PacienteId;
        reg.MotoristaId = sessao.MotoristaIdaId ?? sessao.MotoristaVoltaId;
        reg.VeiculoId = sessao.VeiculoIdaId ?? sessao.VeiculoVoltaId;
        reg.TipoTratamentoId = trat.TipoTratamentoId;
        reg.UnidadeId = trat.UnidadeId;
        reg.Competencia = (dataRef.Year * 100) + dataRef.Month;
        reg.Data = dataRef;
        reg.KmComPaciente = km;
        reg.Unidades = unidades;
        reg.ValorUnitario = valorPorUnidade;
        reg.ValorTotal = valorTotal;
        reg.CodigoSigtap = codigoSigtap;

        await db.SaveChangesAsync(ct);

        var nomes = await pacienteResolver.ResolverManyAsync([reg.PacienteId], ct);
        return MapDto(reg, nomes.TryGetValue(reg.PacienteId, out var p) ? p.Nome : string.Empty, unidade?.Nome ?? string.Empty);
    }

    public async Task<IReadOnlyList<RegistroFaturamentoDto>> ListarAsync(
        int? competencia, DateOnly? de, DateOnly? ate, CancellationToken ct = default)
    {
        var regs = await Filtrar(competencia, de, ate).OrderByDescending(r => r.Data).ToListAsync(ct);
        var nomes = await pacienteResolver.ResolverManyAsync(regs.Select(r => r.PacienteId), ct);
        var unidadeIds = regs.Select(r => r.UnidadeId).Distinct().ToList();
        var unidades = await db.Unidades.AsNoTracking()
            .Where(u => unidadeIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Nome, ct);

        return [.. regs.Select(r => MapDto(
            r,
            nomes.TryGetValue(r.PacienteId, out var p) ? p.Nome : string.Empty,
            unidades.GetValueOrDefault(r.UnidadeId, string.Empty)))];
    }

    public async Task<ResumoFaturamentoDto> ResumoAsync(
        DimensaoFaturamento dimensao, int? competencia, DateOnly? de, DateOnly? ate, CancellationToken ct = default)
    {
        var regs = await Filtrar(competencia, de, ate).ToListAsync(ct);

        Func<RegistroFaturamento, Guid?> chave = dimensao switch
        {
            DimensaoFaturamento.Motorista => r => r.MotoristaId,
            DimensaoFaturamento.Veiculo => r => r.VeiculoId,
            DimensaoFaturamento.TipoTratamento => r => r.TipoTratamentoId,
            DimensaoFaturamento.Unidade => r => r.UnidadeId,
            _ => r => r.PacienteId,
        };

        var grupos = regs.GroupBy(chave).Select(g => new
        {
            Chave = g.Key,
            Qtd = g.Count(),
            Km = g.Sum(x => x.KmComPaciente),
            Un = g.Sum(x => x.Unidades),
            Valor = g.Sum(x => x.ValorTotal),
        }).ToList();

        var descricoes = await ResolverDescricoesAsync(dimensao, grupos.Select(g => g.Chave), ct);

        var itens = grupos
            .Select(g => new ResumoFaturamentoItemDto(
                g.Chave?.ToString() ?? string.Empty,
                g.Chave is { } k && descricoes.TryGetValue(k, out var d) ? d : "Não informado",
                g.Qtd, g.Km, g.Un, g.Valor))
            .OrderByDescending(i => i.TotalValor)
            .ToList();

        return new ResumoFaturamentoDto(
            dimensao, competencia, de, ate, itens,
            itens.Sum(i => i.TotalUnidades), itens.Sum(i => i.TotalValor));
    }

    public async Task<TfdConfigFaturamentoDto> ObterConfigAsync(CancellationToken ct = default)
    {
        var c = await ObterOuCriarConfigAsync(ct);
        return new TfdConfigFaturamentoDto(c.ValorPor50Km, c.KmPorUnidade, c.CodigoSigtap, c.Ativo);
    }

    public async Task AtualizarConfigAsync(AtualizarTfdConfigFaturamentoRequest request, CancellationToken ct = default)
    {
        var c = await ObterOuCriarConfigAsync(ct);
        c.ValorPor50Km = request.ValorPor50Km;
        c.KmPorUnidade = request.KmPorUnidade <= 0 ? 50 : request.KmPorUnidade;
        c.CodigoSigtap = string.IsNullOrWhiteSpace(request.CodigoSigtap) ? null : request.CodigoSigtap.Trim();
        c.Ativo = request.Ativo;
        c.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<RegistroFaturamento> Filtrar(int? competencia, DateOnly? de, DateOnly? ate)
    {
        var q = db.RegistrosFaturamento.AsNoTracking();
        if (competencia is not null) q = q.Where(r => r.Competencia == competencia);
        if (de is not null) q = q.Where(r => r.Data >= de);
        if (ate is not null) q = q.Where(r => r.Data <= ate);
        return q;
    }

    private async Task<Dictionary<Guid, string>> ResolverDescricoesAsync(
        DimensaoFaturamento dim, IEnumerable<Guid?> chaves, CancellationToken ct)
    {
        var ids = chaves.Where(c => c is not null).Select(c => c!.Value).Distinct().ToList();
        if (ids.Count == 0) return [];

        switch (dim)
        {
            case DimensaoFaturamento.Motorista:
                return await db.Motoristas.AsNoTracking().Include(m => m.Usuario)
                    .Where(m => ids.Contains(m.Id))
                    .ToDictionaryAsync(m => m.Id, m => m.Usuario != null ? m.Usuario.NomeCompleto : "Motorista", ct);
            case DimensaoFaturamento.Veiculo:
                return await db.Veiculos.AsNoTracking().Where(v => ids.Contains(v.Id))
                    .ToDictionaryAsync(v => v.Id, v => $"{v.Placa} - {v.Modelo}", ct);
            case DimensaoFaturamento.TipoTratamento:
                return await db.TiposTratamento.AsNoTracking().Where(t => ids.Contains(t.Id))
                    .ToDictionaryAsync(t => t.Id, t => t.Nome, ct);
            case DimensaoFaturamento.Unidade:
                return await db.Unidades.AsNoTracking().Where(u => ids.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.Nome, ct);
            default: // Paciente — resolve no hub FHIR
                var nomes = await pacienteResolver.ResolverManyAsync(ids, ct);
                var dic = new Dictionary<Guid, string>();
                foreach (var kv in nomes) dic[kv.Key] = kv.Value.Nome;
                return dic;
        }
    }

    private async Task<(decimal Valor, int KmPorUnidade, string? Codigo)> ObterContextoConfigAsync(CancellationToken ct)
    {
        var c = await db.TfdConfigFaturamento.AsNoTracking().FirstOrDefaultAsync(ct);
        if (c is null || !c.Ativo) return (0m, 50, c?.CodigoSigtap);
        return (c.ValorPor50Km, c.KmPorUnidade <= 0 ? 50 : c.KmPorUnidade, c.CodigoSigtap);
    }

    private async Task<TfdConfigFaturamento> ObterOuCriarConfigAsync(CancellationToken ct)
    {
        var c = await db.TfdConfigFaturamento.FirstOrDefaultAsync(ct);
        if (c is not null) return c;
        c = new TfdConfigFaturamento { Id = Guid.CreateVersion7(), KmPorUnidade = 50, CriadoEm = DateTime.UtcNow };
        db.TfdConfigFaturamento.Add(c);
        await db.SaveChangesAsync(ct);
        return c;
    }

    private static RegistroFaturamentoDto MapDto(RegistroFaturamento r, string pacienteNome, string unidadeNome) => new(
        r.Id, r.SessaoId, r.PacienteId, pacienteNome, r.MotoristaId, r.VeiculoId, r.TipoTratamentoId,
        r.UnidadeId, unidadeNome, r.Competencia, r.Data, r.KmComPaciente, r.Unidades, r.ValorUnitario,
        r.ValorTotal, r.CodigoSigtap, r.Status);

}
