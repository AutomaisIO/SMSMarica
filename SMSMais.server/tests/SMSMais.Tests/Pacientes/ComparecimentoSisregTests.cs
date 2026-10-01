using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Pacientes.Agendamentos;
using SMSMais.Core.Pacientes.Agendamentos.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// O histórico da aba Agendamentos responde "veio ou não veio?" com os TRÊS estados que a unidade
/// executante aponta no SISREG. Compareceu: chegada registrada na recepção ou confirmada pela
/// executante. Faltou: a unidade registrou falta — é o que a lista de absenteísmo contém (código +
/// dia), seja a leitura das semanas recentes, seja a de 30 dias. Em aberto: a unidade ainda não
/// apontou nada, e alguém foi olhar depois do dia — NÃO é falta. Quando ninguém olhou, "sem registro
/// de chegada".
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ComparecimentoSisregTests(PostgresFixture fixture)
{
    // Meses bem antigos, para não esbarrar em janela de faltas que outro teste tenha deixado na base.
    private static readonly DateOnly DiaComLista = new(2011, 3, 10);
    private static readonly DateOnly DiaSemLista = new(2012, 6, 5);

    /// <summary>Dia coberto só pela leitura das semanas RECENTES da lista de faltas (de hora em hora).</summary>
    private static readonly DateOnly DiaComListaRecente = new(2013, 8, 10);

    private static string Codigo() => Random.Shared.NextInt64(1_000_000_000, 9_999_999_999).ToString();

    /// <summary>Linha de 38 campos do Arquivo de Agendamentos, só com o que o comparecimento lê.</summary>
    private static string LinhaTxt(string codigo, string chegada) =>
        string.Join(';', Enumerable.Range(0, 38).Select(i => i switch { 0 => codigo, 34 => chegada, _ => "" }));

    [Fact]
    public async Task Historico_so_afirma_comparecimento_ou_falta_com_prova()
    {
        var paciente = Guid.NewGuid();
        var agora = DateTime.UtcNow;
        var faltou = Codigo();
        var remarcado = Codigo();
        var cancelado = Codigo();
        var naListaRecente = Codigo();

        await using (var db = fixture.CriarDbContext())
        {
            var unidade = new Unidade
            {
                Id = Guid.NewGuid(), Nome = $"UNID {Guid.NewGuid():N}"[..24], Ativo = true, CriadoEm = agora,
            };
            db.Unidades.Add(unidade);

            Solicitacao Sol(string procedimento, DateOnly dia, string? codigo, string? raw = null) => new()
            {
                Id = Guid.NewGuid(), PacienteId = paciente, Categoria = CategoriaSolicitacao.Consulta,
                UnidadeExecutanteId = unidade.Id, SolicitanteNome = "DR TESTE", ProcedimentoTexto = procedimento,
                Status = StatusSolicitacao.Agendada, StatusConfirmacao = StatusConfirmacaoAgendamento.Pendente,
                Prioridade = PrioridadeSolicitacao.Eletiva, CodigoSolicitacao = codigo, RawSisreg = raw,
                // 13:00 UTC = manhã em Brasília, no mesmo dia.
                DataAgendada = DateTime.SpecifyKind(dia.ToDateTime(new TimeOnly(13, 0)), DateTimeKind.Utc),
                CriadoEm = agora,
            };

            var recepcao = Sol("RECEPCAO", DiaComLista, Codigo());
            recepcao.AutorizadoEm = agora;
            var confirmadoTxt = Codigo();
            var cancelada = Sol("CANCELADA", DiaComLista, cancelado);
            cancelada.Status = StatusSolicitacao.Cancelada;

            // O que a varredura diária da unidade releu depois do atendimento.
            var confirmadoNaVarredura = Sol("CONFIRMADO NA VARREDURA", DiaSemLista, Codigo());
            confirmadoNaVarredura.ChegadaConfirmadaSisreg = true;
            confirmadoNaVarredura.ChegadaSisregLidaEm = agora;
            var pendenteNaVarredura = Sol("PENDENTE NA VARREDURA", DiaSemLista, Codigo());
            pendenteNaVarredura.ChegadaConfirmadaSisreg = false;
            pendenteNaVarredura.ChegadaSisregLidaEm = agora;
            // "Pendente" lido ANTES do dia não diz nada sobre o atendimento.
            var pendenteDeAntes = Sol("PENDENTE DE ANTES", DiaSemLista, Codigo());
            pendenteDeAntes.ChegadaConfirmadaSisreg = false;
            pendenteDeAntes.ChegadaSisregLidaEm =
                DateTime.SpecifyKind(DiaSemLista.AddDays(-2).ToDateTime(new TimeOnly(13, 0)), DateTimeKind.Utc);
            // A coluna manda sobre a linha guardada: a unidade desfez a confirmação.
            var desfeito = Codigo();
            var confirmacaoDesfeita = Sol("CONFIRMACAO DESFEITA", DiaSemLista, desfeito, LinhaTxt(desfeito, "CONFIRMADO"));
            confirmacaoDesfeita.ChegadaConfirmadaSisreg = false;
            confirmacaoDesfeita.ChegadaSisregLidaEm = agora;

            db.Solicitacoes.AddRange(
                recepcao,
                Sol("CONFIRMADO NO TXT", DiaComLista, confirmadoTxt, LinhaTxt(confirmadoTxt, "CONFIRMADO")),
                Sol("FALTA NA LISTA", DiaComLista, faltou, LinhaTxt(faltou, "PENDENTE")),
                // Faltou numa data, foi remarcado: a falta antiga não contamina o agendamento novo.
                Sol("REMARCADO", DiaComLista, remarcado, LinhaTxt(remarcado, "PENDENTE")),
                // Sem código do SISREG não há como casar com a lista — não se afirma "sem falta".
                Sol("SEM CODIGO", DiaComLista, null),
                Sol("DIA SEM LISTA", DiaSemLista, Codigo()),
                Sol("FALTA NA TELA", DiaSemLista, Codigo(),
                    """{"origem":"cons_agendas","situacao":"Agendamento/Falta/Executante"}"""),
                Sol("FUTURO", FusoBrasilia.HojeEmBrasilia().AddDays(20), Codigo()),
                cancelada,
                confirmadoNaVarredura, pendenteNaVarredura, pendenteDeAntes, confirmacaoDesfeita,
                // Semana ainda nova: a falta que a unidade registrou já aparece, sem esperar 30 dias.
                Sol("NA LISTA RECENTE", DiaComListaRecente, naListaRecente),
                Sol("FORA DA LISTA RECENTE", DiaComListaRecente, Codigo()));

            db.SisregFaltasOficiais.AddRange(
                new SisregFaltaOficial { Id = Guid.NewGuid(), CodigoSolicitacao = naListaRecente, DataExecucao = DiaComListaRecente, LidoEm = agora },
                new SisregFaltaOficial { Id = Guid.NewGuid(), CodigoSolicitacao = faltou, DataExecucao = DiaComLista, LidoEm = agora },
                new SisregFaltaOficial { Id = Guid.NewGuid(), CodigoSolicitacao = remarcado, DataExecucao = DiaComLista.AddDays(-3), LidoEm = agora },
                new SisregFaltaOficial { Id = Guid.NewGuid(), CodigoSolicitacao = cancelado, DataExecucao = DiaComLista, LidoEm = agora });

            db.SisregIndicadorColetas.Add(new SisregIndicadorColeta
            {
                Id = Guid.NewGuid(), Coletor = ColetorIndicadorSisreg.Faltas, Status = StatusColetaIndicador.Concluida,
                JanelaInicio = new DateOnly(2011, 3, 1), JanelaFim = new DateOnly(2011, 3, 31),
                Escopo = Guid.NewGuid().ToString("N")[..12], Tentativas = 1, Linhas = 3, LidoEm = agora, CriadoEm = agora,
            });
            db.SisregIndicadorColetas.Add(new SisregIndicadorColeta
            {
                Id = Guid.NewGuid(), Coletor = ColetorIndicadorSisreg.FaltasRecentes, Status = StatusColetaIndicador.Concluida,
                JanelaInicio = new DateOnly(2013, 8, 9), JanelaFim = new DateOnly(2013, 8, 16),
                Escopo = Guid.NewGuid().ToString("N")[..12], Tentativas = 1, Linhas = 1, LidoEm = agora, CriadoEm = agora,
            });

            // Fora do SISREG a regra é a mesma: "Agendada" que ficou no passado sem registro de chegada.
            db.SerSolicitacoes.Add(new SerSolicitacao
            {
                Id = Guid.NewGuid(), IdSer = Codigo(), Tipo = TipoRecursoSer.Consulta, Recurso = "SER ANTIGA",
                PacienteNome = "PACIENTE TESTE", PacienteId = paciente, Situacao = SituacaoSer.Agendada,
                AgendadoParaTexto = "10/02/2020 08:00 - HOSPITAL Y", SincronizadoEm = agora, CriadoEm = agora,
            });

            await db.SaveChangesAsync();
        }

        await using var leitura = fixture.CriarDbContext();
        var r = await new AgendamentosPacienteService(leitura).ListarPorPacienteAsync(paciente);

        var futuro = Assert.Single(r.Proximos);
        Assert.Equal("FUTURO", futuro.Descricao);
        Assert.Equal(SituacaoAgendamentoPaciente.Agendado, futuro.Situacao);

        AgendamentoPacienteItemDto Do(string descricao) => Assert.Single(r.Historico, i => i.Descricao == descricao);

        Assert.Equal(SituacaoAgendamentoPaciente.Compareceu, Do("RECEPCAO").Situacao);
        Assert.Contains("recepção", Do("RECEPCAO").SituacaoOrigem);

        Assert.Equal(SituacaoAgendamentoPaciente.Compareceu, Do("CONFIRMADO NO TXT").Situacao);
        Assert.Contains("unidade executante", Do("CONFIRMADO NO TXT").SituacaoOrigem);

        Assert.Equal(SituacaoAgendamentoPaciente.Faltou, Do("FALTA NA LISTA").Situacao);
        Assert.Contains("falta registrada pela unidade", Do("FALTA NA LISTA").SituacaoOrigem);
        Assert.Equal(SituacaoAgendamentoPaciente.Faltou, Do("FALTA NA TELA").Situacao);

        Assert.Equal(SituacaoAgendamentoPaciente.EmAberto, Do("REMARCADO").Situacao);
        Assert.Equal("Em aberto", Do("REMARCADO").SituacaoDescricao);

        Assert.Equal(SituacaoAgendamentoPaciente.SemRegistroDeChegada, Do("SEM CODIGO").Situacao);
        Assert.Equal(SituacaoAgendamentoPaciente.SemRegistroDeChegada, Do("DIA SEM LISTA").Situacao);
        Assert.Equal("Sem registro de chegada", Do("DIA SEM LISTA").SituacaoDescricao);
        Assert.Equal(SituacaoAgendamentoPaciente.SemRegistroDeChegada, Do("SER ANTIGA").Situacao);

        // A chegada relida pela varredura diária da unidade.
        Assert.Equal(SituacaoAgendamentoPaciente.Compareceu, Do("CONFIRMADO NA VARREDURA").Situacao);
        Assert.Equal(SituacaoAgendamentoPaciente.EmAberto, Do("PENDENTE NA VARREDURA").Situacao);
        Assert.Contains("sem apontamento da unidade", Do("PENDENTE NA VARREDURA").SituacaoOrigem);
        Assert.Equal(SituacaoAgendamentoPaciente.SemRegistroDeChegada, Do("PENDENTE DE ANTES").Situacao);
        Assert.Equal(SituacaoAgendamentoPaciente.EmAberto, Do("CONFIRMACAO DESFEITA").Situacao);

        // A lista de faltas é marcação explícita da unidade: vale como falta mesmo lida cedo. E quem
        // não está nela (nem confirmado) está em aberto — não faltou.
        Assert.Equal(SituacaoAgendamentoPaciente.Faltou, Do("NA LISTA RECENTE").Situacao);
        Assert.Contains("falta registrada pela unidade", Do("NA LISTA RECENTE").SituacaoOrigem);
        Assert.Equal(SituacaoAgendamentoPaciente.EmAberto, Do("FORA DA LISTA RECENTE").Situacao);

        // Cancelada continua cancelada, mesmo constando na lista de faltas.
        Assert.Equal(SituacaoAgendamentoPaciente.Cancelado, Do("CANCELADA").Situacao);
    }

    /// <summary>
    /// ESUS de São Gonçalo: a efetivação que a varredura lê do histórico do paciente vira os mesmos
    /// selos do SISREG. Sem leitura depois do dia, "sem registro de chegada" — nunca "em aberto".
    /// </summary>
    [Fact]
    public async Task EsusSg_mostra_efetivado_nao_efetivado_e_em_aberto()
    {
        var paciente = Guid.NewGuid();
        var agora = DateTime.UtcNow;
        var dia = FusoBrasilia.HojeEmBrasilia().AddDays(-6);
        var depoisDoDia = FusoBrasilia.DeBrasiliaParaUtc(dia.AddDays(2).ToDateTime(new TimeOnly(10, 0)));
        var antesDoDia = FusoBrasilia.DeBrasiliaParaUtc(dia.AddDays(-2).ToDateTime(new TimeOnly(10, 0)));

        await using (var db = fixture.CriarDbContext())
        {
            EsusSgSolicitacao Esus(string recurso, EfetivacaoEsusSg? efetivacao, DateTime? lida, string? motivo = null) => new()
            {
                Id = Guid.NewGuid(), IdEsusSg = Codigo(), Tipo = TipoRecursoEsusSg.Exame, Recurso = recurso,
                PacienteNome = "PACIENTE TESTE", PacienteId = paciente, Situacao = SituacaoEsusSg.Agendada,
                DataAgendada = dia, DataHoraAgendadaTexto = $"{dia:dd/MM/yyyy} 08:00:00",
                Efetivacao = efetivacao, MotivoNaoEfetivacao = motivo, EfetivacaoLidaEm = lida,
                EfetivadoEm = efetivacao == EfetivacaoEsusSg.Efetivado ? depoisDoDia : null,
                SincronizadoEm = agora, CriadoEm = agora,
            };
            db.EsusSgSolicitacoes.AddRange(
                Esus("ESUS EFETIVADO", EfetivacaoEsusSg.Efetivado, depoisDoDia),
                Esus("ESUS NAO EFETIVADO", EfetivacaoEsusSg.NaoEfetivado, depoisDoDia, "Não Compareceu"),
                Esus("ESUS LIDO DEPOIS", null, depoisDoDia),
                Esus("ESUS LIDO ANTES", null, antesDoDia),
                Esus("ESUS NUNCA LIDO", null, null));
            await db.SaveChangesAsync();
        }

        await using var leitura = fixture.CriarDbContext();
        var r = await new AgendamentosPacienteService(leitura).ListarPorPacienteAsync(paciente);
        AgendamentoPacienteItemDto Do(string d) => Assert.Single(r.Historico, i => i.Descricao == d);

        Assert.Equal(SituacaoAgendamentoPaciente.Compareceu, Do("ESUS EFETIVADO").Situacao);
        Assert.Contains("efetivado pela unidade no ESUS", Do("ESUS EFETIVADO").SituacaoOrigem);
        Assert.Equal(SituacaoAgendamentoPaciente.Faltou, Do("ESUS NAO EFETIVADO").Situacao);
        Assert.Contains("Não Compareceu", Do("ESUS NAO EFETIVADO").SituacaoOrigem);
        Assert.Equal(SituacaoAgendamentoPaciente.EmAberto, Do("ESUS LIDO DEPOIS").Situacao);
        Assert.Equal(SituacaoAgendamentoPaciente.SemRegistroDeChegada, Do("ESUS LIDO ANTES").Situacao);
        Assert.Equal(SituacaoAgendamentoPaciente.SemRegistroDeChegada, Do("ESUS NUNCA LIDO").Situacao);
        Assert.All(r.Historico, i => Assert.NotNull(i.DataHora));
    }
}
