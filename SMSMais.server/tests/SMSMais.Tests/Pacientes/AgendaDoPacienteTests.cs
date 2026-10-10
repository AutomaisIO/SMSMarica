using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SMSMais.Core.Anexos;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Exames;
using SMSMais.Core.Integracoes.SisregWeb.Chave;
using SMSMais.Core.Laudos;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Agendamentos;
using SMSMais.Core.Pacientes.Agendamentos.Dtos;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// A agenda do PRÓPRIO paciente no app do cidadão — pedido do Bernardo em 09/10/2026: "em consultas e
/// exames tem que mostrar passado, futuro e os que estão em fila aguardando", de todas as regulações
/// (SISREG, SER, SERNIT, ESUS SG), sem nunca dizer ao paciente por que o pedido está pendente.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class AgendaDoPacienteTests(PostgresFixture fixture)
{
    private static string N() => Random.Shared.NextInt64(100_000_000, 999_999_999).ToString();
    private static string Cns() => "7" + Random.Shared.NextInt64(10_000_000_000_000, 99_999_999_999_999);

    [Fact]
    public async Task Separa_proximos_fila_e_passados_de_todas_as_regulacoes()
    {
        var paciente = Guid.NewGuid();
        var cns = Cns();
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var futuro = hoje.AddDays(20);
        var agora = DateTime.UtcNow;
        var codigoJaAgendado = N();

        await using (var db = fixture.CriarDbContext())
        {
            SerSolicitacao Ser(SituacaoSer situacao, string recurso, string? agendadoPara) => new()
            {
                Id = Guid.NewGuid(), IdSer = N(), Tipo = TipoRecursoSer.Consulta, Recurso = recurso,
                PacienteNome = "PACIENTE TESTE", PacienteId = paciente, Situacao = situacao,
                AgendadoParaTexto = agendadoPara, SincronizadoEm = agora, CriadoEm = agora,
            };
            db.SerSolicitacoes.AddRange(
                Ser(SituacaoSer.EmFila, "CONSULTA EM OFTALMOLOGIA", null),
                Ser(SituacaoSer.Agendada, "CONSULTA EM CARDIOLOGIA", $"{futuro:dd/MM/yyyy} 09:30 - HOSPITAL X"),
                Ser(SituacaoSer.Agendada, "CONSULTA ANTIGA", "10/02/2020 08:00 - HOSPITAL Y"),
                Ser(SituacaoSer.ChegadaConfirmada, "CONSULTA COMPARECEU", "05/03/2021 10:00 - HOSPITAL Z"),
                Ser(SituacaoSer.Agendada, "CONSULTA SEM DATA", "a confirmar"),
                Ser(SituacaoSer.Cancelada, "CONSULTA CANCELADA", null));

            db.SernitSolicitacoes.Add(new SernitSolicitacao
            {
                Id = Guid.NewGuid(), IdSernit = N(), Tipo = TipoRecursoSernit.Exame, Recurso = "ULTRASSONOGRAFIA",
                PacienteNome = "PACIENTE TESTE", PacienteId = paciente, Situacao = SituacaoSernit.Pendente,
                SincronizadoEm = agora, CriadoEm = agora,
            });

            db.EsusSgSolicitacoes.Add(new EsusSgSolicitacao
            {
                Id = Guid.NewGuid(), IdEsusSg = N(), Tipo = TipoRecursoEsusSg.Exame,
                Recurso = "TRATAMENTO DE GLAUCOMA (PPI)", PacienteNome = "PACIENTE TESTE", PacienteId = paciente,
                Situacao = SituacaoEsusSg.SaiuDaFila, SincronizadoEm = agora, CriadoEm = agora,
            });

            var unidade = new Unidade { Id = Guid.NewGuid(), Nome = "POLICLINICA TESTE", CriadoEm = agora };
            db.Unidades.Add(unidade);
            Solicitacao Sisreg(string procedimento, StatusSolicitacao status, DateTime? dataAgendada, string? codigo) => new()
            {
                Id = Guid.NewGuid(), PacienteId = paciente, Categoria = CategoriaSolicitacao.Consulta,
                ProcedimentoTexto = procedimento, UnidadeExecutanteId = unidade.Id, SolicitanteNome = "DR TESTE",
                Status = status, Prioridade = PrioridadeSolicitacao.Eletiva, DataAgendada = dataAgendada,
                CodigoSolicitacao = codigo, CriadoEm = agora,
            };
            db.Solicitacoes.AddRange(
                Sisreg("CONSULTA EM DERMATOLOGIA", StatusSolicitacao.Agendada,
                    FusoBrasilia.DeBrasiliaParaUtc(futuro.ToDateTime(new TimeOnly(14, 0))), codigoJaAgendado),
                Sisreg("CONSULTA EM UROLOGIA", StatusSolicitacao.Agendada,
                    FusoBrasilia.DeBrasiliaParaUtc(new DateTime(2021, 6, 1, 8, 0, 0)), N()),
                // Pedido local sem data: não há como dizer ao paciente que ele está no SISREG.
                Sisreg("PEDIDO MANUAL", StatusSolicitacao.Solicitada, null, null));

            SisregFilaPendente Fila(string procedimento, string cnsDaLinha, string? codigo = null,
                DateTime? ultimoVisto = null, DateTime? saiu = null) => new()
            {
                Id = Guid.NewGuid(), CodigoSolicitacao = codigo ?? N(), Cns = cnsDaLinha, ProcedimentoNome = procedimento,
                DataSolicitacao = hoje.AddDays(-200), PrimeiroVistoEm = agora.AddDays(-30),
                UltimoVistoEm = ultimoVisto ?? agora, SaiuEm = saiu, CriadoEm = agora,
            };
            db.SisregFilaPendentes.AddRange(
                Fila("GRUPO - ULTRASONOGRAFIA", cns),
                Fila("GRUPO - CE UROLOGIA", cns),
                Fila("SAIU DA FILA", cns, saiu: agora.AddDays(-1)),
                Fila("NAO VISTO NA ULTIMA LEITURA", cns, ultimoVisto: agora.AddDays(-10)),
                Fila("JA VIROU AGENDAMENTO", cns, codigo: codigoJaAgendado),
                Fila("DE OUTRO PACIENTE", Cns()));
            await db.SaveChangesAsync();
        }

        await using var leitura = fixture.CriarDbContext();
        var agenda = await new AgendamentosPacienteService(leitura).AgendaDoPacienteAsync(paciente, [cns]);

        MomentoAgendamentoPaciente Momento(string trecho) =>
            Assert.Single(agenda, a => a.Item.Descricao.Contains(trecho)).Momento;

        Assert.Equal(MomentoAgendamentoPaciente.Proximo, Momento("CARDIOLOGIA"));
        Assert.Equal(MomentoAgendamentoPaciente.Proximo, Momento("DERMATOLOGIA"));

        Assert.Equal(MomentoAgendamentoPaciente.NaFila, Momento("OFTALMOLOGIA"));
        Assert.Equal(MomentoAgendamentoPaciente.NaFila, Momento("ULTRASSONOGRAFIA"));
        Assert.Equal(MomentoAgendamentoPaciente.NaFila, Momento("ULTRASONOGRAFIA"));
        Assert.Equal(MomentoAgendamentoPaciente.NaFila, Momento("CE UROLOGIA"));

        Assert.Equal(MomentoAgendamentoPaciente.Passado, Momento("ANTIGA"));
        Assert.Equal(MomentoAgendamentoPaciente.Passado, Momento("COMPARECEU"));
        Assert.Equal(MomentoAgendamentoPaciente.Passado, Momento("CANCELADA"));
        Assert.Equal(MomentoAgendamentoPaciente.Passado, Momento("CONSULTA EM UROLOGIA"));

        // Na fila é só "na fila": a pendência do SERNIT não leva a situação de origem.
        Assert.All(agenda.Where(a => a.Momento == MomentoAgendamentoPaciente.NaFila), a =>
        {
            Assert.Equal(SituacaoAgendamentoPaciente.EmFila, a.Item.Situacao);
            Assert.Null(a.Item.SituacaoOrigem);
            Assert.Null(a.Item.DataHora);
        });

        // A fila do SISREG diz consulta × exame pelo nome do procedimento.
        Assert.Equal("Exame", Assert.Single(agenda, a => a.Item.Descricao == "GRUPO - ULTRASONOGRAFIA").Item.Tipo);
        Assert.Equal("Consulta", Assert.Single(agenda, a => a.Item.Descricao == "GRUPO - CE UROLOGIA").Item.Tipo);

        // Passado sem chegada nem falta apontada não fica "Agendado".
        Assert.Equal(SituacaoAgendamentoPaciente.SemRegistroDeChegada,
            Assert.Single(agenda, a => a.Item.Descricao.Contains("ANTIGA")).Item.Situacao);

        Assert.DoesNotContain(agenda, a => a.Item.Descricao.Contains("SEM DATA")
            || a.Item.Descricao.Contains("GLAUCOMA")
            || a.Item.Descricao.Contains("PEDIDO MANUAL")
            || a.Item.Descricao.Contains("SAIU DA FILA")
            || a.Item.Descricao.Contains("NAO VISTO")
            || a.Item.Descricao.Contains("JA VIROU")
            || a.Item.Descricao.Contains("OUTRO PACIENTE"));
    }

    [Fact]
    public async Task App_mostra_resposta_no_proximo_e_exame_feito_como_realizado()
    {
        var paciente = Guid.NewGuid();
        var futuro = FusoBrasilia.HojeEmBrasilia().AddDays(10);
        var agora = DateTime.UtcNow;

        await using (var db = fixture.CriarDbContext())
        {
            var proximo = await SeedSolicitacao.CriarAsync(db, paciente,
                dataAgendada: FusoBrasilia.DeBrasiliaParaUtc(futuro.ToDateTime(new TimeOnly(8, 0))));
            proximo.Solicitacao!.Status = StatusSolicitacao.Agendada;

            var feito = await SeedSolicitacao.CriarAsync(db, paciente,
                dataAgendada: FusoBrasilia.DeBrasiliaParaUtc(new DateTime(2025, 3, 10, 9, 0, 0)));
            feito.Solicitacao!.Status = StatusSolicitacao.Agendada;
            feito.Status = StatusSolicitacaoExame.Laudada;

            db.SerSolicitacoes.Add(new SerSolicitacao
            {
                Id = Guid.NewGuid(), IdSer = N(), Tipo = TipoRecursoSer.Exame, Recurso = "TOMOGRAFIA",
                PacienteNome = "PACIENTE TESTE", PacienteId = paciente, Situacao = SituacaoSer.Pendente,
                SincronizadoEm = agora, CriadoEm = agora,
            });
            await db.SaveChangesAsync();
        }

        await using var leitura = fixture.CriarDbContext();
        var lista = await Servico(leitura).ListarAgendamentosAsync(paciente, "exame");

        // Próximo → na fila → passado.
        Assert.Equal(["Proximo", "NaFila", "Passado"], lista.Select(a => a.Momento));

        var proximoDto = lista[0];
        Assert.True(proximoDto.PodeResponder);
        Assert.Equal("Pendente", proximoDto.StatusConfirmacao);
        Assert.NotNull(proximoDto.SolicitacaoExameId);
        Assert.Equal("regulação municipal (SISREG)", proximoDto.Origem);

        var filaDto = lista[1];
        Assert.True(filaDto.NaFila);
        Assert.Null(filaDto.InicioEm);
        Assert.Equal("Na fila", filaDto.Status);
        Assert.Equal("regulação estadual (SER)", filaDto.Origem);

        var passadoDto = lista[2];
        Assert.Equal("Realizado", passadoDto.Status);
        Assert.False(passadoDto.PodeResponder);
        Assert.Null(passadoDto.StatusConfirmacao);
        // Ticket (chave do dia, confirmação) é coisa do próximo.
        Assert.Null(passadoDto.SolicitacaoExameId);

        // Filtro de consulta não traz exame.
        Assert.Empty(await Servico(leitura).ListarAgendamentosAsync(paciente, "consulta"));
    }

    private static CidadaoClinicoService Servico(SmsMaisDbContext db)
    {
        // Sem cadastro no hub: a agenda sai sem a fila do SISREG, e o resto não depende dele.
        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<PacienteDto>(new HttpRequestException("hub fora do ar")));
        return new CidadaoClinicoService(
            db, Substitute.For<IAnexosService>(), Substitute.For<ILaudosService>(),
            Substitute.For<ILaudoAssinaturaService>(), Substitute.For<IExameImagensPdfService>(),
            Substitute.For<IChaveConfirmacaoSisregService>(), new AgendamentosPacienteService(db), pacientes,
            NullLogger<CidadaoClinicoService>.Instance);
    }
}
