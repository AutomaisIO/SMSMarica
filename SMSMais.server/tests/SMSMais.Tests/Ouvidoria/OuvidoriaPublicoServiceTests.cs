using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Ouvidoria.Dtos;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;
using static SMSMais.Tests.Ouvidoria.OuvidoriaBancada;

namespace SMSMais.Tests.Ouvidoria;

/// <summary>
/// Canal público (sem login): protocolo + código de acesso (plano §3.2). Regressões impedidas:
/// registro pelo site chegando com canal errado (o painel de canais fica mentiroso); código
/// errado devolvendo algo além de 404 (enumeração de protocolos); evento interno — triagem,
/// anotação, resposta da área — vazando ao cidadão; complementar fora da hora reabrindo o
/// relógio; recurso fora de <c>Respondida</c> ou pela segunda vez.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class OuvidoriaPublicoServiceTests(PostgresFixture fixture)
{
    private static RegistrarManifestacaoPublicaRequest PedidoPublico(
        OuvidoriaTipo tipo = OuvidoriaTipo.Reclamacao,
        OuvidoriaIdentificacao identificacao = OuvidoriaIdentificacao.Identificada)
        => new(tipo, identificacao, Teor, null, null, null, null,
            identificacao == OuvidoriaIdentificacao.Anonima ? null : ManifestanteIdentificado(),
            null, null);

    [Fact]
    public async Task Registrar_pelo_publico_fixa_canal_SitePublico_e_origem_Cidadao()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Sistema(db);

        var criada = await amb.Publico.RegistrarAsync(PedidoPublico());

        Assert.NotNull(criada.CodigoAcesso);
        var m = await LerAsync(db, criada.Id);
        Assert.Equal(OuvidoriaCanal.SitePublico, m.Canal);
        Assert.Equal(OuvidoriaOrigem.Cidadao, m.Origem);
        Assert.Null(m.CriadoPor);
        var registro = Assert.Single(m.Eventos);
        Assert.Equal("Cidadão", registro.AutorNome);
        Assert.Null(registro.AutorId);
        Assert.Contains("site", registro.Texto);
    }

    [Fact]
    public async Task Acompanhar_com_codigo_errado_lanca_NaoEncontrado()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Sistema(db);
        var criada = await amb.Publico.RegistrarAsync(PedidoPublico());

        await Assert.ThrowsAsync<NaoEncontradoException>(() => amb.Publico.AcompanharAsync(criada.Protocolo, "ZZZZZZZZ"));
        await Assert.ThrowsAsync<NaoEncontradoException>(() => amb.Publico.AcompanharAsync(criada.Protocolo, ""));
        await Assert.ThrowsAsync<NaoEncontradoException>(() => amb.Publico.AcompanharAsync("2000-000000", criada.CodigoAcesso!));
        await Assert.ThrowsAsync<NaoEncontradoException>(() => amb.Publico.AcompanharAsync("", criada.CodigoAcesso!));
    }

    /// <summary>Anônima não tem código: nenhum código abre o acompanhamento dela.</summary>
    [Fact]
    public async Task Acompanhar_anonima_nunca_abre()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Sistema(db);
        var criada = await amb.Publico.RegistrarAsync(PedidoPublico(OuvidoriaTipo.Denuncia, OuvidoriaIdentificacao.Anonima));

        Assert.Null(criada.CodigoAcesso);
        await Assert.ThrowsAsync<NaoEncontradoException>(() => amb.Publico.AcompanharAsync(criada.Protocolo, "ABCD2345"));
    }

    [Fact]
    public async Task Acompanhar_com_codigo_certo_devolve_so_eventos_visiveis()
    {
        await using var db = fixture.CriarDbContext();
        var publico = Sistema(db);
        var criada = await publico.Publico.RegistrarAsync(PedidoPublico());

        // A ouvidoria trabalha por dentro: triagem, anotação, encaminhamento, resposta da área.
        var tecnico = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        var ponto = await CriarPontoAsync(db);
        await tecnico.Manifestacoes.TriarAsync(criada.Id, new TriarRequest(null, null, null, OuvidoriaPrioridade.Alta, null, "Resumo interno", null, null, null));
        await tecnico.Manifestacoes.AnotarAsync(criada.Id, new TextoRequest("Anotação interna com nome de servidor."));
        await tecnico.Manifestacoes.EncaminharAsync(criada.Id, new EncaminharRequest(ponto.Id, null, "Orientação interna à área.", null));
        await tecnico.Manifestacoes.ResponderAreaAsync(criada.Id, new TextoComAnexosRequest("Resposta interna da área.", []));

        // Código como o cidadão digita: minúsculo e com hífen no meio.
        var codigoDigitado = $"{criada.CodigoAcesso![..4]}-{criada.CodigoAcesso[4..]}".ToLowerInvariant();
        var acomp = await publico.Publico.AcompanharAsync(criada.Protocolo.ToLowerInvariant(), codigoDigitado);

        Assert.Equal(criada.Protocolo, acomp.Protocolo);
        Assert.Equal(OuvidoriaStatus.RespondidaPelaArea, acomp.Status);
        Assert.False(acomp.Prorrogada);
        Assert.False(acomp.PodeComplementar);
        Assert.False(acomp.PodeRecorrer);
        Assert.Null(acomp.RespostaConclusiva);

        Assert.Equal(2, acomp.Eventos.Count);
        Assert.Equal(OuvidoriaTipoEvento.Registro, acomp.Eventos[0].Tipo);
        Assert.Equal(OuvidoriaTipoEvento.Encaminhamento, acomp.Eventos[1].Tipo);
        Assert.DoesNotContain(acomp.Eventos, e => e.Tipo is OuvidoriaTipoEvento.Triagem or OuvidoriaTipoEvento.Anotacao or OuvidoriaTipoEvento.RespostaArea);
        Assert.DoesNotContain(acomp.Eventos, e => e.Texto != null && e.Texto.Contains(ponto.Nome));
        Assert.DoesNotContain(acomp.Eventos, e => e.Texto != null && e.Texto.Contains("interna"));
    }

    [Fact]
    public async Task Complementar_so_quando_AguardandoComplementacao()
    {
        await using var db = fixture.CriarDbContext();
        var publico = Sistema(db);
        var criada = await publico.Publico.RegistrarAsync(PedidoPublico());

        var ex = await Assert.ThrowsAsync<ConflitoException>(() =>
            publico.Publico.ComplementarAsync(criada.Protocolo, criada.CodigoAcesso!, "Complemento sem pedido."));
        Assert.Equal("ouvidoria.transicao_invalida", ex.Codigo);

        var tecnico = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        await tecnico.Manifestacoes.PedirComplementacaoAsync(criada.Id, new TextoRequest("Em qual unidade foi?"));

        var antes = await publico.Publico.AcompanharAsync(criada.Protocolo, criada.CodigoAcesso!);
        Assert.True(antes.PodeComplementar);
        Assert.Equal(OuvidoriaStatus.AguardandoComplementacao, antes.Status);

        // Código errado não complementa, mesmo no status certo.
        await Assert.ThrowsAsync<NaoEncontradoException>(() =>
            publico.Publico.ComplementarAsync(criada.Protocolo, "ZZZZZZZZ", "Tentativa alheia."));

        await publico.Publico.ComplementarAsync(criada.Protocolo, criada.CodigoAcesso!, "Foi na UBS do centro.");

        var depois = await publico.Publico.AcompanharAsync(criada.Protocolo, criada.CodigoAcesso!);
        Assert.False(depois.PodeComplementar);
        Assert.Equal(OuvidoriaStatus.EmTriagem, depois.Status);
        var complementacao = Assert.Single(depois.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Complementacao);
        Assert.Equal("Foi na UBS do centro.", complementacao.Texto);

        var m = await LerAsync(db, criada.Id);
        Assert.Null(m.SuspensaEm);
        Assert.True(m.ComplementacaoUsada);
    }

    [Fact]
    public async Task Recorrer_so_quando_Respondida_e_uma_vez()
    {
        await using var db = fixture.CriarDbContext();
        var publico = Sistema(db);
        var criada = await publico.Publico.RegistrarAsync(PedidoPublico());

        var cedo = await Assert.ThrowsAsync<ConflitoException>(() =>
            publico.Publico.RecorrerAsync(criada.Protocolo, criada.CodigoAcesso!, "Recurso antes da resposta."));
        Assert.Equal("ouvidoria.transicao_invalida", cedo.Codigo);

        var tecnico = Como(db, await CriarUsuarioAsync(db), PermOuvidoria());
        await tecnico.Manifestacoes.TriarAsync(criada.Id, new TriarRequest(null, null, null, null, null, null, null, null, null));
        await tecnico.Manifestacoes.ResponderCidadaoAsync(criada.Id,
            new ResponderCidadaoRequest("Apurado com a unidade: a fila seguiu a classificação de risco.", true,
                OuvidoriaResolutividade.Resolvida, OuvidoriaSituacaoFinal.NaoProcede, null));

        var respondida = await publico.Publico.AcompanharAsync(criada.Protocolo, criada.CodigoAcesso!);
        Assert.Equal(OuvidoriaStatus.Respondida, respondida.Status);
        Assert.True(respondida.PodeRecorrer);
        Assert.Equal(OuvidoriaResolutividade.Resolvida, respondida.Resolutividade);
        Assert.Contains("classificação de risco", respondida.RespostaConclusiva);

        await publico.Publico.RecorrerAsync(criada.Protocolo, criada.CodigoAcesso!, "Discordo da resposta; fiquei 6 horas.");

        var emRecurso = await publico.Publico.AcompanharAsync(criada.Protocolo, criada.CodigoAcesso!);
        Assert.Equal(OuvidoriaStatus.EmRecurso, emRecurso.Status);
        Assert.False(emRecurso.PodeRecorrer);
        Assert.Single(emRecurso.Eventos, e => e.Tipo == OuvidoriaTipoEvento.Recurso);

        var segundo = await Assert.ThrowsAsync<ConflitoException>(() =>
            publico.Publico.RecorrerAsync(criada.Protocolo, criada.CodigoAcesso!, "Segundo recurso."));
        Assert.Equal("ouvidoria.recurso_unico", segundo.Codigo);
    }

    [Fact]
    public async Task Listar_assuntos_publicos_semeia_e_devolve_os_22_do_manual()
    {
        await using var db = fixture.CriarDbContext();
        var amb = Sistema(db);

        var assuntos = await amb.Publico.ListarAssuntosAsync();

        Assert.True(assuntos.Count >= 22);
        Assert.Contains(assuntos, a => a.Nome == "Assistência à Saúde" && a.PaiId == null);
        Assert.Contains(assuntos, a => a.Nome == "SAMU" && a.PaiId == null);
        Assert.True(await db.OuvidoriaConfiguracoes.AnyAsync());
    }
}
