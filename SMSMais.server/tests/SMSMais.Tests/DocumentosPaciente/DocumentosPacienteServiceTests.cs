using System.Collections.Concurrent;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using SMSMais.Core.Armazenamento;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Cidadao.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Conversas;
using SMSMais.Core.Conversas.Midias;
using SMSMais.Core.DocumentosPaciente;
using SMSMais.Core.Exames;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.DocumentosPaciente;

/// <summary>
/// Acervo do paciente ("Exames anexados" perene) e mídia do WhatsApp.
///
/// <para>O que estes testes guardam: o mesmo arquivo não vira dois documentos no cadastro; o que
/// o paciente manda entra pendente e não pode ser anexado em solicitação antes de alguém aceitar;
/// e as duas travas contra quem manda arquivo em massa — 10 pendentes por paciente (app) e 10 por
/// número (WhatsApp).</para>
/// </summary>
[Collection(nameof(PostgresCollection))]
public class DocumentosPacienteServiceTests(PostgresFixture fixture)
{
    private sealed class ArmazenamentoFake : IArmazenamentoArquivos
    {
        public ConcurrentDictionary<string, byte[]> Arquivos { get; } = new();

        public string MontarChaveDocumento(Guid pacienteId, Guid documentoId, string extensao = "pdf") =>
            $"teste/{pacienteId:D}/{documentoId:D}.{extensao}";

        public Task SalvarAsync(string chave, byte[] conteudo, CancellationToken cancellationToken = default)
        {
            Arquivos[chave] = conteudo;
            return Task.CompletedTask;
        }

        public Task<byte[]?> LerAsync(string chave, CancellationToken cancellationToken = default) =>
            Task.FromResult(Arquivos.TryGetValue(chave, out var c) ? c : null);

        public Task ExcluirAsync(string chave, CancellationToken cancellationToken = default)
        {
            Arquivos.TryRemove(chave, out _);
            return Task.CompletedTask;
        }
    }

    private static DocumentosPacienteService Servico(SmsMaisDbContext db, IArmazenamentoArquivos armazenamento)
    {
        var clinico = Substitute.For<ICidadaoClinicoService>();
        clinico.ListarExamesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ExameResumoDto>>([]));
        clinico.ListarLaudosAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<LaudoResumoDto>>([]));
        return new DocumentosPacienteService(
            db, armazenamento, clinico, Substitute.For<ILaudoAssinaturaService>(),
            Substitute.For<IExameImagensPdfService>(), new UsuarioAtualAccessorFake(Guid.NewGuid()),
            NullLogger<DocumentosPacienteService>.Instance);
    }

    private static byte[] Pdf(string marca) => Encoding.UTF8.GetBytes("%PDF-1.4 " + marca);

    private static NovoDocumentoPaciente Novo(
        Guid pacienteId, byte[] conteudo, SituacaoDocumentoPaciente situacao, string titulo = "Exame") =>
        new(pacienteId, titulo, null, "exame.pdf", "application/pdf", conteudo,
            situacao == SituacaoDocumentoPaciente.Pendente ? OrigemDocumentoPaciente.AppCidadao : OrigemDocumentoPaciente.Painel,
            null, situacao);

    [Fact]
    public async Task Mesmo_arquivo_no_mesmo_paciente_vira_um_documento_so()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db, new ArmazenamentoFake());
        var paciente = Guid.NewGuid();

        var a = await servico.AdicionarAsync(Novo(paciente, Pdf("a"), SituacaoDocumentoPaciente.Aceito));
        var b = await servico.AdicionarAsync(Novo(paciente, Pdf("a"), SituacaoDocumentoPaciente.Aceito, "Outro nome"));

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(1, await db.DocumentosPaciente.CountAsync(d => d.PacienteId == paciente));
    }

    [Fact]
    public async Task Pendente_nao_e_anexavel_ate_alguem_aceitar()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db, new ArmazenamentoFake());
        var paciente = Guid.NewGuid();

        var doc = await servico.AdicionarAsync(Novo(paciente, Pdf("p"), SituacaoDocumentoPaciente.Pendente));

        // Fora da lista de anexáveis e recusado pela chave…
        Assert.Empty(await servico.ListarAsync(paciente, incluirPendentes: false));
        await Assert.ThrowsAsync<ConflitoException>(() => servico.ObterConteudoPorChaveAsync(paciente, doc.Chave));
        // …mas a equipe (e o próprio paciente) consegue abrir para conferir.
        var conteudo = await servico.ObterConteudoAsync(paciente, TipoItemAcervo.Documento, doc.Id, permitirPendente: true);
        Assert.Equal(Pdf("p"), conteudo.Conteudo);

        await servico.AceitarAsync(paciente, doc.Id, new EditarDocumentoPacienteRequest("Raio-X do tórax", "de 2025"));

        var lista = await servico.ListarAsync(paciente, incluirPendentes: false);
        var item = Assert.Single(lista);
        Assert.Equal("Raio-X do tórax", item.Titulo);
        Assert.Equal(SituacaoDocumentoPaciente.Aceito, item.Situacao);
    }

    [Fact]
    public async Task Mesmo_arquivo_chegando_aceito_promove_o_pendente()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db, new ArmazenamentoFake());
        var paciente = Guid.NewGuid();

        var pendente = await servico.AdicionarAsync(Novo(paciente, Pdf("x"), SituacaoDocumentoPaciente.Pendente));
        var aceito = await servico.AdicionarAsync(Novo(paciente, Pdf("x"), SituacaoDocumentoPaciente.Aceito));

        Assert.Equal(pendente.Id, aceito.Id);
        Assert.Equal(SituacaoDocumentoPaciente.Aceito, aceito.Situacao);
    }

    [Fact]
    public async Task Decimo_primeiro_pendente_do_paciente_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db, new ArmazenamentoFake());
        var paciente = Guid.NewGuid();

        for (var i = 0; i < IDocumentosPacienteService.LimitePendentes; i++)
        {
            await servico.AdicionarAsync(Novo(paciente, Pdf($"n{i}"), SituacaoDocumentoPaciente.Pendente));
        }

        var ex = await Assert.ThrowsAsync<ConflitoException>(
            () => servico.AdicionarAsync(Novo(paciente, Pdf("excesso"), SituacaoDocumentoPaciente.Pendente)));
        Assert.Equal("documento.limite_pendentes", ex.Codigo);

        // O que vem da equipe não conta para a trava.
        await servico.AdicionarAsync(Novo(paciente, Pdf("da-equipe"), SituacaoDocumentoPaciente.Aceito));
    }

    [Fact]
    public async Task Tipo_que_nao_e_pdf_nem_imagem_e_recusado()
    {
        await using var db = fixture.CriarDbContext();
        var armazenamento = new ArmazenamentoFake();
        var servico = Servico(db, armazenamento);

        await Assert.ThrowsAsync<ValidacaoException>(() => servico.AdicionarAsync(
            new NovoDocumentoPaciente(Guid.NewGuid(), "Planilha", null, "a.xlsx",
                "application/vnd.ms-excel", Pdf("x"), OrigemDocumentoPaciente.Painel, null,
                SituacaoDocumentoPaciente.Aceito)));
        Assert.Empty(armazenamento.Arquivos);
    }

    // ------------------------------------------------------------------ WhatsApp

    private sealed class ZapFake(byte[] conteudo, string mime) : IZapMidiaCliente
    {
        public int Chamadas { get; private set; }

        public Task<MidiaBaixada> BaixarAsync(string mediaId, long tamanhoMaximo, CancellationToken ct = default)
        {
            Chamadas++;
            return Task.FromResult(new MidiaBaixada(conteudo, mime, null, false));
        }
    }

    private static MensagemWhatsApp MensagemComMidia(string telefone) => new()
    {
        Id = Guid.CreateVersion7(),
        Telefone = telefone,
        Direcao = DirecaoMensagem.Entrada,
        Status = StatusMensagemWhatsApp.Recebida,
        TipoMensagem = TipoMensagem.Imagem,
        MidiaWaId = Random.Shared.NextInt64(1, long.MaxValue).ToString(),
        MidiaMimeType = "image/jpeg",
        MidiaSituacao = SituacaoMidiaWhatsApp.Recebendo,
        OcorridoEm = DateTime.UtcNow,
        CriadoEm = DateTime.UtcNow,
    };

    [Fact]
    public async Task Whatsapp_trava_no_decimo_primeiro_arquivo_pendente_do_numero()
    {
        await using var db = fixture.CriarDbContext();
        var armazenamento = new ArmazenamentoFake();
        var zap = new ZapFake(Encoding.UTF8.GetBytes("jpeg"), "image/jpeg");
        var pacientes = Substitute.For<IPacientesService>();
        var midias = new MidiasConversaService(
            db, zap, armazenamento, Servico(db, armazenamento), pacientes,
            new UsuarioAtualAccessorFake(Guid.NewGuid()), new NotificadorConversaNulo(),
            NullLogger<MidiasConversaService>.Instance);

        var telefone = "5521" + Random.Shared.Next(10_000_000, 99_999_999);
        var mensagens = Enumerable.Range(0, IMidiasConversaService.LimitePendentes + 1)
            .Select(_ => MensagemComMidia(telefone)).ToList();
        foreach (var m in mensagens)
        {
            db.MensagensWhatsApp.Add(m);
            await db.SaveChangesAsync();
            await midias.ProcessarRecebidasAsync(50);
        }

        var situacoes = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.Telefone == telefone)
            .Select(m => m.MidiaSituacao)
            .ToListAsync();
        Assert.Equal(IMidiasConversaService.LimitePendentes, situacoes.Count(s => s == SituacaoMidiaWhatsApp.Pendente));
        Assert.Equal(1, situacoes.Count(s => s == SituacaoMidiaWhatsApp.Bloqueada));
        Assert.Equal(IMidiasConversaService.LimitePendentes, zap.Chamadas); // a 11ª nem foi baixada

        // Decidir uma pendente libera a vaga: a travada baixa ao tentar de novo.
        await midias.DescartarAsync(mensagens[0].Id);
        var travada = mensagens[^1];
        await midias.TentarDeNovoAsync(travada.Id);
        var depois = await db.MensagensWhatsApp.AsNoTracking().FirstAsync(m => m.Id == travada.Id);
        Assert.Equal(SituacaoMidiaWhatsApp.Pendente, depois.MidiaSituacao);
    }

    [Fact]
    public async Task Whatsapp_aceitar_poe_no_cadastro_do_paciente_ligado_a_conversa()
    {
        await using var db = fixture.CriarDbContext();
        var armazenamento = new ArmazenamentoFake();
        var pacientes = Substitute.For<IPacientesService>();
        pacientes.ListarPorTelefoneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<PacienteListItemDto>>([]));
        var acervo = Servico(db, armazenamento);
        var midias = new MidiasConversaService(
            db, new ZapFake(Encoding.UTF8.GetBytes("foto"), "image/jpeg"), armazenamento, acervo, pacientes,
            new UsuarioAtualAccessorFake(Guid.NewGuid()), new NotificadorConversaNulo(),
            NullLogger<MidiasConversaService>.Instance);

        var paciente = Guid.NewGuid();
        var msg = MensagemComMidia("5521" + Random.Shared.Next(10_000_000, 99_999_999));
        msg.PacienteId = paciente;
        db.MensagensWhatsApp.Add(msg);
        await db.SaveChangesAsync();
        await midias.ProcessarRecebidasAsync(50);

        // Paciente sem ligação com a conversa: recusado.
        await Assert.ThrowsAsync<ValidacaoException>(
            () => midias.AceitarAsync(msg.Id, new AceitarMidiaRequest(Guid.NewGuid(), "Receita", null)));

        await midias.AceitarAsync(msg.Id, new AceitarMidiaRequest(paciente, "Receita", "do dia 30"));

        var item = Assert.Single(await acervo.ListarAsync(paciente, incluirPendentes: false));
        Assert.Equal("Receita", item.Titulo);
        Assert.Equal("WhatsApp", item.Origem);
        // A cópia da conversa saiu; o visualizador da conversa passa a ler a do cadastro.
        var conteudo = await midias.ObterConteudoAsync(msg.Id);
        Assert.Equal(Encoding.UTF8.GetBytes("foto"), conteudo.Conteudo);
    }
}
