using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMarica.Core.Ser.Pacientes;
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

    /// <summary>
    /// O caso da Ester (10/08/2026): o pedido mais novo veio sem CPF e sem telefone, e era ele
    /// quem falava pela pessoa inteira — apagou telefone e carimbou "sem CPF" em quem tinha CPF
    /// em duas solicitações anteriores. Agora a mais nova empresta da irmã de mesma CNS.
    ///
    /// <para>A outra metade do teste é tão importante quanto: o espelho do SER <b>não pode</b>
    /// sair alterado. Ele reproduz o que o Estado tem; se a consolidação vazasse para a entidade
    /// rastreada, o próximo <c>SaveChanges</c> inventaria no espelho um dado que o SER nunca
    /// mandou.</para>
    /// </summary>
    [Fact]
    public async Task Solicitacao_sem_cpf_e_sem_telefone_empresta_da_irma_de_mesma_cns()
    {
        await using var db = fixture.CriarDbContext();

        var marca = Guid.NewGuid().ToString("N")[..6];
        var baseData = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        const string cns = "700300908811839";

        var irma = Nova($"{marca}-irma", "52998224725", cns, baseData);
        irma.TelefoneWhatsapp = "(21) 96715-6518";
        irma.TelefoneContato = "(21) 99715-0212";

        // A mais recente: mesma CNS, sem CPF, sem telefone — e é ela que está na fila.
        var alvo = Nova($"{marca}-alvo", null, cns, baseData.AddDays(30));
        alvo.PacienteConciliarEm = baseData.AddDays(30);

        db.SerSolicitacoes.AddRange(irma, alvo);
        await db.SaveChangesAsync();

        var espia = new ConciliacaoEspia();
        var servico = new SerBackfillPacientesService(
            db, espia, NullLogger<SerBackfillPacientesService>.Instance);

        await servico.ExecutarPendentesAsync(50, CancellationToken.None);

        var conciliada = espia.Recebidas.Should()
            .ContainSingle(x => x.IdSer == alvo.IdSer).Subject;
        conciliada.Cpf.Should().Be("52998224725", "a irmã de mesma CNS sabia o CPF");
        conciliada.TelefoneWhatsapp.Should().Be("(21) 96715-6518");
        conciliada.TelefoneContato.Should().Be("(21) 99715-0212");

        // O espelho continua sendo o retrato fiel do SER.
        await using var conferencia = fixture.CriarDbContext();
        var noBanco = await conferencia.SerSolicitacoes
            .AsNoTracking().SingleAsync(x => x.Id == alvo.Id);
        noBanco.Cpf.Should().BeNull("o SER não mandou CPF nesta solicitação");
        noBanco.TelefoneWhatsapp.Should().BeNull();
        noBanco.TelefoneContato.Should().BeNull();
    }

    /// <summary>Captura o que a conciliação recebeu, sem falar com hub nenhum.</summary>
    private sealed class ConciliacaoEspia : ISerConciliacaoPacienteService
    {
        public List<SerSolicitacao> Recebidas { get; } = [];

        public Task<ConciliacaoSerDto> ConciliarAsync(SerSolicitacao s, CancellationToken ct)
        {
            Recebidas.Add(s);
            return Task.FromResult(
                new ConciliacaoSerDto(ResultadoConciliacaoSer.Inalterado, null, []));
        }
    }
}
