using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.SisregWeb.Indicadores;
using SMSMais.Core.Integracoes.SisregWeb.Varredura;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.Sisreg.Base;

/// <summary>Configuração da conferência de frescor da base do SISREG e do aviso diário.</summary>
public sealed class FrescorBaseSisregOpcoes
{
    public const string Secao = "Sisreg:Frescor";

    /// <summary>
    /// Chegada relida há mais que isto conta como atrasada. A varredura relê toda noite, então 30 h só
    /// estoura quando a rotina deixou de ler — não por esperar a próxima noite.
    /// </summary>
    public int HorasParaAtraso { get; set; } = 30;

    /// <summary>
    /// Lista de faltas RECENTE (semana com menos de 30 dias) lida há mais que isto conta como atrasada.
    /// O coletor relê esses dias de hora em hora entre 01:20 e 18:00; o SISREG corta cerca de metade
    /// das leituras, mas 6 rodadas seguidas sem fechar já é problema.
    /// </summary>
    public int HorasParaAtrasoFaltas { get; set; } = 6;

    /// <summary>Primeira conferência do dia (Brasília) — antes de a equipe abrir o relatório.</summary>
    public TimeOnly HoraDoAviso { get; set; } = new(7, 0);

    /// <summary>Última conferência do dia (Brasília). Entre as duas, confere de hora em hora.</summary>
    public TimeOnly HoraFimDoAviso { get; set; } = new(18, 0);

    /// <summary>Quantos dias para trás o aviso diário confere.</summary>
    public int DiasDoAviso { get; set; } = 45;

    /// <summary>Desliga só o aviso diário; a faixa da Consulta continua.</summary>
    public bool AvisoLigado { get; set; } = true;
}

/// <param name="Agendamentos">Agendamentos do dia que passaram sem chegada confirmada — os que o
/// relatório mostra como "Pendente de atualização" enquanto as faltas do dia não forem lidas.</param>
/// <param name="UltimaLeitura">Última leitura completa da lista de faltas do dia (null = nunca lida).</param>
public sealed record DiaSemFaltasDto(DateOnly Dia, int Agendamentos, DateTime? UltimaLeitura);

/// <param name="Agendamentos">Agendamentos passados em aberto da unidade, dentro da janela relida.</param>
/// <param name="UltimaLeitura">Última vez que a varredura releu a chegada desses agendamentos (null = nunca).</param>
public sealed record ChegadaAtrasadaDto(Guid UnidadeId, string Unidade, int Agendamentos, DateTime? UltimaLeitura);

/// <summary>
/// O que a base do SISREG ainda não sabe de um período. Vazio = em dia: o "Pendente de atualização"
/// que aparecer é mesmo a unidade sem apontar nada.
/// </summary>
public sealed record FrescorBaseSisregDto(
    IReadOnlyList<DiaSemFaltasDto> DiasSemFaltas,
    IReadOnlyList<ChegadaAtrasadaDto> ChegadasAtrasadas,
    int HorasParaAtraso)
{
    public bool EmDia => DiasSemFaltas.Count == 0 && ChegadasAtrasadas.Count == 0;

    public static FrescorBaseSisregDto Vazio(int horas) => new([], [], horas);

    /// <summary>Identidade de cada pendência ("faltas:2026-09-14", "chegada:{unidade}") — é o que diz
    /// se apareceu problema NOVO entre uma conferência e outra.</summary>
    public IReadOnlySet<string> Pendencias() =>
        DiasSemFaltas.Select(d => $"faltas:{d.Dia:yyyy-MM-dd}")
            .Concat(ChegadasAtrasadas.Select(c => $"chegada:{c.UnidadeId}"))
            .ToHashSet();
}

/// <summary>
/// Confere se a base tem o que o SISREG sabe sobre os agendamentos já passados. A situação de um
/// atendimento (Consulta e ficha do paciente) sai de duas leituras que NÃO vêm com o agendamento:
/// <list type="bullet">
/// <item><b>a lista de faltas</b> do dia (coletor dos Indicadores, <c>sisreg_indicador_coleta</c>);</item>
/// <item><b>a chegada</b> relida pela varredura noturna (<c>solicitacao.chegada_sisreg_lida_em</c>).</item>
/// </list>
/// Sem a primeira, quem faltou aparece "Pendente"; sem a segunda, quem veio também.
///
/// <para><b>Por que existe (07/10/2026).</b> As faltas de 09–23/09 só entraram na base no dia 07/10,
/// e 14/09 nem assim — a equipe tirou o relatório de pendentes com quem faltou contado como pendente,
/// e ninguém foi avisado: cada falha do coletor era um aviso no log que não chega ao celular. Esta
/// conferência olha a CONSEQUÊNCIA (dia sem lista, unidade sem releitura), não a falha de cada
/// requisição — o SISREG corta uma leitura em cada duas sem que isso seja problema, porque a próxima
/// rodada fecha.</para>
/// </summary>
public interface IFrescorBaseSisregService
{
    /// <param name="unidades">Unidades executantes a conferir; null = todas.</param>
    Task<FrescorBaseSisregDto> ConferirAsync(
        DateOnly inicio, DateOnly fim, IReadOnlyCollection<Guid>? unidades, CancellationToken ct);
}

public sealed class FrescorBaseSisregService(
    SmsMaisDbContext db,
    IOptions<FrescorBaseSisregOpcoes> opcoes,
    IOptions<ColetaIndicadoresOpcoes> coleta,
    IOptions<VarreduraSisregOpcoes> varredura) : IFrescorBaseSisregService
{
    /// <summary>
    /// Dias com agendamento passado ainda em aberto que não têm a lista de faltas lida — ou que têm
    /// uma leitura recente (semana com menos de 30 dias, relida de hora em hora) que parou de ser
    /// renovada. A leitura oficial (coletor 1) é definitiva: lida uma vez, está lida.
    /// </summary>
    private const string SqlDias = """
        with dias as (
            select (s.data_agendada at time zone 'America/Sao_Paulo')::date dia, count(*)::int n
            from smsmarica.solicitacao s
            where s.excluido_em is null and s.raw_sisreg is not null and s.codigo_solicitacao is not null
              and s.status = 2 and s.autorizado_em is null
              and coalesce(s.chegada_confirmada_sisreg, false) = false
              and s.data_agendada >= @inicio_utc and s.data_agendada < @fim_utc
              and (@todas or s.unidade_executante_id = any(@unidades))
            group by 1
        )
        select d.dia "Dia", d.n "Agendamentos", l.ultima "UltimaLeitura"
        from dias d
        left join lateral (
            select max(c.lido_em) ultima, bool_or(c.coletor = 1) oficial
            from smsmarica.sisreg_indicador_coleta c
            where c.coletor in (1, 5) and c.lido_em is not null
              and d.dia between c.janela_inicio and c.janela_fim
        ) l on true
        where l.ultima is null
           or (not l.oficial and d.dia > @limite_recente and l.ultima < @velho)
        order by d.dia
        """;

    /// <summary>
    /// Unidades cujos agendamentos passados em aberto não foram relidos pela varredura dentro do prazo.
    /// Quem está confirmado não é recarimbado (estado final), por isso só o "em aberto" mede o frescor.
    /// </summary>
    private const string SqlChegadas = """
        select u.id "UnidadeId", u.nome "Unidade", count(*)::int "Agendamentos",
               max(s.chegada_sisreg_lida_em) "UltimaLeitura"
        from smsmarica.solicitacao s
        join smsmarica.unidade u on u.id = s.unidade_executante_id
        where s.excluido_em is null and s.raw_sisreg is not null and s.codigo_solicitacao is not null
          and s.status = 2 and s.autorizado_em is null
          and coalesce(s.chegada_confirmada_sisreg, false) = false
          and s.data_agendada >= @inicio_utc and s.data_agendada < @fim_utc
          and (@todas or s.unidade_executante_id = any(@unidades))
        group by u.id, u.nome
        having max(s.chegada_sisreg_lida_em) is null or max(s.chegada_sisreg_lida_em) < @velho
        order by u.nome
        """;

    public async Task<FrescorBaseSisregDto> ConferirAsync(
        DateOnly inicio, DateOnly fim, IReadOnlyCollection<Guid>? unidades, CancellationToken ct)
    {
        var horas = Math.Max(1, opcoes.Value.HorasParaAtraso);
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var ontem = hoje.AddDays(-1);
        if (fim > ontem) fim = ontem;
        if (fim < inicio) return FrescorBaseSisregDto.Vazio(horas);

        var velho = DateTime.UtcNow.AddHours(-horas);
        var faltasVelhas = DateTime.UtcNow.AddHours(-Math.Max(1, opcoes.Value.HorasParaAtrasoFaltas));
        var todas = unidades is null;
        var ids = unidades?.Distinct().ToArray() ?? [];

        var dias = await db.Database
            .SqlQueryRaw<DiaSemFaltasDto>(SqlDias,
                Parametros(inicio, fim, todas, ids,
                    new NpgsqlParameter("limite_recente", NpgsqlDbType.Date)
                    {
                        Value = hoje.AddDays(-Math.Max(1, coleta.Value.DiasParaFaltas)),
                    },
                    new NpgsqlParameter("velho", NpgsqlDbType.TimestampTz) { Value = faltasVelhas }))
            .ToListAsync(ct);

        // A chegada só é relida nos últimos N dias (a varredura): mais velho que isso é estado final.
        List<ChegadaAtrasadaDto> chegadas = [];
        var diasDeChegada = varredura.Value.DiasDeChegada;
        if (diasDeChegada > 0)
        {
            var desde = hoje.AddDays(-(diasDeChegada - 1));
            var ini = inicio > desde ? inicio : desde;
            if (ini <= fim)
            {
                chegadas = await db.Database
                    .SqlQueryRaw<ChegadaAtrasadaDto>(SqlChegadas,
                        Parametros(ini, fim, todas, ids,
                            new NpgsqlParameter("velho", NpgsqlDbType.TimestampTz) { Value = velho }))
                    .ToListAsync(ct);
            }
        }

        return new FrescorBaseSisregDto(dias, chegadas, horas);
    }

    private static object[] Parametros(
        DateOnly inicio, DateOnly fim, bool todas, Guid[] unidades, params NpgsqlParameter[] extras) =>
    [
        new NpgsqlParameter("inicio_utc", NpgsqlDbType.TimestampTz)
        {
            Value = FusoBrasilia.DeBrasiliaParaUtc(inicio.ToDateTime(TimeOnly.MinValue)),
        },
        new NpgsqlParameter("fim_utc", NpgsqlDbType.TimestampTz)
        {
            Value = FusoBrasilia.DeBrasiliaParaUtc(fim.AddDays(1).ToDateTime(TimeOnly.MinValue)),
        },
        new NpgsqlParameter("todas", NpgsqlDbType.Boolean) { Value = todas },
        new NpgsqlParameter("unidades", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = unidades },
        .. extras,
    ];
}
