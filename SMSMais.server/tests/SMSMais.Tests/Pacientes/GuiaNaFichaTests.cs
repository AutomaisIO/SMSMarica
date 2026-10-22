using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Pacientes.Agendamentos;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sisreg;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// A guia do agendamento na aba Agendamentos da ficha — pedido do Bernardo em 22/10/2026: abrir a
/// guia do paciente direto da linha, para imprimir ou baixar. A guia é a que a fila já guardou no
/// acervo (ADR-0076); a ficha só aponta para ela.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class GuiaNaFichaTests(PostgresFixture fixture)
{
    private static string N() => Random.Shared.NextInt64(100_000_000, 999_999_999).ToString();

    [Fact]
    public async Task Linha_do_SER_traz_a_guia_do_agendamento_vigente()
    {
        var paciente = Guid.NewGuid();
        var futuro = FusoBrasilia.HojeEmBrasilia().AddDays(15);
        var antes = FusoBrasilia.HojeEmBrasilia().AddDays(5);
        var agora = DateTime.UtcNow;
        var espelho = new SerSolicitacao
        {
            Id = Guid.NewGuid(), IdSer = N(), Tipo = TipoRecursoSer.Exame, Recurso = "RESSONANCIA MAGNETICA",
            PacienteNome = "PACIENTE TESTE", PacienteId = paciente, Situacao = SituacaoSer.Agendada,
            AgendadoParaTexto = $"{futuro:dd/MM/yyyy} 07:00 - HOSPITAL X", SincronizadoEm = agora, CriadoEm = agora,
        };
        // Remarcado: a data antiga ficou de histórico, com a guia dela; a vigente tem a própria.
        var antigo = Agendamento(espelho, paciente, antes, SituacaoAgendamentoExterno.Reagendado, agora.AddDays(-3));
        var vigente = Agendamento(espelho, paciente, futuro, SituacaoAgendamentoExterno.Ativo, agora);
        var guiaAntiga = Documento(paciente, agora);
        var guiaVigente = Documento(paciente, agora);

        await using (var db = fixture.CriarDbContext())
        {
            db.SerSolicitacoes.Add(espelho);
            db.AgendamentosExternos.AddRange(antigo, vigente);
            db.DocumentosPaciente.AddRange(guiaAntiga, guiaVigente);
            db.GuiasAgendamento.AddRange(
                Guia(SistemaRegulacao.Ser, paciente, antes, guiaAntiga.Id, agora, agendamentoExternoId: antigo.Id),
                Guia(SistemaRegulacao.Ser, paciente, futuro, guiaVigente.Id, agora, agendamentoExternoId: vigente.Id));
            await db.SaveChangesAsync();
        }

        await using var leitura = fixture.CriarDbContext();
        var linha = Assert.Single((await new AgendamentosPacienteService(leitura).ListarPorPacienteAsync(paciente)).Proximos);

        Assert.Equal(guiaVigente.Id, linha.GuiaDocumentoId);
    }

    [Fact]
    public async Task Linha_do_SISREG_so_traz_a_guia_da_data_em_que_esta()
    {
        var paciente = Guid.NewGuid();
        var futuro = FusoBrasilia.HojeEmBrasilia().AddDays(10);
        var agora = DateTime.UtcNow;
        var unidade = new Unidade { Id = Guid.NewGuid(), Nome = "POLICLINICA TESTE", CriadoEm = agora };
        Solicitacao Sisreg(string procedimento, DateOnly dia) => new()
        {
            Id = Guid.NewGuid(), PacienteId = paciente, Categoria = CategoriaSolicitacao.Consulta,
            ProcedimentoTexto = procedimento, UnidadeExecutanteId = unidade.Id, SolicitanteNome = "DR TESTE",
            Status = StatusSolicitacao.Agendada, Prioridade = PrioridadeSolicitacao.Eletiva,
            DataAgendada = FusoBrasilia.DeBrasiliaParaUtc(dia.ToDateTime(new TimeOnly(14, 0))),
            CodigoSolicitacao = N(), CriadoEm = agora,
        };
        var comGuia = Sisreg("CONSULTA EM DERMATOLOGIA", futuro);
        // Remarcada depois de a guia vir: a guia é da data antiga e não vale para a linha.
        var remarcada = Sisreg("CONSULTA EM UROLOGIA", futuro.AddDays(7));
        var documento = Documento(paciente, agora);
        var daDataAntiga = Documento(paciente, agora);

        await using (var db = fixture.CriarDbContext())
        {
            db.Unidades.Add(unidade);
            db.Solicitacoes.AddRange(comGuia, remarcada);
            db.DocumentosPaciente.AddRange(documento, daDataAntiga);
            db.GuiasAgendamento.AddRange(
                Guia(SistemaRegulacao.Sisreg, paciente, futuro, documento.Id, agora, solicitacaoId: comGuia.Id),
                Guia(SistemaRegulacao.Sisreg, paciente, futuro, daDataAntiga.Id, agora, solicitacaoId: remarcada.Id));
            await db.SaveChangesAsync();
        }

        await using var leitura = fixture.CriarDbContext();
        var proximos = (await new AgendamentosPacienteService(leitura).ListarPorPacienteAsync(paciente)).Proximos;

        Assert.Equal(documento.Id, Assert.Single(proximos, i => i.Id == comGuia.Id).GuiaDocumentoId);
        Assert.Null(Assert.Single(proximos, i => i.Id == remarcada.Id).GuiaDocumentoId);
    }

    private static AgendamentoExterno Agendamento(
        SerSolicitacao espelho, Guid paciente, DateOnly data, SituacaoAgendamentoExterno situacao, DateTime criadoEm) => new()
    {
        Id = Guid.CreateVersion7(), Sistema = SistemaRegulacao.Ser, OrigemId = espelho.Id, NumeroExterno = espelho.IdSer,
        PacienteId = paciente, Tipo = TipoAgendamento.Exame, Procedimento = espelho.Recurso, Data = data,
        Situacao = situacao, CriadoEm = criadoEm,
    };

    private static DocumentoPaciente Documento(Guid paciente, DateTime agora) => new()
    {
        Id = Guid.CreateVersion7(), PacienteId = paciente, Titulo = "Guia", NomeArquivo = "guia.pdf",
        MimeType = "application/pdf", TamanhoBytes = 10, HashSha256 = new string('a', 64),
        ChaveArmazenamento = $"teste/{Guid.NewGuid()}.pdf", Origem = OrigemDocumentoPaciente.GuiaAgendamento,
        Situacao = SituacaoDocumentoPaciente.Aceito, CriadoEm = agora,
    };

    private static GuiaAgendamento Guia(
        SistemaRegulacao sistema, Guid paciente, DateOnly data, Guid documentoId, DateTime agora,
        Guid? agendamentoExternoId = null, Guid? solicitacaoId = null) => new()
    {
        Id = Guid.CreateVersion7(), Sistema = sistema, AgendamentoExternoId = agendamentoExternoId,
        SolicitacaoId = solicitacaoId, PacienteId = paciente, DataAtendimento = data,
        Situacao = SituacaoGuiaAgendamento.Obtida, DocumentoPacienteId = documentoId, CriadoEm = agora,
    };
}
