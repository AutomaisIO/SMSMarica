using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Integracoes.SisregWeb.Importacao;
using SMSMais.Core.Integracoes.SisregWeb.Varredura;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// A releitura de chegadas da varredura diária: o que o SISREG diz da chegada do paciente
/// (CONFIRMADO/PENDENTE) entra na solicitação — e só isso. O que se protege: chegada gravada no
/// agendamento errado (remarcado), linha confirmada reescrita toda noite à toa, e fatia que estoura o
/// teto do SISREG por não olhar o volume antes.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ChegadasSisregTests(PostgresFixture fixture)
{
    private static readonly DateOnly Hoje = new(2013, 5, 20);
    private static readonly DateOnly Dia = new(2013, 5, 10);

    private static string Codigo() => Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString();

    private static MarcacaoSisreg Lida(string codigo, DateOnly dia, bool? chegada) => new(
        CodigoSolicitacao: codigo, CnsPaciente: null, NomePaciente: null, ProcedimentoTexto: null, CodigoSigtap: null,
        CpfMedicoSolicitante: null, NomeMedicoSolicitante: null, CrmMedicoSolicitante: null,
        CnesUnidadeSolicitante: null, NomeUnidadeSolicitante: null, CnesUnidadeExecutante: null,
        NomeUnidadeExecutante: null, DataHoraAtendimento: dia.ToDateTime(new TimeOnly(10, 0)),
        DataSolicitacao: null, DataRegulacao: null, Cid: null, ChegadaConfirmada: chegada);

    // ------------------------------------------------------------------ fatias por volume

    [Fact]
    public void Fatias_somam_ate_o_alvo_e_dia_gordo_fica_sozinho()
    {
        var d = new DateOnly(2031, 3, 1);
        var volume = new Dictionary<DateOnly, int> { [d] = 300, [d.AddDays(1)] = 300, [d.AddDays(2)] = 100, [d.AddDays(3)] = 600 };

        var fatias = ChegadasSisregService.FatiarPorVolume(d, d.AddDays(9), volume, alvoPorFatia: 550);

        List<(DateOnly, DateOnly)> esperado =
            [(d, d), (d.AddDays(1), d.AddDays(2)), (d.AddDays(3), d.AddDays(3)), (d.AddDays(4), d.AddDays(9))];
        Assert.Equal(esperado, fatias);
    }

    [Fact]
    public void Fatias_encostam_sem_sobrepor_e_respeitam_o_limite_de_dias_do_SISREG()
    {
        var d = new DateOnly(2031, 3, 1);

        var fatias = ChegadasSisregService.FatiarPorVolume(d, d.AddDays(39), new Dictionary<DateOnly, int>(), 550);

        List<(DateOnly, DateOnly)> esperado = [(d, d.AddDays(30)), (d.AddDays(31), d.AddDays(39))];
        Assert.Equal(esperado, fatias);
        Assert.Empty(ChegadasSisregService.FatiarPorVolume(d, d.AddDays(-1), new Dictionary<DateOnly, int>(), 550));
    }

    // ------------------------------------------------------------------------ gravação

    [Fact]
    public async Task Grava_so_a_chegada_do_agendamento_certo_e_nao_reescreve_o_que_ja_esta_confirmado()
    {
        var agora = DateTime.UtcNow;
        var antiga = agora.AddDays(-3);
        string confirmou = Codigo(), pendente = Codigo(), remarcado = Codigo(), jaConfirmado = Codigo(),
            seguePendente = Codigo(), desfeito = Codigo(), futuro = Codigo();
        Guid unidadeId;

        await using (var db = fixture.CriarDbContext())
        {
            var unidade = new Unidade { Id = Guid.NewGuid(), Nome = $"UNID {Guid.NewGuid():N}"[..24], Ativo = true, CriadoEm = agora };
            unidadeId = unidade.Id;
            db.Unidades.Add(unidade);

            Solicitacao Sol(string codigo, DateOnly dia, bool? chegada = null, DateTime? lidaEm = null) => new()
            {
                Id = Guid.NewGuid(), PacienteId = Guid.NewGuid(), Categoria = CategoriaSolicitacao.Consulta,
                UnidadeExecutanteId = unidade.Id, SolicitanteNome = "DR TESTE", ProcedimentoTexto = "CONSULTA",
                Status = StatusSolicitacao.Agendada, StatusConfirmacao = StatusConfirmacaoAgendamento.Pendente,
                Prioridade = PrioridadeSolicitacao.Eletiva, CodigoSolicitacao = codigo, RawSisreg = "sisreg",
                DataAgendada = DateTime.SpecifyKind(dia.ToDateTime(new TimeOnly(13, 0)), DateTimeKind.Utc),
                ChegadaConfirmadaSisreg = chegada, ChegadaSisregLidaEm = lidaEm, CriadoEm = agora,
            };

            db.Solicitacoes.AddRange(
                Sol(confirmou, Dia), Sol(pendente, Dia), Sol(remarcado, Dia),
                Sol(jaConfirmado, Dia, true, antiga), Sol(seguePendente, Dia, false, antiga),
                Sol(desfeito, Dia, true, antiga), Sol(futuro, Hoje.AddDays(5)));
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CriarDbContext())
        {
            var servico = new ChegadasSisregService(db);

            var r = await servico.GravarAsync(
            [
                Lida(confirmou, Dia, true),
                Lida(confirmou, Dia, true), // a divisão de fatias pode repetir a linha na fronteira
                Lida(pendente, Dia, false),
                Lida(remarcado, Dia.AddDays(2), true), // no SISREG o dia é outro: não é este agendamento
                Lida(jaConfirmado, Dia, true),
                Lida(seguePendente, Dia, false),
                Lida(desfeito, Dia, false),
                Lida(futuro, Hoje.AddDays(5), true), // ainda não aconteceu
                Lida(Codigo(), Dia, null), // linha sem a coluna legível
            ], Hoje, default);

            Assert.Equal(new ChegadasGravadas(Lidas: 6, Confirmadas: 3, Pendentes: 3, Atualizadas: 4), r);

            var volume = await servico.VolumePorDiaAsync(unidadeId, Dia.AddDays(-5), Hoje.AddDays(-1), default);
            Assert.Equal(6, Assert.Single(volume, v => v.Key == Dia).Value);
        }

        await using var ver = fixture.CriarDbContext();
        var gravadas = await ver.Solicitacoes.AsNoTracking()
            .Where(s => s.UnidadeExecutanteId == unidadeId)
            .ToDictionaryAsync(s => s.CodigoSolicitacao!, s => (s.ChegadaConfirmadaSisreg, s.ChegadaSisregLidaEm));

        Assert.True(gravadas[confirmou].ChegadaConfirmadaSisreg);
        Assert.NotNull(gravadas[confirmou].ChegadaSisregLidaEm);
        Assert.False(gravadas[pendente].ChegadaConfirmadaSisreg);
        Assert.Null(gravadas[remarcado].ChegadaConfirmadaSisreg);
        Assert.Null(gravadas[futuro].ChegadaConfirmadaSisreg);

        // Confirmado que continua confirmado é estado final: não é regravado.
        Assert.True(gravadas[jaConfirmado].ChegadaSisregLidaEm < agora.AddDays(-2));
        // Pendente é carimbado de novo: "lido em" é o que diz que alguém foi olhar depois do dia.
        Assert.True(gravadas[seguePendente].ChegadaSisregLidaEm > agora.AddMinutes(-5));
        // A unidade desfez a confirmação no SISREG: o espelho acompanha.
        Assert.False(gravadas[desfeito].ChegadaConfirmadaSisreg);
    }
}
