using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Pacientes.Agendamentos;
using SMSMais.Core.Pacientes.Agendamentos.Dtos;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// O que o paciente vê da regulação externa (robô do WhatsApp e app do cidadão) — regra do Bernardo
/// em 30/09/2026: pedido NA FILA aparece só como "na fila" (PENDENTE também, sem o motivo); pedido
/// AGENDADO aparece com data e local; o resto (agendamento passado, sem data legível, cancelado,
/// saída da fila) não aparece.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class RegulacaoParaPacienteTests(PostgresFixture fixture)
{
    private static string N() => Random.Shared.NextInt64(1_000_000, 9_999_999).ToString();

    [Fact]
    public async Task Fila_e_pendencia_viram_so_na_fila_e_agendado_futuro_vem_com_data_e_local()
    {
        var paciente = Guid.NewGuid();
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var futuro = hoje.AddDays(20);
        var agora = DateTime.UtcNow;

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
                // Agendamento que já passou: não é "próximo".
                Ser(SituacaoSer.Agendada, "CONSULTA ANTIGA", "10/02/2020 08:00 - HOSPITAL Y"),
                // Agendado sem data legível: para o paciente só se afirma agendamento com data.
                Ser(SituacaoSer.Agendada, "CONSULTA SEM DATA", "a confirmar"),
                Ser(SituacaoSer.Cancelada, "CONSULTA CANCELADA", null));

            db.SernitSolicitacoes.Add(new SernitSolicitacao
            {
                Id = Guid.NewGuid(), IdSernit = N(), Tipo = TipoRecursoSernit.Exame, Recurso = "ULTRASSONOGRAFIA",
                PacienteNome = "PACIENTE TESTE", PacienteId = paciente, Situacao = SituacaoSernit.Pendente,
                SincronizadoEm = agora, CriadoEm = agora,
            });

            db.EsusSgSolicitacoes.AddRange(
                new EsusSgSolicitacao
                {
                    Id = Guid.NewGuid(), IdEsusSg = N(), Tipo = TipoRecursoEsusSg.Exame,
                    Recurso = "TRATAMENTO DE RETINA (PPI)", PacienteNome = "PACIENTE TESTE", PacienteId = paciente,
                    Situacao = SituacaoEsusSg.Agendada, DataAgendada = futuro,
                    DataHoraAgendadaTexto = $"{futuro:dd/MM/yyyy} 13:15:00", UnidadeExecutora = "ABRAE  2297523",
                    SincronizadoEm = agora, CriadoEm = agora,
                },
                new EsusSgSolicitacao
                {
                    Id = Guid.NewGuid(), IdEsusSg = N(), Tipo = TipoRecursoEsusSg.Exame,
                    Recurso = "TRATAMENTO DE GLAUCOMA (PPI)", PacienteNome = "PACIENTE TESTE", PacienteId = paciente,
                    Situacao = SituacaoEsusSg.SaiuDaFila, SincronizadoEm = agora, CriadoEm = agora,
                });
            await db.SaveChangesAsync();
        }

        await using var leitura = fixture.CriarDbContext();
        var itens = await new AgendamentosPacienteService(leitura).RegulacaoParaPacienteAsync(paciente);

        Assert.Equal(4, itens.Count);

        // Pendência do SERNIT sai como "na fila", sem a situação de origem (o motivo nunca sai).
        var pendente = Assert.Single(itens, i => i.Origem == OrigemAgendamentoPaciente.Sernit);
        Assert.Equal(SituacaoAgendamentoPaciente.EmFila, pendente.Situacao);
        Assert.Null(pendente.SituacaoOrigem);

        Assert.Contains(itens, i => i.Origem == OrigemAgendamentoPaciente.Ser
            && i.Situacao == SituacaoAgendamentoPaciente.EmFila && i.Descricao.Contains("OFTALMOLOGIA"));

        var cardio = Assert.Single(itens, i => i.Descricao.Contains("CARDIOLOGIA"));
        Assert.Equal(SituacaoAgendamentoPaciente.Agendado, cardio.Situacao);
        Assert.Equal(futuro.ToDateTime(new TimeOnly(9, 30)), cardio.DataHora);

        var retina = Assert.Single(itens, i => i.Origem == OrigemAgendamentoPaciente.EsusSg);
        Assert.Equal(SituacaoAgendamentoPaciente.Agendado, retina.Situacao);
        Assert.Equal("ABRAE  2297523", retina.Unidade);

        Assert.DoesNotContain(itens, i => i.Descricao.Contains("ANTIGA") || i.Descricao.Contains("SEM DATA")
            || i.Descricao.Contains("CANCELADA") || i.Descricao.Contains("GLAUCOMA"));

        // Agendados (com data) antes dos que estão na fila.
        Assert.NotNull(itens[0].DataHora);
        Assert.Null(itens[^1].DataHora);
    }

    [Theory]
    [InlineData(OrigemAgendamentoPaciente.Ser, "regulação estadual (SER)")]
    [InlineData(OrigemAgendamentoPaciente.Sernit, "regulação de Niterói")]
    [InlineData(OrigemAgendamentoPaciente.EsusSg, "regulação de São Gonçalo")]
    public void Origem_em_linguagem_do_paciente(OrigemAgendamentoPaciente origem, string esperado) =>
        Assert.Equal(esperado, AgendamentosPacienteService.DescreverRegulacaoParaPaciente(origem));
}
