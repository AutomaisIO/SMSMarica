using Microsoft.EntityFrameworkCore;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Lotacao;

/// <summary>
/// Quando muda quantos acompanhantes vão numa viagem que já está numa rota, os lugares deles mudam junto:
/// a mais, cada um ganha um assento livre (de acompanhante e, sem ele, de passageiro); a menos, o assento
/// sobra. A ida já embarcada e o resgate já embarcado não mudam.
/// </summary>
internal static class LugaresDosAcompanhantes
{
    /// <summary>Deixa <paramref name="total"/> assentos de acompanhante para a sessão em cada rota aberta.
    /// Falso quando não há lugar para todos — nada é mudado.</summary>
    public static async Task<bool> AjustarAsync(SmsMaisDbContext db, SessaoDeTratamento sessao, int total, CancellationToken ct)
    {
        var alocacoes = await (
            from a in db.Alocacoes
            where a.SessaoId == sessao.Id
            join r in db.Rotas on a.RotaDiariaId equals r.Id
            where r.Status == StatusRota.Planejada || r.Status == StatusRota.EmAndamento
            select a).ToListAsync(ct);

        var incluir = new List<Alocacao>();
        var excluir = new List<Alocacao>();
        foreach (var grupo in alocacoes.GroupBy(a => (a.RotaDiariaId, Resgate: a.Parada == TipoParada.Retorno)))
        {
            var embarcou = grupo.Key.Resgate ? sessao.HoraSaidaUnidade is not null : sessao.HoraSaidaResidencia is not null;
            if (embarcou) continue;

            var deles = grupo.Where(a => a.Tipo == TipoAlocacao.Acompanhante).ToList();
            if (deles.Count > total)
            {
                excluir.AddRange(deles.Skip(total));
                continue;
            }
            if (deles.Count == total) continue;

            var rota = await db.Rotas.AsNoTracking().FirstAsync(r => r.Id == grupo.Key.RotaDiariaId, ct);
            var lotacao = grupo.Key.Resgate
                ? await LotacaoDaRota.AgoraAsync(db, rota, ct)
                : await LotacaoDaRota.DaIdaAsync(db, rota, ct);
            var faltam = total - deles.Count;
            var livres = lotacao.Livres.Where(a => a.Tipo == TipoAssento.Acompanhante)
                .Concat(lotacao.Livres.Where(a => a.Tipo == TipoAssento.Passageiro))
                .Take(faltam)
                .ToList();
            if (livres.Count < faltam) return false;

            var modelo = grupo.FirstOrDefault(a => a.Tipo == TipoAlocacao.Paciente) ?? grupo.First();
            incluir.AddRange(livres.Select(a => new Alocacao
            {
                Id = Guid.CreateVersion7(),
                RotaDiariaId = modelo.RotaDiariaId,
                SessaoId = sessao.Id,
                AssentoId = a.Id,
                Tipo = TipoAlocacao.Acompanhante,
                Parada = modelo.Parada,
                OrdemParada = modelo.OrdemParada,
                CriadoEm = DateTime.UtcNow,
            }));
        }

        db.Alocacoes.RemoveRange(excluir);
        db.Alocacoes.AddRange(incluir);
        return true;
    }
}
