using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.Varredura;

/// <param name="Lidas">Agendamentos já passados que vieram com a chegada informada.</param>
/// <param name="Confirmadas">Quantos o SISREG dá como CONFIRMADO.</param>
/// <param name="Pendentes">Quantos seguem PENDENTE de confirmação pela unidade.</param>
/// <param name="Atualizadas">Solicitações nossas efetivamente regravadas.</param>
public sealed record ChegadasGravadas(int Lidas, int Confirmadas, int Pendentes, int Atualizadas)
{
    public static readonly ChegadasGravadas Nada = new(0, 0, 0, 0);
}

/// <summary>
/// A chegada do paciente como o SISREG a registra (coluna 34 do Arquivo de Agendamentos), gravada em
/// <c>Solicitacao.ChegadaConfirmadaSisreg</c>.
///
/// <para><b>Por que existe.</b> A varredura lia de hoje para a frente e a importação só regrava a
/// linha quando muda data, executante ou procedimento — então a chegada ficava para sempre no
/// "pendente" de antes do atendimento. Medido em produção em 01/10/2026: setembro com 0,9% de
/// confirmados, contra ~63% nos meses que a carga do histórico leu depois do fato. A varredura
/// diária da unidade passou a reler os últimos dias; este serviço é o que ela chama para gravar.</para>
///
/// <para><b>Só a chegada.</b> Não cria solicitação, não muda data nem procedimento, não avisa
/// paciente, não detecta ausente. Reler o passado pelo fluxo de importação é o que tornava
/// "importar o passado" perigoso; aqui o passado só responde uma pergunta — veio ou não veio.</para>
/// </summary>
public interface IChegadasSisregService
{
    /// <summary>
    /// Quantos agendamentos da unidade temos em cada dia da janela. Serve para fatiar a exportação
    /// abaixo do teto de registros do SISREG sem pagar a requisição que só descobriria o estouro.
    /// </summary>
    Task<IReadOnlyDictionary<DateOnly, int>> VolumePorDiaAsync(
        Guid unidadeId, DateOnly inicio, DateOnly fim, CancellationToken ct);

    /// <summary>Grava a chegada das marcações cujo dia de atendimento já chegou (até hoje, inclusive).</summary>
    Task<ChegadasGravadas> GravarAsync(IReadOnlyList<MarcacaoSisreg> lidas, DateOnly hoje, CancellationToken ct);
}

public sealed class ChegadasSisregService(SmsMaisDbContext db) : IChegadasSisregService
{
    /// <summary>Lote do UPDATE. Só evita um comando com dezenas de milhares de parâmetros de array.</summary>
    private const int Lote = 2000;

    public async Task<IReadOnlyDictionary<DateOnly, int>> VolumePorDiaAsync(
        Guid unidadeId, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        var inicioUtc = FusoBrasilia.DeBrasiliaParaUtc(inicio.ToDateTime(TimeOnly.MinValue));
        var fimUtc = FusoBrasilia.DeBrasiliaParaUtc(fim.AddDays(1).ToDateTime(TimeOnly.MinValue));

        // Só o que veio do SISREG (tem RAW) e segue de pé: é o que o arquivo vai trazer de volta.
        var instantes = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.UnidadeExecutanteId == unidadeId && s.ExcluidoEm == null && s.RawSisreg != null
                && (s.Status == StatusSolicitacao.Agendada || s.Status == StatusSolicitacao.Realizada)
                && s.DataAgendada >= inicioUtc && s.DataAgendada < fimUtc)
            .Select(s => s.DataAgendada!.Value)
            .ToListAsync(ct);

        return instantes
            .GroupBy(i => DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(i)))
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public async Task<ChegadasGravadas> GravarAsync(
        IReadOnlyList<MarcacaoSisreg> lidas, DateOnly hoje, CancellationToken ct)
    {
        // Uma linha por (código, dia): a divisão de fatias pode repetir a mesma marcação na fronteira.
        var passadas = lidas
            .Where(m => m.ChegadaConfirmada is not null && m.DataHoraAtendimento is { } dh
                && DateOnly.FromDateTime(dh) <= hoje)
            .GroupBy(m => (m.CodigoSolicitacao, Dia: DateOnly.FromDateTime(m.DataHoraAtendimento!.Value)))
            .Select(g => (Codigo: g.Key.CodigoSolicitacao, g.Key.Dia, Confirmada: g.Any(m => m.ChegadaConfirmada == true)))
            .ToList();
        if (passadas.Count == 0) return ChegadasGravadas.Nada;

        var agora = DateTime.UtcNow;
        var atualizadas = 0;
        foreach (var lote in passadas.Chunk(Lote))
        {
            // Casa por código E dia: se o SISREG remarcou e a nossa data ainda é outra, a chegada lida
            // não é deste agendamento — quem acerta a data é a varredura normal, não esta releitura.
            //
            // Não regrava quem já está confirmado e continua confirmado (é o estado final, e a tabela
            // tem um milhão de linhas); o pendente é carimbado de novo a cada leitura, porque "lido em"
            // é o que separa "a unidade ainda não confirmou" de "ninguém foi olhar".
            atualizadas += await db.Database.ExecuteSqlRawAsync("""
                update smsmarica.solicitacao s
                   set chegada_confirmada_sisreg = x.c, chegada_sisreg_lida_em = @agora
                  from unnest(@codigos, @dias, @confirmadas) as x(cod, dia, c)
                 where s.codigo_solicitacao = x.cod
                   and s.codigo_solicitacao is not null and s.codigo_solicitacao <> '0000' and s.excluido_em is null
                   and (s.data_agendada at time zone 'America/Sao_Paulo')::date = x.dia
                   and (s.chegada_confirmada_sisreg is distinct from x.c or not x.c)
                """,
                [
                    new NpgsqlParameter("codigos", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = lote.Select(p => p.Codigo).ToArray() },
                    new NpgsqlParameter("dias", NpgsqlDbType.Array | NpgsqlDbType.Date) { Value = lote.Select(p => p.Dia).ToArray() },
                    new NpgsqlParameter("confirmadas", NpgsqlDbType.Array | NpgsqlDbType.Boolean) { Value = lote.Select(p => p.Confirmada).ToArray() },
                    new NpgsqlParameter("agora", NpgsqlDbType.TimestampTz) { Value = agora },
                ], ct);
        }

        var confirmadas = passadas.Count(p => p.Confirmada);
        return new ChegadasGravadas(passadas.Count, confirmadas, passadas.Count - confirmadas, atualizadas);
    }

    /// <summary>
    /// Parte a janela em fatias de dias consecutivos que somem até <paramref name="alvoPorFatia"/>
    /// agendamentos, pelo volume que JÁ temos no banco.
    ///
    /// <para><b>Por que não deixar a exportação se partir sozinha.</b> Ela só descobre que estourou o
    /// teto depois de pagar a requisição, e aí paga mais duas pelas metades. Medido em 30/09/2026: uma
    /// unidade com 2.624 agendamentos em 30 dias gastou 13 requisições. Sabendo o volume de antemão,
    /// saem ~5. A divisão pelo teto continua lá atrás como rede — o nosso número pode estar abaixo do
    /// do SISREG.</para>
    /// </summary>
    internal static List<(DateOnly Inicio, DateOnly Fim)> FatiarPorVolume(
        DateOnly inicio, DateOnly fim, IReadOnlyDictionary<DateOnly, int> porDia, int alvoPorFatia)
    {
        var fatias = new List<(DateOnly, DateOnly)>();
        if (fim < inicio) return fatias;

        var abertura = inicio;
        var soma = 0;
        for (var dia = inicio; dia <= fim; dia = dia.AddDays(1))
        {
            var doDia = porDia.GetValueOrDefault(dia);
            var estouraVolume = dia > abertura && soma + doDia > alvoPorFatia;
            // O SISREG recusa exportação com diferença de datas maior que o limite da varredura.
            var estouraDias = dia.DayNumber - abertura.DayNumber > VarreduraSisregOpcoes.MaxDiasAFrente;
            if (estouraVolume || estouraDias)
            {
                fatias.Add((abertura, dia.AddDays(-1)));
                abertura = dia;
                soma = 0;
            }
            soma += doDia;
        }

        fatias.Add((abertura, fim));
        return fatias;
    }
}
