using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Extensao.Distribuicao;
using SMSMais.Core.Institucional;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.Extensao;

/// <summary>Monta os arquivos que se publicam: o .zip da extensão e um "executável".</summary>
internal static class ArquivosDeTeste
{
    public static byte[] Zip(params (string Nome, string Conteudo)[] arquivos)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (nome, conteudo) in arquivos)
            {
                using var saida = zip.CreateEntry(nome).Open();
                saida.Write(Encoding.UTF8.GetBytes(conteudo));
            }
        }
        return memoria.ToArray();
    }

    public static string Manifesto(string versao) => $$"""
        {
          "manifest_version": 3, "version": "{{versao}}",
          "background": { "service_worker": "background.js", "type": "module" },
          "action": { "default_popup": "popup.html" },
          "content_scripts": [ { "matches": ["https://exemplo/*"], "js": ["content.js"] } ]
        }
        """;

    public static byte[] Extensao(string versao) => Zip(
        ("manifest.json", Manifesto(versao)),
        ("background.js", "// sw"),
        ("content.js", "// cs"),
        ("popup.html", "<html>"));

    public static byte[] Executavel(string marca = "") => Encoding.UTF8.GetBytes("MZ executavel de teste " + marca);
}

/// <summary>
/// As duas conferências que não dependem do banco: qual versão é a mais nova e se o pacote da
/// extensão se sustenta. As duas espelham o que o atualizador faz no computador — se os lados
/// discordassem, o painel anunciaria uma versão que nenhum computador instala.
/// </summary>
public class ExtensaoPacoteEVersaoTests
{
    [Theory]
    [InlineData("0.5.10", "0.5.9", true)]
    [InlineData("0.5.27", "0.5.26", true)]
    [InlineData("0.5.9", "0.5.10", false)]
    [InlineData("0.5.26", "0.5.26", false)]
    [InlineData("1.0", "1.0.0", false)]
    [InlineData("1.0.0.1", "1.0.0", true)]
    [InlineData("1", "0.9.9", true)]
    public void Versao_mais_nova_e_numero_a_numero(string a, string b, bool esperado) =>
        Assert.Equal(esperado, VersaoPublicada.MaisNova(a, b));

    [Theory]
    [InlineData("0.5.33", true)]
    [InlineData("1", true)]
    [InlineData("1.2.3.4", true)]
    [InlineData("1.2.3.4.5", false)]
    [InlineData("v1.2", false)]
    [InlineData("1.2-beta", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Formato_de_versao(string? versao, bool esperado) =>
        Assert.Equal(esperado, VersaoPublicada.Valida(versao));

    [Fact]
    public void Pacote_valido_devolve_a_versao_do_manifest()
    {
        Assert.Equal("0.5.33", PacoteDaExtensao.LerVersao(ArquivosDeTeste.Extensao("0.5.33")));
    }

    [Fact]
    public void Pacote_com_pasta_de_cima_e_arquivos_alheios_tambem_vale()
    {
        var zip = ArquivosDeTeste.Zip(
            ("extensao-main/manifest.json", ArquivosDeTeste.Manifesto("1.2.3")),
            ("extensao-main/background.js", ""),
            ("extensao-main/content.js", ""),
            ("extensao-main/popup.html", ""),
            ("extensao-main/README.md", "# leia"),
            ("extensao-main/.gitignore", "x"));
        Assert.Equal("1.2.3", PacoteDaExtensao.LerVersao(zip));
    }

    [Fact]
    public void Recusa_pacote_que_o_computador_recusaria()
    {
        // falta um arquivo que o manifest manda carregar
        var falta = ArquivosDeTeste.Zip(
            ("manifest.json", ArquivosDeTeste.Manifesto("1.0.0")), ("background.js", ""), ("popup.html", ""));
        Assert.Contains("content.js", Mensagem(() => PacoteDaExtensao.LerVersao(falta)));

        // sem manifest, manifest quebrado, versão fora do formato
        Assert.Contains("não tem manifest.json", Mensagem(() => PacoteDaExtensao.LerVersao(ArquivosDeTeste.Zip(("background.js", "")))));
        Assert.Contains("JSON válido", Mensagem(() => PacoteDaExtensao.LerVersao(ArquivosDeTeste.Zip(("manifest.json", "{ \"version\": ")))));
        Assert.Contains("formato", Mensagem(() => PacoteDaExtensao.LerVersao(
            ArquivosDeTeste.Zip(("manifest.json", "{ \"version\": \"1.0-beta\" }")))));

        // o que não é .zip (página de erro salva como arquivo, por exemplo)
        Assert.Contains(".zip válido", Mensagem(() => PacoteDaExtensao.LerVersao(Encoding.UTF8.GetBytes("<html>erro</html>"))));

        // caminho que sai da pasta
        var foge = ArquivosDeTeste.Zip(("manifest.json", ArquivosDeTeste.Manifesto("1.0.0")), ("../fora.js", "x"));
        Assert.Contains("caminho inválido", Mensagem(() => PacoteDaExtensao.LerVersao(foge)));
    }

    private static string Mensagem(Action acao)
    {
        var erro = Assert.Throws<ValidacaoException>(acao);
        return string.Join(" | ", erro.Erros.SelectMany(e => e.Value));
    }
}

/// <summary>
/// Distribuição da extensão pela plataforma (ADR-0064). O que estes testes guardam:
/// o conteúdo só sai para computador AUTORIZADO por alguém logado; o código de ativação vale uma
/// vez; versão recém-publicada só chega aos computadores de teste até ser promovida; e a API de
/// publicação só existe enquanto houver chave ativa.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ExtensaoDistribuicaoServiceTests(PostgresFixture fixture)
{
    private const string Painel = "https://painel.exemplo.gov.br";
    private const string Api = "https://api.exemplo.gov.br";

    // A bancada guarda as linhas entre execuções, e versão publicada tem de ser MAIOR que todas as
    // anteriores. O primeiro número é o relógio (cresce de uma execução para a outra); o segundo,
    // um bloco por teste (cresce dentro da execução).
    private static readonly long Relogio = (long)(DateTime.UtcNow - new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
    private static int _bloco;
    private readonly int _meuBloco = Interlocked.Increment(ref _bloco);

    private string Versao(int n) => $"{Relogio}.{_meuBloco}.{n}";

    private static ExtensaoDistribuicaoService Servico(SmsMaisDbContext db, Guid? usuario = null, string? painel = Painel)
    {
        var instituicao = Substitute.For<IInstituicaoService>();
        instituicao.ObterAsync(Arg.Any<CancellationToken>())
            .Returns(IInstituicaoService.ObterPadrao() with { UrlPainel = painel });
        return new ExtensaoDistribuicaoService(
            db, new UsuarioAtualAccessorFake(usuario), instituicao, new MemoryCache(new MemoryCacheOptions()));
    }

    /// <summary>Um computador já autorizado (pelo fluxo do Configurar), no canal pedido.</summary>
    private static async Task<(DispositivoAutenticado Dispositivo, string Token)> ComputadorAsync(
        SmsMaisDbContext db, ExtensaoCanal canal = ExtensaoCanal.Prod)
    {
        var anonimo = Servico(db);
        var pedido = await anonimo.IniciarAtivacaoAsync(new IniciarAtivacaoRequest($"PC-{Guid.NewGuid():N}"[..20], "0.1.0"));
        await Servico(db, Guid.NewGuid()).AutorizarAtivacaoAsync(pedido.UrlAutorizar.Split('/')[^1]);
        var troca = await anonimo.TrocarCodigoAsync(new TrocarCodigoRequest(pedido.Codigo, null, null));
        var dispositivo = (await anonimo.AutenticarAsync(troca.Token))!;
        if (canal != ExtensaoCanal.Prod)
        {
            await Servico(db, Guid.NewGuid()).DefinirCanalAsync(dispositivo.Id, canal);
            dispositivo = (await anonimo.AutenticarAsync(troca.Token))!;
        }
        return (dispositivo, troca.Token!);
    }

    private static InventarioDoComputador SemInventario => new(null, null, null);

    [Fact]
    public async Task Configurar_computador_pede_alguem_logado_autoriza_e_o_codigo_vale_uma_vez()
    {
        await using var db = fixture.CriarDbContext();
        var computador = Servico(db);
        var quemAutoriza = Guid.NewGuid();

        var pedido = await computador.IniciarAtivacaoAsync(new IniciarAtivacaoRequest("RECEPCAO-01", "0.1.0"));
        Assert.StartsWith($"{Painel}/app/extensao/autorizar/", pedido.UrlAutorizar);
        var codigoPublico = pedido.UrlAutorizar.Split('/')[^1];
        Assert.NotEqual(pedido.Codigo, codigoPublico);

        // Antes de alguém autorizar, o computador só recebe "aguardando".
        Assert.Equal(EstadoDaTroca.Aguardando, (await computador.TrocarCodigoAsync(new TrocarCodigoRequest(pedido.Codigo, null, null))).Estado);

        // A página do painel mostra QUAL computador está pedindo.
        var naPagina = await Servico(db, quemAutoriza).ObterAtivacaoAsync(codigoPublico.ToLowerInvariant());
        Assert.Equal("RECEPCAO-01", naPagina.Computador);
        Assert.Equal(SituacaoAtivacao.Pendente, naPagina.Situacao);

        var autorizada = await Servico(db, quemAutoriza).AutorizarAtivacaoAsync(codigoPublico);
        Assert.Equal(SituacaoAtivacao.Autorizada, autorizada.Situacao);

        var troca = await computador.TrocarCodigoAsync(new TrocarCodigoRequest(pedido.Codigo, "RECEPCAO-01", "0.1.0"));
        Assert.Equal(EstadoDaTroca.Autorizado, troca.Estado);
        Assert.False(string.IsNullOrEmpty(troca.Token));

        // Uso único: o mesmo código não rende um segundo token.
        Assert.Equal(EstadoDaTroca.Encerrada, (await computador.TrocarCodigoAsync(new TrocarCodigoRequest(pedido.Codigo, null, null))).Estado);

        var dispositivo = await computador.AutenticarAsync(troca.Token);
        Assert.NotNull(dispositivo);
        Assert.Equal(ExtensaoCanal.Prod, dispositivo.Canal);

        // No banco fica o hash do token, nunca o token; e quem autorizou fica registrado.
        var linha = await db.ExtensaoDispositivos.AsNoTracking().SingleAsync(d => d.Id == dispositivo.Id);
        Assert.NotEqual(troca.Token, linha.TokenHash);
        Assert.Equal(ExtensaoSegredos.Hash(troca.Token!), linha.TokenHash);
        Assert.Equal(quemAutoriza, linha.AutorizadoPor);
        Assert.Equal("RECEPCAO-01", linha.Computador);
    }

    [Fact]
    public async Task Token_desconhecido_ou_codigo_inventado_nao_passam()
    {
        await using var db = fixture.CriarDbContext();
        var servico = Servico(db);
        Assert.Null(await servico.AutenticarAsync(null));
        Assert.Null(await servico.AutenticarAsync(""));
        Assert.Null(await servico.AutenticarAsync(ExtensaoSegredos.GerarSegredo()));
        Assert.Equal(EstadoDaTroca.Encerrada, (await servico.TrocarCodigoAsync(new TrocarCodigoRequest("nao-existe", null, null))).Estado);
        Assert.Equal(EstadoDaTroca.Encerrada, (await servico.TrocarCodigoAsync(new TrocarCodigoRequest(null, null, null))).Estado);
        await Assert.ThrowsAsync<NaoEncontradoException>(() => Servico(db, Guid.NewGuid()).AutorizarAtivacaoAsync("ZZZZZZZZ"));
    }

    [Fact]
    public async Task Pedido_vencido_nao_e_autorizado_nem_trocado()
    {
        await using var db = fixture.CriarDbContext();
        var pedido = await Servico(db).IniciarAtivacaoAsync(new IniciarAtivacaoRequest("PC-ATRASADO", null));
        var codigoPublico = pedido.UrlAutorizar.Split('/')[^1];
        await db.ExtensaoAtivacoes.Where(a => a.CodigoPublico == codigoPublico)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ExpiraEm, DateTime.UtcNow.AddMinutes(-1)));
        db.ChangeTracker.Clear(); // o ExecuteUpdate não avisa o rastreador: sem isto o pedido "voltaria" válido

        var erro = await Assert.ThrowsAsync<ConflitoException>(() => Servico(db, Guid.NewGuid()).AutorizarAtivacaoAsync(codigoPublico));
        Assert.Equal("extensao.ativacao_vencida", erro.Codigo);
        Assert.Equal(EstadoDaTroca.Encerrada, (await Servico(db).TrocarCodigoAsync(new TrocarCodigoRequest(pedido.Codigo, null, null))).Estado);
    }

    [Fact]
    public async Task Sem_endereco_do_painel_nao_ha_pagina_de_autorizacao()
    {
        await using var db = fixture.CriarDbContext();
        var erro = await Assert.ThrowsAsync<ConflitoException>(
            () => Servico(db, painel: null).IniciarAtivacaoAsync(new IniciarAtivacaoRequest("PC", null)));
        Assert.Equal("extensao.painel_sem_endereco", erro.Codigo);
    }

    [Fact]
    public async Task Instalador_do_painel_ja_chega_autorizado_por_quem_baixou()
    {
        await using var db = fixture.CriarDbContext();
        var quemBaixa = Guid.NewGuid();
        var admin = Servico(db, Guid.NewGuid());
        var executavel = ArquivosDeTeste.Executavel(Versao(1));
        var publicado = await admin.PublicarAsync(ExtensaoArtefato.Atualizador, Versao(1), null, executavel);
        await admin.PromoverAsync(publicado.Id);

        var instalador = await Servico(db, quemBaixa).GerarInstaladorAsync(Api + "/");

        // É o executável publicado + o rabicho com o endereço da API e o código.
        Assert.Equal(Versao(1), instalador.Versao);
        Assert.True(instalador.Conteudo.AsSpan().StartsWith(executavel));
        var rabicho = Encoding.UTF8.GetString(instalador.Conteudo.AsSpan(executavel.Length));
        Assert.StartsWith("\n#SMSMAIS-INSTALADOR:", rabicho);
        var embutido = JsonDocument.Parse(rabicho["\n#SMSMAIS-INSTALADOR:".Length..]).RootElement;
        Assert.Equal(Api, embutido.GetProperty("api").GetString());
        var codigo = embutido.GetProperty("codigo").GetString()!;

        // O código troca direto pelo token (ninguém precisa clicar em autorizar de novo)…
        var computador = Servico(db);
        var troca = await computador.TrocarCodigoAsync(new TrocarCodigoRequest(codigo, "PC-NOVO", Versao(1)));
        Assert.Equal(EstadoDaTroca.Autorizado, troca.Estado);
        var dispositivo = (await computador.AutenticarAsync(troca.Token))!;
        var linha = await db.ExtensaoDispositivos.AsNoTracking().SingleAsync(d => d.Id == dispositivo.Id);
        Assert.Equal(quemBaixa, linha.AutorizadoPor);
        Assert.Equal("PC-NOVO", linha.Computador);

        // …e só uma vez: o mesmo instalador copiado para outro PC não autoriza o segundo.
        Assert.Equal(EstadoDaTroca.Encerrada, (await computador.TrocarCodigoAsync(new TrocarCodigoRequest(codigo, "OUTRO-PC", null))).Estado);

        // Cada download leva um código próprio.
        var outro = await Servico(db, quemBaixa).GerarInstaladorAsync(Api);
        Assert.False(outro.Conteudo.AsSpan().SequenceEqual(instalador.Conteudo));

        // Sem uma pessoa por trás (chave de serviço), não há instalador.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Servico(db).GerarInstaladorAsync(Api));
    }

    [Fact]
    public async Task Versao_publicada_so_chega_ao_canal_de_teste_ate_ser_promovida()
    {
        await using var db = fixture.CriarDbContext();
        var admin = Servico(db, Guid.NewGuid());
        var (deProd, _) = await ComputadorAsync(db);
        var (deTeste, _) = await ComputadorAsync(db, ExtensaoCanal.Teste);
        var servico = Servico(db);

        var v1 = await admin.PublicarAsync(ExtensaoArtefato.Extensao, null, "primeira", ArquivosDeTeste.Extensao(Versao(1)));
        Assert.Equal(Versao(1), v1.Versao); // lida do manifest
        await admin.PromoverAsync(v1.Id);
        var v2 = await admin.PublicarAsync(ExtensaoArtefato.Extensao, null, "em conferência", ArquivosDeTeste.Extensao(Versao(2)));

        // Publicada e ainda não promovida: só o computador de teste enxerga.
        Assert.Equal(Versao(1), (await servico.ConsultarExtensaoAsync(deProd, SemInventario))!.Version);
        var paraTeste = (await servico.ConsultarExtensaoAsync(deTeste, SemInventario))!;
        Assert.Equal(Versao(2), paraTeste.Version);
        Assert.Equal("teste", paraTeste.Canal);
        Assert.Equal($"extensao-{Versao(1)}.zip", (await servico.BaixarAsync(deProd, ExtensaoArtefato.Extensao))!.NomeArquivo);

        var lista = await admin.ListarPacotesAsync();
        Assert.True(lista.Single(p => p.Id == v2.Id).AtualEmTeste);
        Assert.False(lista.Single(p => p.Id == v2.Id).AtualEmProd);
        Assert.True(lista.Single(p => p.Id == v1.Id).AtualEmProd);

        // Promovida: todos recebem. O serviço novo tem cache próprio — o de quem promoveu foi derrubado.
        var promovida = await admin.PromoverAsync(v2.Id);
        Assert.NotNull(promovida.PromovidoEm);
        Assert.Equal(Versao(2), (await admin.ConsultarExtensaoAsync(deProd, SemInventario))!.Version);

        // Retirada: deixa de ser entregue; a produção volta a anunciar a anterior.
        await admin.RetirarAsync(v2.Id);
        Assert.Equal(Versao(1), (await admin.ConsultarExtensaoAsync(deProd, SemInventario))!.Version);
        Assert.Equal(Versao(1), (await admin.ConsultarExtensaoAsync(deTeste, SemInventario))!.Version);
        var erro = await Assert.ThrowsAsync<ConflitoException>(() => admin.PromoverAsync(v2.Id));
        Assert.Equal("extensao.versao_retirada", erro.Codigo);
    }

    [Fact]
    public async Task Atualizador_segue_os_mesmos_canais_e_anuncia_o_sha256()
    {
        await using var db = fixture.CriarDbContext();
        var admin = Servico(db, Guid.NewGuid());
        var (deProd, _) = await ComputadorAsync(db);
        var (deTeste, _) = await ComputadorAsync(db, ExtensaoCanal.Teste);

        var exe1 = ArquivosDeTeste.Executavel(Versao(1));
        var v1 = await admin.PublicarAsync(ExtensaoArtefato.Atualizador, Versao(1), null, exe1);
        await admin.PromoverAsync(v1.Id);
        var exe2 = ArquivosDeTeste.Executavel(Versao(2));
        await admin.PublicarAsync(ExtensaoArtefato.Atualizador, Versao(2), null, exe2);

        var paraProd = (await admin.ConsultarAtualizadorAsync(deProd))!;
        Assert.Equal(Versao(1), paraProd.Versao);
        Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(exe1)), paraProd.Sha256);
        Assert.Equal(exe1, (await admin.BaixarAsync(deProd, ExtensaoArtefato.Atualizador))!.Conteudo);

        Assert.Equal(Versao(2), (await admin.ConsultarAtualizadorAsync(deTeste))!.Versao);
        Assert.Equal(exe2, (await admin.BaixarAsync(deTeste, ExtensaoArtefato.Atualizador))!.Conteudo);
    }

    [Fact]
    public async Task Publicar_recusa_o_que_nenhum_computador_instalaria()
    {
        await using var db = fixture.CriarDbContext();
        var admin = Servico(db, Guid.NewGuid());
        await admin.PublicarAsync(ExtensaoArtefato.Extensao, null, null, ArquivosDeTeste.Extensao(Versao(5)));

        // mesma versão de novo; versão menor (computador não rebaixa)
        var repetida = await Assert.ThrowsAsync<ConflitoException>(
            () => admin.PublicarAsync(ExtensaoArtefato.Extensao, null, null, ArquivosDeTeste.Extensao(Versao(5))));
        Assert.Equal("extensao.versao_repetida", repetida.Codigo);
        await Assert.ThrowsAsync<ValidacaoException>(
            () => admin.PublicarAsync(ExtensaoArtefato.Extensao, null, null, ArquivosDeTeste.Extensao(Versao(4))));

        // versão informada diferente da do manifest
        await Assert.ThrowsAsync<ValidacaoException>(
            () => admin.PublicarAsync(ExtensaoArtefato.Extensao, "9.9.9", null, ArquivosDeTeste.Extensao(Versao(6))));

        // atualizador: sem versão, sem ser executável, e o instalador baixado do painel (com rabicho)
        await Assert.ThrowsAsync<ValidacaoException>(
            () => admin.PublicarAsync(ExtensaoArtefato.Atualizador, null, null, ArquivosDeTeste.Executavel()));
        await Assert.ThrowsAsync<ValidacaoException>(
            () => admin.PublicarAsync(ExtensaoArtefato.Atualizador, Versao(7), null, Encoding.UTF8.GetBytes("nao sou exe")));
        byte[] comRabicho = [.. ArquivosDeTeste.Executavel(), .. Encoding.UTF8.GetBytes("\n#SMSMAIS-INSTALADOR:{\"api\":\"x\",\"codigo\":\"y\"}")];
        await Assert.ThrowsAsync<ValidacaoException>(
            () => admin.PublicarAsync(ExtensaoArtefato.Atualizador, Versao(7), null, comRabicho));
        await Assert.ThrowsAsync<ValidacaoException>(
            () => admin.PublicarAsync(ExtensaoArtefato.Extensao, null, null, []));
    }

    [Fact]
    public async Task Cada_consulta_do_computador_atualiza_o_inventario()
    {
        await using var db = fixture.CriarDbContext();
        var (dispositivo, _) = await ComputadorAsync(db);
        var servico = Servico(db);

        await servico.ConsultarExtensaoAsync(dispositivo, new InventarioDoComputador("0.5.33", "0.1.2", "modo-dev-desligado"));

        var linha = await db.ExtensaoDispositivos.AsNoTracking().SingleAsync(d => d.Id == dispositivo.Id);
        Assert.Equal("0.5.33", linha.VersaoExtensao);
        Assert.Equal("0.1.2", linha.VersaoAtualizador);
        Assert.Equal("modo-dev-desligado", linha.SituacaoChrome);
        Assert.NotNull(linha.UltimoContatoEm);

        var naTela = (await Servico(db, Guid.NewGuid()).ListarDispositivosAsync()).Single(d => d.Id == dispositivo.Id);
        Assert.Equal("modo-dev-desligado", naTela.SituacaoChrome);

        // O que vem de fora é cortado no tamanho da coluna (não derruba a consulta).
        await servico.ConsultarExtensaoAsync(dispositivo, new InventarioDoComputador(new string('9', 500), null, new string('x', 500)));
        linha = await db.ExtensaoDispositivos.AsNoTracking().SingleAsync(d => d.Id == dispositivo.Id);
        Assert.Equal(32, linha.VersaoExtensao!.Length);
        Assert.Equal(30, linha.SituacaoChrome!.Length);
        Assert.Equal("0.1.2", linha.VersaoAtualizador); // sem informar, fica a última conhecida
    }

    [Fact]
    public async Task Computador_revogado_deixa_de_ser_reconhecido()
    {
        await using var db = fixture.CriarDbContext();
        var (dispositivo, token) = await ComputadorAsync(db);
        var admin = Servico(db, Guid.NewGuid());

        var revogado = await admin.RevogarAsync(dispositivo.Id);
        Assert.NotNull(revogado.RevogadoEm);
        Assert.Null(await Servico(db).AutenticarAsync(token));

        // revogar de novo não é erro; computador que não existe, sim
        Assert.Equal(revogado.RevogadoEm, (await admin.RevogarAsync(dispositivo.Id)).RevogadoEm);
        await Assert.ThrowsAsync<NaoEncontradoException>(() => admin.RevogarAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Api_de_publicacao_so_existe_enquanto_ha_chave_ativa()
    {
        await using var db = fixture.CriarDbContext();
        var admin = Servico(db, Guid.NewGuid());
        var anonimo = Servico(db);

        // Parte de "desligada" (outro teste ou execução pode ter deixado uma chave).
        await admin.RevogarChavePublicacaoAsync();
        Assert.False((await admin.ObterChavePublicacaoAsync()).Ativa);
        Assert.False(await anonimo.ChavePublicacaoValeAsync("pub_qualquer"));
        Assert.False(await anonimo.ChavePublicacaoValeAsync(null));

        var gerada = await admin.GerarChavePublicacaoAsync();
        Assert.StartsWith("pub_", gerada.Chave);
        Assert.True(gerada.Situacao.Ativa);
        Assert.StartsWith(gerada.Situacao.Prefixo!, gerada.Chave);
        Assert.True(gerada.Situacao.Prefixo!.Length < 12); // na tela só o começo; a chave inteira nunca volta
        Assert.True(await anonimo.ChavePublicacaoValeAsync(gerada.Chave));
        Assert.NotNull((await admin.ObterChavePublicacaoAsync()).UltimoUsoEm);
        Assert.False(await db.ExtensaoChavesPublicacao.AnyAsync(c => c.ChaveHash == gerada.Chave)); // só o hash

        // O que entra pela API fica marcado como tal.
        var pelaApi = await anonimo.PublicarAsync(
            ExtensaoArtefato.Extensao, null, null, ArquivosDeTeste.Extensao(Versao(1)), pelaApi: true);
        Assert.True(pelaApi.PublicadoPelaApi);
        Assert.Null(pelaApi.PublicadoPorNome);
        Assert.True((await anonimo.PromoverAsync(pelaApi.Id, pelaApi: true)).PromovidoPelaApi);

        // Gerar outra revoga a anterior; revogar desliga a API.
        var outra = await admin.GerarChavePublicacaoAsync();
        Assert.False(await anonimo.ChavePublicacaoValeAsync(gerada.Chave));
        Assert.True(await anonimo.ChavePublicacaoValeAsync(outra.Chave));
        Assert.False((await admin.RevogarChavePublicacaoAsync()).Ativa);
        Assert.False(await anonimo.ChavePublicacaoValeAsync(outra.Chave));

        // Sem uma pessoa logada não se gera chave.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => anonimo.GerarChavePublicacaoAsync());
    }
}
