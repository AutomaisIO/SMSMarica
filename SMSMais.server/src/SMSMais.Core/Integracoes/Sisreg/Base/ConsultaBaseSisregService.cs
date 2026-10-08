using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Exames;
using SMSMais.Core.Identidade;
using SMSMais.Core.Institucional;
using SMSMais.Core.Integracoes.Sisreg.Base.Dtos;
using SMSMais.Core.Midias;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.Sisreg.Base;

/// <inheritdoc cref="IConsultaBaseSisregService"/>
public sealed class ConsultaBaseSisregService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IPacienteResolver pacientes,
    IInstituicaoService instituicao,
    IMidiasService midias,
    IFrescorBaseSisregService frescor) : IConsultaBaseSisregService
{
    /// <summary>Teto do período: um ano. Mais que isso vira relatório, não consulta de tela.</summary>
    public const int MaxDiasPeriodo = 366;

    /// <summary>Tamanhos de página aceitos — os mesmos da tela.</summary>
    public static readonly int[] TamanhosPagina = [100, 200, 500];

    /// <summary>
    /// Teto do PDF. O nome do paciente vem do hub FHIR em lotes de 100; acima disso o arquivo
    /// demora e ninguém lê — melhor pedir um recorte menor.
    /// </summary>
    public const int MaxLinhasPdf = 5000;

    /// <summary>
    /// A definição única de "atendimento no recorte" e da sua situação. Os três usos (contagem,
    /// página, PDF) partem daqui, para que a tela e o PDF não discordem do mesmo número.
    ///
    /// <para>A situação segue a régua da ficha do paciente (<c>AgendamentosPacienteService</c>):
    /// <list type="bullet">
    /// <item>chegada: a coluna relida pela varredura manda; sem ela, o "CONFIRMADO" da coluna 34 do
    /// TXT guardado (o "PENDENTE" guardado não vale — costuma ser de antes do atendimento); no
    /// envelope da Consulta de Agendas (JSON), o segmento da situação;</item>
    /// <item>falta: lista oficial de absenteísmo, por código + DIA (o mesmo código pode faltar, ser
    /// remarcado e comparecer na data nova);</item>
    /// <item>passou do dia sem nenhum dos dois = Pendente: a unidade não apontou — não é falta.</item>
    /// </list></para>
    ///
    /// <para>Nenhuma chave literal no SQL: o EF passa o texto por <c>string.Format</c> e quebra em
    /// <c>like '{%'</c>. Por isso "começa com chave" (envelope JSON) é <c>ascii(raw) = 123</c>.</para>
    ///
    /// <para><c>{PERIODO}</c> é trocado por um de dois predicados fechados (ver
    /// <see cref="PredicadoPeriodo"/>) — CASE no WHERE impediria o índice de data.</para>
    /// </summary>
    private const string Base = """
        with base as (
            select s.id, s.paciente_id, s.categoria, s.status, s.codigo_solicitacao,
                   s.data_agendada, s.data_solicitacao, s.unidade_executante_id,
                   coalesce(nullif(btrim(s.procedimento_texto), ''), nullif(btrim(s.especialidade_texto), ''),
                            '(sem procedimento)') proc,
                   (s.data_agendada at time zone 'America/Sao_Paulo')::date dia,
                   s.autorizado_em,
                   coalesce(s.chegada_confirmada_sisreg,
                       case when ascii(s.raw_sisreg) <> 123
                             and upper(btrim(split_part(s.raw_sisreg, ';', 35))) = 'CONFIRMADO' then true end) confirmada,
                   case when ascii(s.raw_sisreg) = 123 then
                       case when s.raw_sisreg ~* '"situacao"\s*:\s*"([^"]*/)?\s*confirmado\s*(/[^"]*)?"' then true
                            when s.raw_sisreg ~* '"situacao"\s*:\s*"([^"]*/)?\s*falta\s*(/[^"]*)?"' then false end
                   end na_tela
            from smsmarica.solicitacao s
            where s.excluido_em is null
              and {PERIODO}
              and (@ve_tudo or s.unidade_executante_id = any(@escopo))
              and (cardinality(@unidades) = 0 or s.unidade_executante_id = any(@unidades))
              and ((@exames and s.categoria <> 1) or (@consultas and s.categoria = 1))
        ),
        sit as (
            select b.*,
                   case
                     when b.autorizado_em is not null or b.status = 3 then 'Compareceu'
                     when b.status = 4 then 'Cancelada'
                     when b.status = 1 then 'NaFila'
                     when b.dia is null or b.dia > @hoje then 'Agendada'
                     when b.confirmada or b.na_tela then 'Compareceu'
                     when b.na_tela = false
                          or exists (select 1 from smsmarica.sisreg_falta f
                                     where f.codigo_solicitacao = b.codigo_solicitacao
                                       and f.data_execucao = b.dia) then 'Faltou'
                     when b.dia < @hoje then 'Pendente'
                     else 'Agendada'
                   end situacao
            from base b
        ),
        filtrado as (
            select * from sit
            where (cardinality(@situacoes) = 0 or situacao = any(@situacoes))
              and (cardinality(@procedimentos) = 0 or proc = any(@procedimentos))
        )
        """;

    private const string SelectLinhas = """
        select f.id "SolicitacaoId",
               coalesce(e.id, f.id) "DetalheId",
               f.paciente_id "PacienteId",
               f.codigo_solicitacao "CodigoSolicitacao",
               f.proc "Procedimento",
               f.categoria "Categoria",
               f.data_agendada "DataAgendada",
               f.data_solicitacao "DataSolicitacao",
               u.nome "UnidadeExecutante",
               f.situacao "Situacao"
        from filtrado f
        left join smsmarica.exame_imagem e on e.solicitacao_id = f.id
        left join smsmarica.unidade u on u.id = f.unidade_executante_id
        """;

    public async Task<ConsultaBaseSisregResultado> BuscarAsync(
        ConsultaBaseSisregFiltro filtro, CancellationToken ct = default)
    {
        Validar(filtro);
        if (!TamanhosPagina.Contains(filtro.Tamanho))
            throw new ValidacaoException("tamanho", "Mostre 100, 200 ou 500 por página.");
        if (filtro.Pagina < 1)
            throw new ValidacaoException("pagina", "A página começa em 1.");

        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        var hoje = FusoBrasilia.HojeEmBrasilia();

        var contagens = await db.Database
            .SqlQueryRaw<ContagemLinha>(
                Montar(filtro.Eixo, """
                    select situacao "Situacao", count(*)::int "Quantidade"
                    from filtrado group by situacao
                    """),
                Parametros(filtro, escopo, hoje))
            .ToListAsync(ct);

        // Pessoas distintas no resultado inteiro — não dá para somar por situação: quem faltou num
        // dia e compareceu no outro contaria duas vezes.
        var pessoas = contagens.Count == 0
            ? 0
            : await db.Database
                .SqlQueryRaw<int>(
                    Montar(filtro.Eixo, """select count(distinct paciente_id)::int "Value" from filtrado"""),
                    Parametros(filtro, escopo, hoje))
                .SingleAsync(ct);

        var total = contagens.Sum(c => c.Quantidade);
        var linhas = total == 0
            ? []
            : await db.Database
                .SqlQueryRaw<AgendamentoLinha>(
                    Montar(filtro.Eixo, SelectLinhas + Ordem(filtro.Eixo) + " limit @limite offset @deslocamento"),
                    [.. Parametros(filtro, escopo, hoje),
                     new NpgsqlParameter("limite", NpgsqlDbType.Integer) { Value = filtro.Tamanho },
                     new NpgsqlParameter("deslocamento", NpgsqlDbType.Integer)
                     {
                         Value = (filtro.Pagina - 1) * filtro.Tamanho,
                     }])
                .ToListAsync(ct);

        var itens = await EnriquecerAsync(linhas, ct);

        var porSituacao = Enum.GetValues<SituacaoAgendamentoSisreg>()
            .Select(s => new ContagemSituacaoDto(
                s, contagens.FirstOrDefault(c => c.Situacao == s.ToString())?.Quantidade ?? 0))
            .Where(c => c.Quantidade > 0)
            .ToList();

        var aviso = await FrescorAsync(filtro, escopo, hoje, ct);
        return new ConsultaBaseSisregResultado(total, pessoas, porSituacao, filtro.Pagina, filtro.Tamanho, itens, aviso);
    }

    public async Task<OpcoesConsultaBaseSisregDto> OpcoesAsync(
        DateOnly inicio, DateOnly fim, EixoDataConsultaSisreg eixo, CancellationToken ct = default)
    {
        // Só o período e o escopo: as listas mostram tudo o que existe no recorte para o usuário
        // escolher, independentemente do que já está marcado.
        var filtro = new ConsultaBaseSisregFiltro(inicio, fim, eixo);
        Validar(filtro);
        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        var hoje = FusoBrasilia.HojeEmBrasilia();

        var unidades = await db.Database
            .SqlQueryRaw<OpcaoUnidadeConsultaDto>(
                Montar(eixo, """
                    select u.id "Id", u.nome "Nome", count(*)::int "Quantidade"
                    from filtrado f join smsmarica.unidade u on u.id = f.unidade_executante_id
                    group by u.id, u.nome order by u.nome
                    """),
                Parametros(filtro, escopo, hoje))
            .ToListAsync(ct);

        var procedimentos = await db.Database
            .SqlQueryRaw<OpcaoProcedimentoConsultaDto>(
                Montar(eixo, """
                    select proc "Nome", count(*)::int "Quantidade"
                    from filtrado group by proc order by proc
                    """),
                Parametros(filtro, escopo, hoje))
            .ToListAsync(ct);

        return new OpcoesConsultaBaseSisregDto(unidades, procedimentos);
    }

    public async Task<byte[]> PdfAsync(ConsultaBaseSisregFiltro filtro, CancellationToken ct = default)
    {
        Validar(filtro);
        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, ct);
        var hoje = FusoBrasilia.HojeEmBrasilia();

        // Um a mais que o teto: basta para saber que estourou, sem trazer a base inteira.
        var linhas = await db.Database
            .SqlQueryRaw<AgendamentoLinha>(
                Montar(filtro.Eixo, SelectLinhas + Ordem(filtro.Eixo) + " limit @limite"),
                [.. Parametros(filtro, escopo, hoje),
                 new NpgsqlParameter("limite", NpgsqlDbType.Integer) { Value = MaxLinhasPdf + 1 }])
            .ToListAsync(ct);

        if (linhas.Count > MaxLinhasPdf)
            throw new ValidacaoException(
                "filtro",
                $"O PDF sai com até {MaxLinhasPdf.ToString("N0", PdfCultura)} atendimentos e este filtro passa disso. "
                + "Diminua o período ou marque menos unidades, situações ou procedimentos.");

        var itens = (await EnriquecerAsync(linhas, ct))
            .OrderBy(i => i.PacienteNome ?? "￿", StringComparer.Create(PdfCultura, ignoreCase: true))
            .ThenBy(i => i.DataAgendada)
            .ToList();

        var nomesUnidades = filtro.UnidadeIds is { Count: > 0 } ids
            ? await db.Unidades.AsNoTracking()
                .Where(u => ids.Contains(u.Id))
                .OrderBy(u => u.Nome)
                .Select(u => u.Nome)
                .ToListAsync(ct)
            : [];

        var aviso = await FrescorAsync(filtro, escopo, hoje, ct);
        var idv = await IdentidadeVisualPdf.ResolverAsync(instituicao, midias, ct);
        return new ConsultaBaseSisregPdf(filtro, itens, nomesUnidades, aviso, DateTime.UtcNow, idv).Gerar();
    }

    /// <summary>
    /// O frescor do MESMO recorte que o usuário está vendo: as unidades marcadas (dentro do escopo
    /// dele) e os dias de agendamento do período. Pelo eixo da solicitação, o agendamento pode cair
    /// em qualquer dia depois do início — confere do início até ontem.
    /// </summary>
    private Task<FrescorBaseSisregDto> FrescorAsync(
        ConsultaBaseSisregFiltro filtro, EscopoUnidadeResultado escopo, DateOnly hoje, CancellationToken ct)
    {
        IReadOnlyCollection<Guid>? unidades = filtro.UnidadeIds is { Count: > 0 } marcadas
            ? (escopo.VeTudo ? marcadas : marcadas.Intersect(escopo.Unidades).ToArray())
            : (escopo.VeTudo ? null : escopo.Unidades);
        var fim = filtro.Eixo == EixoDataConsultaSisreg.Solicitacao ? hoje.AddDays(-1) : filtro.Fim;
        return frescor.ConferirAsync(filtro.Inicio, fim, unidades, ct);
    }

    private static readonly System.Globalization.CultureInfo PdfCultura =
        System.Globalization.CultureInfo.GetCultureInfo("pt-BR");

    private static void Validar(ConsultaBaseSisregFiltro f)
    {
        if (f.Fim < f.Inicio)
            throw new ValidacaoException("periodo", "A data final é anterior à inicial.");
        if (f.Fim.DayNumber - f.Inicio.DayNumber + 1 > MaxDiasPeriodo)
            throw new ValidacaoException("periodo", "O período vai até um ano.");
        if (!f.IncluirExames && !f.IncluirConsultas)
            throw new ValidacaoException("categoria", "Marque Exames, Consultas ou os dois.");
    }

    /// <summary>
    /// O período por um de dois predicados FECHADOS — nunca texto do cliente na consulta. No
    /// agendamento, o dia de Brasília vira o intervalo de instantes UTC correspondente: é assim que
    /// o índice <c>(status, data_agendada)</c> é usado e que a consulta das 22h cai no dia certo.
    /// </summary>
    private static string PredicadoPeriodo(EixoDataConsultaSisreg eixo) => eixo switch
    {
        EixoDataConsultaSisreg.Solicitacao => "s.data_solicitacao between @inicio and @fim",
        _ => "s.data_agendada >= (@inicio::timestamp at time zone 'America/Sao_Paulo') "
             + "and s.data_agendada < ((@fim + 1)::timestamp at time zone 'America/Sao_Paulo')",
    };

    private static string Ordem(EixoDataConsultaSisreg eixo) => eixo switch
    {
        EixoDataConsultaSisreg.Solicitacao => " order by f.data_solicitacao, f.data_agendada nulls last, f.id",
        _ => " order by f.data_agendada, f.id",
    };

    private static string Montar(EixoDataConsultaSisreg eixo, string select) =>
        Base.Replace("{PERIODO}", PredicadoPeriodo(eixo)) + select;

    /// <summary>
    /// Parâmetros novos a cada consulta (um <see cref="DbParameter"/> não pode estar em dois
    /// comandos). Datas como <see cref="DateOnly"/> + tipo explícito: um <c>DateTime</c> viraria
    /// <c>timestamptz</c> e o Npgsql recusaria Kind=Unspecified antes de o SQL rodar.
    /// </summary>
    private static DbParameter[] Parametros(ConsultaBaseSisregFiltro f, EscopoUnidadeResultado escopo, DateOnly hoje) =>
    [
        new NpgsqlParameter("inicio", NpgsqlDbType.Date) { Value = f.Inicio },
        new NpgsqlParameter("fim", NpgsqlDbType.Date) { Value = f.Fim },
        new NpgsqlParameter("hoje", NpgsqlDbType.Date) { Value = hoje },
        new NpgsqlParameter("ve_tudo", NpgsqlDbType.Boolean) { Value = escopo.VeTudo },
        new NpgsqlParameter("escopo", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = escopo.Unidades },
        new NpgsqlParameter("unidades", NpgsqlDbType.Array | NpgsqlDbType.Uuid)
        {
            Value = (f.UnidadeIds ?? []).Distinct().ToArray(),
        },
        new NpgsqlParameter("situacoes", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = (f.Situacoes ?? []).Select(s => s.ToString()).Distinct().ToArray(),
        },
        new NpgsqlParameter("procedimentos", NpgsqlDbType.Array | NpgsqlDbType.Text)
        {
            Value = (f.Procedimentos ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToArray(),
        },
        new NpgsqlParameter("exames", NpgsqlDbType.Boolean) { Value = f.IncluirExames },
        new NpgsqlParameter("consultas", NpgsqlDbType.Boolean) { Value = f.IncluirConsultas },
    ];

    private async Task<List<AgendamentoBaseSisregDto>> EnriquecerAsync(
        List<AgendamentoLinha> linhas, CancellationToken ct)
    {
        if (linhas.Count == 0) return [];
        // Nome e CPF vivem no hub FHIR (ADR-0007/0010); a solicitação guarda só o PacienteId.
        var resumo = await pacientes.ResolverManyAsync(linhas.Select(l => l.PacienteId), ct);
        return linhas.Select(l =>
        {
            resumo.TryGetValue(l.PacienteId, out var p);
            return new AgendamentoBaseSisregDto(
                l.SolicitacaoId,
                l.DetalheId,
                l.PacienteId,
                p?.Nome,
                p?.Cpf,
                l.CodigoSolicitacao,
                l.Procedimento,
                (CategoriaSolicitacao)l.Categoria,
                l.DataAgendada,
                l.DataSolicitacao,
                l.UnidadeExecutante,
                Enum.Parse<SituacaoAgendamentoSisreg>(l.Situacao));
        }).ToList();
    }

    internal sealed record ContagemLinha(string Situacao, int Quantidade);

    internal sealed record AgendamentoLinha(
        Guid SolicitacaoId,
        Guid DetalheId,
        Guid PacienteId,
        string? CodigoSolicitacao,
        string Procedimento,
        int Categoria,
        DateTime? DataAgendada,
        DateOnly? DataSolicitacao,
        string? UnidadeExecutante,
        string Situacao);
}
