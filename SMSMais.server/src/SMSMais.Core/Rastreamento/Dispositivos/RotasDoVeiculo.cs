using SMSMais.Core.Common.Tempo;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Rastreamento.Dispositivos;

/// <summary>
/// "Rotas do dia" de um veículo, a mesma regra para quem pode entrar no tablet (ADR-0073) e para o
/// que o tablet mostra: rotas de hoje no fuso de Brasília, sem as canceladas, mais a de ontem que
/// ainda está em andamento (volta que passa da meia-noite).
/// </summary>
internal static class RotasDoVeiculo
{
    public static IQueryable<RotaDiaria> DoDia(SmsMaisDbContext db, Guid veiculoId)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var ontem = hoje.AddDays(-1);
        return db.Rotas.Where(r =>
            r.VeiculoId == veiculoId && r.Status != StatusRota.Cancelada
            && (r.Data == hoje || (r.Data == ontem && r.Status == StatusRota.EmAndamento)));
    }
}
