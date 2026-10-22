using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Translado.Geracao;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Lotacao;

/// <summary>Os lugares livres do carro e quem já está nele (conta para isolamento e maca).</summary>
internal sealed record OcupacaoDoCarro(Veiculo Veiculo, IReadOnlyList<Assento> Livres, IReadOnlyList<NecessidadeViagem> ABordo);

/// <summary>
/// Quanto cabe no carro. A regra é uma só: a central e o motorista nunca põem no carro mais gente do que
/// ele leva. Cada acompanhante ocupa um lugar, como o paciente.
/// <list type="bullet">
/// <item><b>Na ida</b> (montar a rota) cada assento é de uma pessoa.</item>
/// <item><b>No resgate</b> vale quem está no carro naquela hora: quem foi na ida libera o lugar ao descer
/// na unidade (ou ao faltar); quem foi marcado para resgate ocupa o lugar até chegar em casa.</item>
/// </list>
/// </summary>
internal static class LotacaoDaRota
{
    public static async Task<Veiculo> VeiculoAsync(SmsMaisDbContext db, Guid veiculoId, CancellationToken ct) =>
        await db.Veiculos.AsNoTracking()
            .Include(v => v.Fileiras).ThenInclude(f => f.Assentos)
            .FirstAsync(v => v.Id == veiculoId, ct);

    /// <summary>Assentos que levam passageiro: sem o do motorista, os bloqueados e os removidos.</summary>
    public static IReadOnlyList<Assento> Usaveis(Veiculo v) => [.. v.Fileiras
        .OrderBy(f => f.Ordem)
        .SelectMany(f => f.Assentos
            .Where(a => !a.Excluido && !a.Bloqueado && a.Tipo != TipoAssento.Motorista)
            .OrderBy(a => a.Numero))];

    /// <summary>Lotação na hora do resgate.</summary>
    public static Task<OcupacaoDoCarro> AgoraAsync(SmsMaisDbContext db, RotaDiaria rota, CancellationToken ct) =>
        CalcularAsync(db, rota, l => l.Parada == TipoParada.Retorno
            ? l.HoraChegadaResidencia is null
            : l.HoraChegadaUnidade is null && l.Status is not (StatusSessao.NaoRealizada or StatusSessao.Cancelada), ct);

    /// <summary>Lotação da ida, para montar a rota: todo assento da ida conta, o resgate não.</summary>
    public static Task<OcupacaoDoCarro> DaIdaAsync(SmsMaisDbContext db, RotaDiaria rota, CancellationToken ct) =>
        CalcularAsync(db, rota, l => l.Parada != TipoParada.Retorno, ct);

    private static async Task<OcupacaoDoCarro> CalcularAsync(
        SmsMaisDbContext db, RotaDiaria rota, Func<Linha, bool> ocupa, CancellationToken ct)
    {
        var veiculo = await VeiculoAsync(db, rota.VeiculoId, ct);
        var linhas = await (
            from a in db.Alocacoes.AsNoTracking()
            where a.RotaDiariaId == rota.Id
            join s in db.Sessoes.AsNoTracking() on a.SessaoId equals s.Id
            join t in db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            select new Linha(
                a.AssentoId, a.SessaoId, a.Tipo, a.Parada, s.Status, s.HoraChegadaUnidade, s.HoraChegadaResidencia,
                t.Mobilidade, t.Isolamento, t.DificuldadeVeiculoAlto)).ToListAsync(ct);

        var noCarro = linhas.Where(ocupa).ToList();
        var ocupados = noCarro.Select(l => l.AssentoId).ToHashSet();
        var aBordo = noCarro
            .Where(l => l.Tipo == TipoAlocacao.Paciente)
            .DistinctBy(l => l.SessaoId)
            .Select(l => new NecessidadeViagem(l.SessaoId, l.Mobilidade, l.Isolamento, l.DificuldadeVeiculoAlto, 0))
            .ToList();
        return new OcupacaoDoCarro(veiculo, [.. Usaveis(veiculo).Where(a => !ocupados.Contains(a.Id))], aBordo);
    }

    private sealed record Linha(
        Guid AssentoId,
        Guid SessaoId,
        TipoAlocacao Tipo,
        TipoParada Parada,
        StatusSessao Status,
        TimeOnly? HoraChegadaUnidade,
        TimeOnly? HoraChegadaResidencia,
        MobilidadeTransporte Mobilidade,
        bool Isolamento,
        bool DificuldadeVeiculoAlto);
}
