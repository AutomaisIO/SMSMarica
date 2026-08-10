using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Ser;
using SMSMarica.Tests.Infraestrutura;

namespace SMSMarica.Tests.Integracoes;

/// <summary>
/// A fila do backfill de pacientes do SER, contra Postgres real.
///
/// <para><b>Por que este teste precisa de banco de verdade.</b> A consulta agrupa por (CPF, CNS),
/// pega o <c>First()</c> de cada grupo por data e ordena por um booleano calculado. LINQ assim ou
/// o EF traduz para SQL, ou estoura em runtime — e estouraria exatamente quando alguém clicasse no
/// backfill em produção, que é o pior momento possível para descobrir. Em memória o teste passaria
/// sempre e não provaria nada.</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class SerBackfillOrdemTests(PostgresFixture fixture)
{
    private static SerSolicitacao Nova(
        string idSer, string? cpf, string? cns, DateTime sincronizadoEm) => new()
        {
            Id = Guid.NewGuid(),
            IdSer = idSer,
            Tipo = TipoRecursoSer.Consulta,
            Situacao = SituacaoSer.EmFila,
            Cpf = cpf,
            Cns = cns,
            PacienteNome = $"PACIENTE {idSer}",
            CriadoEm = sincronizadoEm,
            SincronizadoEm = sincronizadoEm,
        };

    /// <summary>
    /// Espelha a consulta de <c>IdsMaisRecentesPorPacienteAsync</c>: um id por paciente, o mais
    /// recente, e <b>quem tem CPF primeiro</b> — a ordem que reduz duplicata, porque parte dos
    /// só-CNS ganha CPF antes de chegar a vez deles.
    /// </summary>
    [Fact]
    public async Task Fila_traz_um_por_paciente_o_mais_recente_e_com_cpf_primeiro()
    {
        await using var db = fixture.CriarDbContext();

        var marca = Guid.NewGuid().ToString("N")[..6];
        var baseData = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

        // Mesmo paciente (mesmo CPF+CNS) em duas solicitações: só a mais recente entra.
        var antiga = Nova($"{marca}-1", "52998224725", "700300908811839", baseData);
        var recente = Nova($"{marca}-2", "52998224725", "700300908811839", baseData.AddDays(5));
        // Um só com CNS — tem de vir DEPOIS de quem tem CPF.
        var soCns = Nova($"{marca}-3", null, "898003229589752", baseData.AddDays(9));

        db.SerSolicitacoes.AddRange(antiga, recente, soCns);
        await db.SaveChangesAsync();

        var grupos = await db.SerSolicitacoes
            .AsNoTracking()
            .Where(s => s.ExcluidoEm == null
                        && s.IdSer.StartsWith(marca)
                        && ((s.Cpf != null && s.Cpf != "") || (s.Cns != null && s.Cns != "")))
            .GroupBy(s => new { s.Cpf, s.Cns })
            .Select(g => new
            {
                Id = g
                    .OrderByDescending(x => x.SincronizadoEm)
                    .ThenByDescending(x => x.DataSolicitacao)
                    .Select(x => x.Id)
                    .First(),
                TemCpf = g.Key.Cpf != null && g.Key.Cpf != "",
            })
            .OrderByDescending(x => x.TemCpf)
            .ToListAsync();

        grupos.Should().HaveCount(2, "duas identidades distintas, não três solicitações");
        grupos[0].Id.Should().Be(recente.Id, "quem tem CPF vem primeiro, e é a solicitação mais nova");
        grupos[0].TemCpf.Should().BeTrue();
        grupos[1].Id.Should().Be(soCns.Id, "o só-CNS fica para o fim, mesmo sendo o mais recente de todos");
        grupos.Select(x => x.Id).Should().NotContain(antiga.Id, "a solicitação velha do mesmo paciente sai");
    }
}
