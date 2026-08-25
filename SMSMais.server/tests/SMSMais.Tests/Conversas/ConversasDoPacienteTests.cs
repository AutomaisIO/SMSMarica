using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Conversas;
using SMSMais.Core.Institucional;
using SMSMais.Core.Institucional.Dtos;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Conversas;

/// <summary>
/// Aba "Conversas" da ficha do paciente: sessões derivadas da linha do tempo de
/// <c>whatsapp_mensagem</c> (blocos por 24h+ de silêncio, por telefone). Invariantes: automação
/// e legado (sem conversa_id) entram; telefone de família entra marcado "pelo telefone";
/// telefone alheio ao paciente é 404 na leitura de mensagens.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ConversasDoPacienteTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Sessoes_separam_por_silencio_de_24h_e_vem_mais_recentes_primeiro()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var fone = FoneUnico();
        var t0 = DateTime.UtcNow.AddDays(-10);

        // Sessão 1: três mensagens próximas. Silêncio de 30h. Sessão 2: duas mensagens.
        await CriarMensagemAsync(db, fone, t0, DirecaoMensagem.Entrada, pacienteId);
        await CriarMensagemAsync(db, fone, t0.AddMinutes(5), DirecaoMensagem.Saida, pacienteId, autor: "ANA");
        await CriarMensagemAsync(db, fone, t0.AddHours(2), DirecaoMensagem.Entrada, pacienteId);
        await CriarMensagemAsync(db, fone, t0.AddHours(32), DirecaoMensagem.Entrada, pacienteId);
        await CriarMensagemAsync(db, fone, t0.AddHours(33), DirecaoMensagem.Saida, pacienteId, autor: "BRUNO");

        var sessoes = await CriarServico(db, pacienteId).ListarSessoesAsync(pacienteId);

        Assert.Equal(2, sessoes.Count);
        // Mais recente primeiro.
        Assert.Equal(2, sessoes[0].QtdMensagens);
        Assert.Equal(["BRUNO"], sessoes[0].Operadores);
        Assert.Equal(3, sessoes[1].QtdMensagens);
        Assert.Equal(2, sessoes[1].QtdRecebidas);
        Assert.Equal(1, sessoes[1].QtdEnviadas);
        Assert.Equal(["ANA"], sessoes[1].Operadores);
        Assert.True(sessoes[1].Inicio < sessoes[0].Inicio);
        Assert.False(sessoes[0].PeloTelefone);
    }

    [Fact]
    public async Task Saida_sem_autor_marca_a_sessao_como_automatica()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var fone = FoneUnico();
        var t0 = DateTime.UtcNow.AddDays(-1);

        await CriarMensagemAsync(db, fone, t0, DirecaoMensagem.Saida, pacienteId); // automação
        await CriarMensagemAsync(db, fone, t0.AddMinutes(3), DirecaoMensagem.Saida, pacienteId, autor: "ANA");

        var sessoes = await CriarServico(db, pacienteId).ListarSessoesAsync(pacienteId);

        var sessao = Assert.Single(sessoes);
        Assert.True(sessao.TemAutomaticas);
        Assert.Equal(["ANA"], sessao.Operadores);
    }

    [Fact]
    public async Task Telefone_do_cadastro_com_mensagens_de_OUTRA_pessoa_entra_marcado_pelo_telefone()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var outroPaciente = Guid.NewGuid();
        var foneDaCasa = FoneUnico();
        var t0 = DateTime.UtcNow.AddDays(-2);

        // Celular de família: as mensagens estão vinculadas a OUTRO cadastro, mas o telefone
        // é o principal DESTE paciente.
        await CriarMensagemAsync(db, foneDaCasa, t0, DirecaoMensagem.Entrada, outroPaciente);
        await CriarMensagemAsync(db, foneDaCasa, t0.AddMinutes(10), DirecaoMensagem.Saida, outroPaciente, autor: "ANA");

        var sessoes = await CriarServico(db, pacienteId, telefonePrincipal: foneDaCasa)
            .ListarSessoesAsync(pacienteId);

        var sessao = Assert.Single(sessoes);
        Assert.True(sessao.PeloTelefone);
        Assert.Equal(foneDaCasa, sessao.Telefone);
    }

    [Fact]
    public async Task Mensagens_da_sessao_respeitam_a_faixa_e_vem_cronologicas()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var fone = FoneUnico();
        var t0 = DateTime.UtcNow.AddDays(-10);

        await CriarMensagemAsync(db, fone, t0, DirecaoMensagem.Entrada, pacienteId, conteudo: "primeira");
        await CriarMensagemAsync(db, fone, t0.AddHours(1), DirecaoMensagem.Saida, pacienteId, autor: "ANA", conteudo: "resposta");
        await CriarMensagemAsync(db, fone, t0.AddHours(40), DirecaoMensagem.Entrada, pacienteId, conteudo: "outra sessão");

        var servico = CriarServico(db, pacienteId);
        var sessoes = await servico.ListarSessoesAsync(pacienteId);
        var antiga = sessoes[^1];

        var mensagens = await servico.ObterMensagensDaSessaoAsync(
            pacienteId, antiga.Telefone, antiga.Inicio, antiga.Fim);

        Assert.Equal(2, mensagens.Count);
        Assert.Equal("primeira", mensagens[0].Conteudo);
        Assert.Equal("resposta", mensagens[1].Conteudo);
        Assert.Equal("ANA", mensagens[1].AutorNomeExibicao);
    }

    [Fact]
    public async Task Telefone_alheio_ao_paciente_e_404_na_leitura_de_mensagens()
    {
        await using var db = fixture.CriarDbContext();
        var pacienteId = Guid.NewGuid();
        var foneDeTerceiro = FoneUnico();
        await CriarMensagemAsync(db, foneDeTerceiro, DateTime.UtcNow, DirecaoMensagem.Entrada, Guid.NewGuid());

        await Assert.ThrowsAsync<NaoEncontradoException>(() =>
            CriarServico(db, pacienteId).ObterMensagensDaSessaoAsync(
                pacienteId, foneDeTerceiro, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)));
    }

    // ===================== HELPERS =====================

    private static ConversasDoPacienteService CriarServico(
        SmsMaisDbContext db, Guid pacienteId, string? telefonePrincipal = null)
    {
        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ObterPorIdAsync(pacienteId, Arg.Any<CancellationToken>())
            .Returns(Paciente(pacienteId, telefonePrincipal));

        var instituicao = Substitute.For<IInstituicaoService>();
        instituicao.ObterAsync(Arg.Any<CancellationToken>()).Returns(Instituicao());

        return new ConversasDoPacienteService(db, pacientes, instituicao);
    }

    private static PacienteDto Paciente(Guid id, string? telefonePrincipal) =>
        new(id, "PACIENTE TESTE", "12345678909", null, 0, 0, true, DateTime.UtcNow,
            null, null, default, default, default, default, null, null, "Brasileira",
            null, null, null, null, telefonePrincipal, null, null, null, null,
            null, null, default, default, [], [], [], [], null, null, null);

    private static InstituicaoDto Instituicao() =>
        new("SMS TESTE", "SECRETARIA TESTE", "SMS", null, null, null, "RJ", 21,
            null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null);

    /// <summary>Celular canônico 55 21 9XXXXXXXX — chave de whatsapp_mensagem.telefone.</summary>
    private static string FoneUnico() => $"55219{Random.Shared.Next(10_000_000, 99_999_999)}";

    private static async Task CriarMensagemAsync(
        SmsMaisDbContext db, string telefone, DateTime ocorridoEm, DirecaoMensagem direcao,
        Guid? pacienteId = null, string? autor = null, string? conteudo = null)
    {
        db.MensagensWhatsApp.Add(new MensagemWhatsApp
        {
            Id = Guid.CreateVersion7(),
            PacienteId = pacienteId,
            Telefone = telefone,
            Direcao = direcao,
            Conteudo = conteudo ?? "msg de teste",
            Status = direcao == DirecaoMensagem.Entrada
                ? StatusMensagemWhatsApp.Recebida
                : StatusMensagemWhatsApp.Enviada,
            AutorNomeExibicao = autor,
            OcorridoEm = ocorridoEm,
            CriadoEm = ocorridoEm,
        });
        await db.SaveChangesAsync();
    }
}
