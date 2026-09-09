using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.SisregWeb.Ofertas.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.Ofertas;

/// <summary>
/// <b>O que abriu</b> no SISREG — a leitura de oportunidade do dado que já coletamos.
///
/// <para>São duas coisas distintas, com urgências distintas, e o operador precisa das duas na
/// mesma tela:</para>
/// <list type="number">
///   <item><b>Agenda nova</b>: um bloco de escala que passou a existir ("abriu espirometria no CDT,
///   32 vagas"). Vem de <c>sisreg_escala</c>, pela data em que NÓS a vimos pela primeira vez
///   (<c>CriadoEm</c>) — não há coluna do SISREG dizendo "nasci agora".</item>
///   <item><b>Vaga liberada</b>: um agendamento que sumiu do export porque alguém cancelou lá.
///   Vem de <c>sisreg_alteracao_agenda</c> tipo <see cref="TipoAlteracaoAgenda.Ausente"/> — o
///   MESMO registro que a fila de alterações mostra como "confirme o cancelamento". É o mesmo
///   fato lido do outro lado: para quem perdeu, é cancelamento; para quem espera, é vaga.</item>
/// </list>
///
/// <para><b>Por que a espera entra aqui.</b> "4 vagas de ecocardiograma" é burocracia; "4 vagas
/// numa fila que espera 466 dias" muda o que a pessoa faz agora. A espera é o que ordena a tela.
/// <b>Ressalva que precisa aparecer na interface</b>: é a espera de quem JÁ foi atendido
/// (<c>data_agendada − data_solicitacao</c>), não a de quem está esperando — a fila viva do SISREG
/// não é lida por nós. Serve para PRIORIZAR entre procedimentos, não para prometer prazo.</para>
///
/// <para><b>Somente leitura.</b> Nada aqui escreve, nada aqui fala com o SISREG.</para>
/// </summary>
public interface IOfertasSisregService
{
    /// <param name="dias">Janela de "novidade" das agendas: quantos dias atrás olhar o
    /// <c>CriadoEm</c> da escala.</param>
    Task<OfertasSisregDto> ListarAsync(int dias, CancellationToken cancellationToken = default);
}

public sealed class OfertasSisregService(SmsMaisDbContext db) : IOfertasSisregService
{
    /// <summary>
    /// Espera acima da qual a oferta é destacada. Seis meses não é um número clínico — é o ponto a
    /// partir do qual a fila deixou de ser "demora" e virou outra coisa.
    /// </summary>
    private const int DiasEsperaUrgente = 180;

    /// <summary>
    /// Uma vaga que vaga para daqui a menos disto morre se ninguém agir hoje. É o que separa
    /// "aproveitável" de "registro histórico".
    /// </summary>
    private const int DiasVagaPerecivel = 7;

    public async Task<OfertasSisregDto> ListarAsync(int dias, CancellationToken cancellationToken = default)
    {
        var janela = Math.Clamp(dias, 1, 90);
        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var desde = DateTime.UtcNow.AddDays(-janela);

        var agendas = await AgendasNovasAsync(desde, hoje, cancellationToken);
        var vagas = await VagasLiberadasAsync(hoje, cancellationToken);

        // A espera é calculada UMA vez, só para os procedimentos que aparecem na tela. Rodar o
        // percentil sobre a base inteira (1M linhas) para depois jogar 99% fora seria caro à toa.
        var codigos = agendas.Select(a => a.ProcedimentoCodigo)
            .Concat(vagas.Select(v => v.ProcedimentoCodigo))
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var espera = await EsperaPorProcedimentoAsync(codigos, cancellationToken);

        return new OfertasSisregDto(
            [.. agendas.Select(a => a with { EsperaMedianaDias = Espera(espera, a.ProcedimentoCodigo) })
                .OrderByDescending(a => a.EsperaMedianaDias ?? -1)
                .ThenByDescending(a => a.Vagas)],
            [.. vagas.Select(v => v with { EsperaMedianaDias = Espera(espera, v.ProcedimentoCodigo) })
                .OrderBy(v => v.DataAgendada)],
            janela,
            DiasEsperaUrgente,
            DiasVagaPerecivel);
    }

    private static int? Espera(IReadOnlyDictionary<string, int> mapa, string? codigo) =>
        codigo is not null && mapa.TryGetValue(codigo, out var d) ? d : null;

    /// <summary>
    /// Blocos de escala vistos pela primeira vez dentro da janela, agrupados por unidade ×
    /// procedimento.
    ///
    /// <para><b>Agrupar é o ponto.</b> Uma agenda semanal nasce como várias linhas — uma por dia da
    /// semana, às vezes uma por profissional. As 32 vagas de espirometria do CDT são 7 escalas; sem
    /// agrupar, a tela mostraria 7 "novidades" para um fato só e o operador aprenderia a ignorá-la.</para>
    ///
    /// <para>Só entra escala <b>ativa, não ausente e ainda vigente</b>: bloco que já venceu não é
    /// oferta, é histórico.</para>
    /// </summary>
    private async Task<List<AgendaNovaDto>> AgendasNovasAsync(
        DateTime desde, DateOnly hoje, CancellationToken ct)
    {
        // Busca CHAPADA e agrupa em memória, de propósito. Agrupar no banco parece mais barato mas
        // não compila: `Distinct()` sobre uma coleção dentro da projeção de um GroupBy não tem
        // tradução no EF ("Unable to translate a collection subquery in a projection") — e isso só
        // aparece em RUNTIME, com 500 na cara do operador. O conjunto aqui é pequeno por
        // construção (só escalas vistas nos últimos N dias: 32 numa janela de 5 dias, medido em
        // 09/09/2026), então trazer as linhas custa menos que a ginástica para o SQL aceitar.
        var linhas = await db.SisregEscalas.AsNoTracking()
            .Where(e => e.CriadoEm >= desde
                && e.Status == StatusEscalaSisreg.Ativa
                && !e.Ausente
                && e.VigenciaFim >= hoje)
            .Select(e => new
            {
                e.UnidadeId,
                UnidadeNome = e.Unidade!.Nome,
                e.ProcedimentoCodigo,
                e.ProcedimentoNome,
                e.CboDescricao,
                e.DiaSemana,
                e.VagasTotal,
                e.VigenciaInicio,
                e.VigenciaFim,
                e.CriadoEm,
            })
            .ToListAsync(ct);

        return [.. linhas
            .GroupBy(e => new { e.UnidadeId, e.ProcedimentoCodigo })
            .Select(g => new AgendaNovaDto(
                g.Key.UnidadeId,
                g.First().UnidadeNome,
                g.Key.ProcedimentoCodigo,
                g.First().ProcedimentoNome,
                g.First().CboDescricao,
                g.Count(),
                g.Sum(e => e.VagasTotal),
                g.Min(e => e.VigenciaInicio),
                g.Max(e => e.VigenciaFim),
                // Dias da semana em que essa agenda abre — é o que diz "toda terça" vs "um dia só".
                [.. g.Select(e => (int)e.DiaSemana).Distinct().Order()],
                g.Min(e => e.CriadoEm),
                null))];
    }

    /// <summary>
    /// Agendamentos que sumiram do SISREG e cuja data ainda não passou — as vagas que dá para
    /// reaproveitar.
    ///
    /// <para>Só <b>pendentes</b> (<c>TratadaEm == null</c>): tratada quer dizer que alguém já
    /// decidiu o que fazer, e continuar oferecendo o que já foi resolvido é como a fila de
    /// alterações vira ruído.</para>
    ///
    /// <para><b>Não afirmamos que a vaga está livre</b> — afirmamos que o SISREG parou de mostrar
    /// aquele agendamento. Confirmar é trabalho de gente, e a tela diz isso.</para>
    /// </summary>
    private async Task<List<VagaLiberadaDto>> VagasLiberadasAsync(DateOnly hoje, CancellationToken ct)
    {
        var inicioUtc = FusoBrasilia.InicioDoDiaAtualEmUtc();

        return await db.SisregAlteracoesAgenda.AsNoTracking()
            .Where(a => a.Tipo == TipoAlteracaoAgenda.Ausente && a.TratadaEm == null)
            .Join(db.Solicitacoes.AsNoTracking().Where(s => s.ExcluidoEm == null && s.DataAgendada >= inicioUtc),
                a => a.SolicitacaoId, s => s.Id, (a, s) => new { a, s })
            .Select(x => new VagaLiberadaDto(
                x.a.Id,
                x.s.Id,
                x.s.UnidadeExecutanteId,
                x.s.UnidadeExecutante!.Nome,
                x.s.ProcedimentoCodigoSisreg,
                x.s.ProcedimentoTexto,
                x.s.DataAgendada!.Value,
                x.a.DetectadaEm,
                null))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Mediana de <c>data_agendada − data_solicitacao</c> por procedimento, sobre o que foi
    /// agendado neste ano.
    ///
    /// <para><b>O que este número é e o que não é.</b> É a espera de quem JÁ conseguiu data —
    /// enviesada para baixo por construção, porque quem nunca foi atendido não entra na conta.
    /// Serve para ordenar procedimentos entre si, não para prometer prazo a ninguém.</para>
    ///
    /// <para>Feito em SQL cru porque <c>percentile_disc</c> não tem tradução no LINQ. Sem SQL cru
    /// seria trazer as linhas para a memória — e são centenas de milhares.</para>
    /// </summary>
    private async Task<Dictionary<string, int>> EsperaPorProcedimentoAsync(
        IReadOnlyList<string> codigos, CancellationToken ct)
    {
        if (codigos.Count == 0) return [];

        // `DateOnly` e não `DateTime`: DateTime Unspecified quebra em runtime no Npgsql com coluna
        // de data — armadilha já paga uma vez neste projeto.
        var inicioAno = new DateOnly(FusoBrasilia.ParaExibicao(DateTime.UtcNow).Year, 1, 1);

        var linhas = await db.Database
            .SqlQuery<EsperaLinha>($"""
                SELECT s.procedimento_codigo_sisreg AS "Codigo",
                       percentile_disc(0.5) WITHIN GROUP (
                           ORDER BY (s.data_agendada::date - s.data_solicitacao::date))::int AS "Dias"
                FROM smsmarica.solicitacao s
                WHERE s.procedimento_codigo_sisreg = ANY({codigos})
                  AND s.excluido_em IS NULL
                  AND s.data_solicitacao IS NOT NULL
                  AND s.data_agendada >= {inicioAno}
                GROUP BY s.procedimento_codigo_sisreg
                """)
            .ToListAsync(ct);

        return linhas
            .Where(l => l.Codigo is not null && l.Dias is not null)
            .ToDictionary(l => l.Codigo!, l => l.Dias!.Value, StringComparer.Ordinal);
    }

    private sealed record EsperaLinha(string? Codigo, int? Dias);
}
