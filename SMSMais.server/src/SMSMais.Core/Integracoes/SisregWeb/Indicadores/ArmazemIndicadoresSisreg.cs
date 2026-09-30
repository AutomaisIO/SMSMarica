using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Indicadores;

/// <summary>
/// Onde o coletor e a conciliação gravam o que leram para os Indicadores de Regulação. Interface para
/// que a lógica de coleta (o que conta como leitura completa, quando recusar) seja testável sem banco.
/// </summary>
public interface IArmazemIndicadoresSisreg
{
    Task<int> ContarFaltasAsync(DateOnly inicio, DateOnly fim, CancellationToken ct);

    /// <summary>Troca as faltas de [início, fim] pelas lidas, numa transação.</summary>
    Task SubstituirFaltasAsync(DateOnly inicio, DateOnly fim, IReadOnlyList<FaltaLidaSisreg> faltas, CancellationToken ct);

    Task<int> ContarPpiAsync(DateOnly competencia, CancellationToken ct);

    /// <summary>Troca as cotas da competência pelas lidas, numa transação.</summary>
    Task SubstituirPpiAsync(DateOnly competencia, IReadOnlyList<CotaPpiLidaSisreg> cotas, CancellationToken ct);

    /// <summary>Upsert-only (código + instante do cancelamento). Devolve quantas eram novas.</summary>
    Task<int> GravarCanceladasAsync(IReadOnlyList<MarcacaoCanceladaLidaSisreg> canceladas, CancellationToken ct);

    /// <summary>Upsert-only (código + situação). Devolve quantas eram novas.</summary>
    Task<int> GravarDesfechosAsync(
        string cnesSolicitante, IReadOnlyList<(DesfechoLidoSisreg Linha, SituacaoDesfechoSisreg Situacao)> desfechos,
        CancellationToken ct);

    /// <summary>
    /// Registra (ou atualiza) uma janela CONCLUÍDA — usado pela conciliação diária, que lê fora do
    /// cursor do coletor mas prova a leitura do mesmo jeito.
    /// </summary>
    Task RegistrarJanelaConcluidaAsync(
        ColetorIndicadorSisreg coletor, DateOnly inicio, DateOnly fim, string escopo, int linhas, CancellationToken ct);
}

public sealed class ArmazemIndicadoresSisreg(SmsMaisDbContext db) : IArmazemIndicadoresSisreg
{
    public Task<int> ContarFaltasAsync(DateOnly inicio, DateOnly fim, CancellationToken ct) =>
        db.SisregFaltasOficiais.CountAsync(f => f.DataExecucao >= inicio && f.DataExecucao <= fim, ct);

    public async Task SubstituirFaltasAsync(
        DateOnly inicio, DateOnly fim, IReadOnlyList<FaltaLidaSisreg> faltas, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        // Uma linha por (código, data): a lista às vezes repete a mesma falta em páginas vizinhas.
        var unicas = faltas
            .Where(f => f.DataExecucao >= inicio && f.DataExecucao <= fim)
            .GroupBy(f => (f.Codigo, f.DataExecucao))
            .Select(g => g.First())
            .ToList();

        // Transação manual só dentro da estratégia de execução (EnableRetryOnFailure está ligado).
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.SisregFaltasOficiais
                .Where(f => f.DataExecucao >= inicio && f.DataExecucao <= fim)
                .ExecuteDeleteAsync(ct);
            db.SisregFaltasOficiais.AddRange(unicas.Select(f => new SisregFaltaOficial
            {
                Id = Guid.NewGuid(),
                CodigoSolicitacao = Cortar(f.Codigo, 20)!,
                DataExecucao = f.DataExecucao,
                Hora = Cortar(f.Hora, 10),
                Procedimento = Cortar(f.Procedimento, 300),
                UnidadeSolicitante = Cortar(f.UnidadeSolicitante, 200),
                LidoEm = agora,
            }));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        db.ChangeTracker.Clear();
    }

    public Task<int> ContarPpiAsync(DateOnly competencia, CancellationToken ct) =>
        db.SisregPpiCotas.CountAsync(c => c.Competencia == competencia, ct);

    public async Task SubstituirPpiAsync(DateOnly competencia, IReadOnlyList<CotaPpiLidaSisreg> cotas, CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var unicas = cotas.GroupBy(c => (c.CodigoInterno, c.Tipo)).Select(g => g.First()).ToList();

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.SisregPpiCotas.Where(c => c.Competencia == competencia).ExecuteDeleteAsync(ct);
            db.SisregPpiCotas.AddRange(unicas.Select(c => new SisregPpiCota
            {
                Id = Guid.NewGuid(),
                Competencia = competencia,
                CodigoInterno = Cortar(c.CodigoInterno, 20)!,
                CodigoUnificado = Cortar(c.CodigoUnificado, 20),
                Procedimento = Cortar(c.Procedimento, 300),
                Total = c.Total,
                Usada = c.Usada,
                Saldo = c.Saldo,
                Tipo = Cortar(c.Tipo, 30)!,
                LidoEm = agora,
            }));
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        db.ChangeTracker.Clear();
    }

    public async Task<int> GravarCanceladasAsync(IReadOnlyList<MarcacaoCanceladaLidaSisreg> canceladas, CancellationToken ct)
    {
        var validas = canceladas
            .Where(c => c.CanceladoEm is not null)
            .GroupBy(c => (c.Codigo, c.CanceladoEm))
            .Select(g => g.First())
            .ToList();
        if (validas.Count == 0) return 0;

        // ON CONFLICT DO NOTHING: a conciliação relê o mesmo dia várias vezes e o coletor pode ler o
        // mês inteiro por cima — gravar de novo o que já existe não pode falhar nem duplicar.
        return await db.Database.ExecuteSqlRawAsync("""
            insert into smsmarica.sisreg_marcacao_cancelada
                (id, codigo_solicitacao, cancelado_em, data_marcacao, procedimento, justificativa, lido_em)
            select gen_random_uuid(), x.c, x.q, x.d, x.p, x.j, @agora
            from unnest(@codigos, @instantes, @datas, @procedimentos, @justificativas) as x(c, q, d, p, j)
            on conflict (codigo_solicitacao, cancelado_em) do nothing
            """,
            [
                Parametro("codigos", NpgsqlDbType.Text, validas.Select(c => Cortar(c.Codigo, 20)).ToArray()),
                Parametro("instantes", NpgsqlDbType.TimestampTz, validas.Select(c => (DateTime?)DateTime.SpecifyKind(c.CanceladoEm!.Value, DateTimeKind.Utc)).ToArray()),
                Parametro("datas", NpgsqlDbType.Date, validas.Select(c => c.DataMarcacao).ToArray()),
                Parametro("procedimentos", NpgsqlDbType.Text, validas.Select(c => Cortar(c.Procedimento, 300)).ToArray()),
                Parametro("justificativas", NpgsqlDbType.Text, validas.Select(c => Cortar(c.Justificativa, 1000)).ToArray()),
                new NpgsqlParameter("agora", NpgsqlDbType.TimestampTz) { Value = DateTime.UtcNow },
            ], ct);
    }

    public async Task<int> GravarDesfechosAsync(
        string cnesSolicitante, IReadOnlyList<(DesfechoLidoSisreg Linha, SituacaoDesfechoSisreg Situacao)> desfechos,
        CancellationToken ct)
    {
        var unicos = desfechos.GroupBy(d => (d.Linha.Codigo, d.Situacao)).Select(g => g.First()).ToList();
        if (unicos.Count == 0) return 0;

        return await db.Database.ExecuteSqlRawAsync("""
            insert into smsmarica.sisreg_solicitacao_desfecho
                (id, codigo_solicitacao, situacao, data_solicitacao, procedimento, unidade_solicitante_cnes, origem, lido_em)
            select gen_random_uuid(), x.c, x.s, x.d, x.p, @cnes, @origem, @agora
            from unnest(@codigos, @situacoes, @datas, @procedimentos) as x(c, s, d, p)
            on conflict (codigo_solicitacao, situacao) do nothing
            """,
            [
                Parametro("codigos", NpgsqlDbType.Text, unicos.Select(d => Cortar(d.Linha.Codigo, 20)).ToArray()),
                Parametro("situacoes", NpgsqlDbType.Integer, unicos.Select(d => (int?)(int)d.Situacao).ToArray()),
                Parametro("datas", NpgsqlDbType.Date, unicos.Select(d => d.Linha.DataSolicitacao).ToArray()),
                Parametro("procedimentos", NpgsqlDbType.Text, unicos.Select(d => Cortar(d.Linha.Procedimento, 300)).ToArray()),
                new NpgsqlParameter("cnes", NpgsqlDbType.Varchar) { Value = Cortar(cnesSolicitante, 10)! },
                new NpgsqlParameter("origem", NpgsqlDbType.Integer) { Value = (int)OrigemDesfechoSisreg.Tela },
                new NpgsqlParameter("agora", NpgsqlDbType.TimestampTz) { Value = DateTime.UtcNow },
            ], ct);
    }

    public Task RegistrarJanelaConcluidaAsync(
        ColetorIndicadorSisreg coletor, DateOnly inicio, DateOnly fim, string escopo, int linhas, CancellationToken ct) =>
        db.Database.ExecuteSqlRawAsync("""
            insert into smsmarica.sisreg_indicador_coleta
                (id, coletor, janela_inicio, janela_fim, escopo, status, tentativas, linhas, iniciado_em, lido_em, criado_em)
            values (gen_random_uuid(), @coletor, @inicio, @fim, @escopo, @concluida, 1, @linhas, @agora, @agora, @agora)
            on conflict (coletor, janela_inicio, escopo) do update
               set janela_fim = excluded.janela_fim, status = excluded.status, linhas = excluded.linhas,
                   lido_em = excluded.lido_em, erro = null
            """,
            [
                new NpgsqlParameter("coletor", NpgsqlDbType.Integer) { Value = (int)coletor },
                new NpgsqlParameter("inicio", NpgsqlDbType.Date) { Value = inicio },
                new NpgsqlParameter("fim", NpgsqlDbType.Date) { Value = fim },
                new NpgsqlParameter("escopo", NpgsqlDbType.Varchar) { Value = escopo },
                new NpgsqlParameter("concluida", NpgsqlDbType.Integer) { Value = (int)StatusColetaIndicador.Concluida },
                new NpgsqlParameter("linhas", NpgsqlDbType.Integer) { Value = linhas },
                new NpgsqlParameter("agora", NpgsqlDbType.TimestampTz) { Value = DateTime.UtcNow },
            ], ct);

    private static NpgsqlParameter Parametro<T>(string nome, NpgsqlDbType tipo, T[] valores) =>
        new(nome, NpgsqlDbType.Array | tipo) { Value = valores };

    private static string? Cortar(string? s, int n) => s is null ? null : s.Length <= n ? s : s[..n];
}
