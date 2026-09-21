using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using Tipo = SMSMais.Data.Entities.Enums.TipoEventoAtendimentoConfirmacao;

namespace SMSMais.Core.Notificacoes.Confirmacoes;

public interface IConfirmacoesEquipeService
{
    /// <summary>Produção por atendente no período (dias de Brasília, inclusivos).</summary>
    Task<EquipeConfirmacoesDto> ObterAsync(DateOnly de, DateOnly ate, CancellationToken ct = default);

    /// <summary>Os atos de uma atendente no período, do mais recente para o mais antigo.</summary>
    Task<IReadOnlyList<AtoAtendenteDto>> AtosAsync(Guid usuarioId, DateOnly de, DateOnly ate, CancellationToken ct = default);
}

/// <summary>
/// Aba Equipe da tela de Confirmações: quem fez o quê, lido da trilha append-only
/// <c>atendimento_confirmacao_evento</c>. Só leitura, e só conta ato com autor — o que o sistema
/// fez sozinho não é produção de ninguém.
///
/// <para>Calcula em memória sobre uma projeção enxuta: a trilha de um período é de poucos
/// milhares de linhas, e mediana por pessoa com "evento anterior da mesma ficha" fica ilegível
/// em SQL. Os dias são de Brasília (ver <see cref="FusoBrasilia"/>).</para>
/// </summary>
public sealed class ConfirmacoesEquipeService(SmsMaisDbContext db) : IConfirmacoesEquipeService
{
    private const int MaxDiasPeriodo = 400;

    /// <summary>Intervalo entre desfechos acima disto é pausa (almoço, reunião), não ritmo.</summary>
    private const int PausaMin = 60;

    private static readonly Tipo[] Pegar = [Tipo.Atendido, Tipo.Assumido, Tipo.Retomado];

    private static readonly Tipo[] Desfecho =
    [
        Tipo.Confirmado, Tipo.Cancelado, Tipo.EnviadoPendente, Tipo.ContatoErrado,
        Tipo.ContatoCorrigido, Tipo.PedidoCancelamentoDesfeito,
    ];

    private sealed record Linha(Guid AtendimentoId, Tipo Tipo, Guid? Ator, Guid? Para, DateTime OcorridoEm);

    public async Task<EquipeConfirmacoesDto> ObterAsync(DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var (inicio, fim) = Periodo(ref de, ref ate);

        var noPeriodo = await db.AtendimentoConfirmacaoEventos.AsNoTracking()
            .Where(e => e.AtorUsuarioId != null && e.OcorridoEm >= inicio && e.OcorridoEm < fim)
            .Select(e => new Linha(e.AtendimentoId, e.Tipo, e.AtorUsuarioId, e.ParaUsuarioId, e.OcorridoEm))
            .ToListAsync(ct);

        // O "pegou" que antecede um desfecho pode ter sido antes do período (ficha estacionada na
        // sexta, resolvida na segunda): a história das fichas tocadas vem inteira.
        var fichas = noPeriodo.Select(e => e.AtendimentoId).Distinct().ToArray();
        var historia = fichas.Length == 0
            ? []
            : (await db.AtendimentoConfirmacaoEventos.AsNoTracking()
                .Where(e => fichas.Contains(e.AtendimentoId) && e.OcorridoEm < fim)
                .Select(e => new Linha(e.AtendimentoId, e.Tipo, e.AtorUsuarioId, e.ParaUsuarioId, e.OcorridoEm))
                .ToListAsync(ct))
              .GroupBy(e => e.AtendimentoId)
              .ToDictionary(g => g.Key, g => g.OrderBy(e => e.OcorridoEm).ToList());

        var ids = noPeriodo.Select(e => e.Ator!.Value).Distinct().ToList();
        var nomes = await db.Usuarios.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NomeCompleto, ct);

        var atendentes = noPeriodo
            .GroupBy(e => e.Ator!.Value)
            .Select(g => Producao(g.Key, nomes.GetValueOrDefault(g.Key) ?? "(usuário removido)", [.. g], historia))
            .OrderByDescending(a => a.Desfechos).ThenByDescending(a => a.Pegou).ThenBy(a => a.Nome)
            .ToList();

        // O total NÃO é a soma das medianas: recalcula sobre todos os atos. O ritmo da equipe é o
        // ritmo de cada uma (intervalos por pessoa), agregado — não o intervalo entre colegas.
        var total = Producao(Guid.Empty, "Equipe", noPeriodo, historia) with
        {
            RitmoMin = Mediana([.. noPeriodo.GroupBy(e => e.Ator!.Value).SelectMany(g => Intervalos([.. g]))]),
            DiasAtivos = noPeriodo.Select(e => Dia(e.OcorridoEm)).Distinct().Count(),
        };

        var porDia = noPeriodo.Where(e => Desfecho.Contains(e.Tipo))
            .GroupBy(e => Dia(e.OcorridoEm))
            .Select(g => new EquipeDiaDto(g.Key,
                g.Count(e => e.Tipo == Tipo.Confirmado),
                g.Count(e => e.Tipo == Tipo.Cancelado),
                g.Count(e => e.Tipo != Tipo.Confirmado && e.Tipo != Tipo.Cancelado)))
            .OrderBy(d => d.Dia)
            .ToList();

        var trilhaDesde = await db.AtendimentoConfirmacaoEventos.AsNoTracking()
            .OrderBy(e => e.OcorridoEm).Select(e => (DateTime?)e.OcorridoEm).FirstOrDefaultAsync(ct);

        return new EquipeConfirmacoesDto(de, ate, trilhaDesde, PausaMin, total, atendentes, porDia);
    }

    public async Task<IReadOnlyList<AtoAtendenteDto>> AtosAsync(
        Guid usuarioId, DateOnly de, DateOnly ate, CancellationToken ct = default)
    {
        var (inicio, fim) = Periodo(ref de, ref ate);

        var atos = await db.AtendimentoConfirmacaoEventos.AsNoTracking()
            .Where(e => e.AtorUsuarioId == usuarioId && e.OcorridoEm >= inicio && e.OcorridoEm < fim)
            .OrderByDescending(e => e.OcorridoEm)
            .Take(500)
            .Select(e => new
            {
                e.Tipo, e.OcorridoEm, e.Observacao,
                e.Atendimento!.SolicitacaoId,
                e.Atendimento.Solicitacao!.CodigoSolicitacao,
                Procedimento = e.Atendimento.Solicitacao.EspecialidadeTexto ?? e.Atendimento.Solicitacao.ProcedimentoTexto,
            })
            .ToListAsync(ct);

        return [.. atos.Select(a => new AtoAtendenteDto(
            a.Tipo.ToString(), a.OcorridoEm, a.SolicitacaoId, a.CodigoSolicitacao, a.Procedimento, a.Observacao))];
    }

    // ------------------------------------------------------------------ cálculo

    private static AtendenteProducaoDto Producao(
        Guid usuarioId, string nome, List<Linha> atos, Dictionary<Guid, List<Linha>> historia)
    {
        int N(params Tipo[] tipos) => atos.Count(e => tipos.Contains(e.Tipo));

        // Pegar → desfecho: para cada desfecho, o último momento em que a ficha passou para a mão
        // de quem o deu (pegou ela mesma, ou recebeu por transferência).
        var tempos = new List<double>();
        foreach (var d in atos.Where(e => Desfecho.Contains(e.Tipo)))
        {
            if (!historia.TryGetValue(d.AtendimentoId, out var h)) continue;
            var pegou = h.LastOrDefault(e => e.OcorridoEm <= d.OcorridoEm && e != d
                && ((Pegar.Contains(e.Tipo) && e.Ator == d.Ator)
                    || (e.Tipo == Tipo.Transferido && e.Para == d.Ator)));
            if (pegou is not null) tempos.Add((d.OcorridoEm - pegou.OcorridoEm).TotalMinutes);
        }

        return new AtendenteProducaoDto(
            usuarioId, nome,
            Pegou: N(Pegar),
            Confirmou: N(Tipo.Confirmado),
            Cancelou: N(Tipo.Cancelado),
            CancelouNoSisreg: N(Tipo.CanceladoNoSisreg),
            SisregRecusou: N(Tipo.SisregRecusouCancelamento),
            AvisouPaciente: N(Tipo.PacienteAvisadoCancelamento),
            Pendente: N(Tipo.EnviadoPendente),
            ContatoErrado: N(Tipo.ContatoErrado),
            ContatoCorrigido: N(Tipo.ContatoCorrigido),
            PedidoDesfeito: N(Tipo.PedidoCancelamentoDesfeito),
            Liberou: N(Tipo.Liberado),
            Transferiu: N(Tipo.Transferido),
            Desfechos: N(Desfecho),
            TempoAteDesfechoMin: Mediana(tempos),
            RitmoMin: Mediana(Intervalos(atos)),
            DiasAtivos: atos.Select(e => Dia(e.OcorridoEm)).Distinct().Count(),
            PrimeiraAcaoEm: atos.Count > 0 ? atos.Min(e => e.OcorridoEm) : null,
            UltimaAcaoEm: atos.Count > 0 ? atos.Max(e => e.OcorridoEm) : null);
    }

    /// <summary>Minutos entre desfechos consecutivos de UMA pessoa no mesmo dia, sem as pausas.</summary>
    private static List<double> Intervalos(List<Linha> atosDeUmaPessoa)
    {
        var intervalos = new List<double>();
        foreach (var dia in atosDeUmaPessoa.Where(e => Desfecho.Contains(e.Tipo)).GroupBy(e => Dia(e.OcorridoEm)))
        {
            var ordenados = dia.OrderBy(e => e.OcorridoEm).ToList();
            for (var i = 1; i < ordenados.Count; i++)
            {
                var min = (ordenados[i].OcorridoEm - ordenados[i - 1].OcorridoEm).TotalMinutes;
                if (min <= PausaMin) intervalos.Add(min);
            }
        }
        return intervalos;
    }

    private static double? Mediana(List<double> valores)
    {
        if (valores.Count == 0) return null;
        valores.Sort();
        var meio = valores.Count / 2;
        var m = valores.Count % 2 == 1 ? valores[meio] : (valores[meio - 1] + valores[meio]) / 2;
        return Math.Round(m, 1);
    }

    private static DateOnly Dia(DateTime utc) => DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(utc));

    /// <summary>Dias de Brasília [de, ate] → instantes UTC [início, fim).</summary>
    private static (DateTime Inicio, DateTime Fim) Periodo(ref DateOnly de, ref DateOnly ate)
    {
        if (ate < de) (de, ate) = (ate, de);
        if (ate.DayNumber - de.DayNumber + 1 > MaxDiasPeriodo)
            throw new ValidacaoException("periodo", $"O período não pode exceder {MaxDiasPeriodo} dias.");
        return (FusoBrasilia.DeBrasiliaParaUtc(de.ToDateTime(TimeOnly.MinValue)),
            FusoBrasilia.DeBrasiliaParaUtc(ate.AddDays(1).ToDateTime(TimeOnly.MinValue)));
    }
}
