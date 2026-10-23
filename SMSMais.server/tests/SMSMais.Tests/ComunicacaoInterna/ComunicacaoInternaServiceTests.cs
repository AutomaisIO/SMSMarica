using System.Collections.Concurrent;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SMSMais.Core.Armazenamento;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.ComunicacaoInterna;
using SMSMais.Core.ComunicacaoInterna.Dtos;
using SMSMais.Core.Integracoes.ElevenLabs;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Tests.Infraestrutura;

namespace SMSMais.Tests.ComunicacaoInterna;

/// <summary>
/// Regras da Comunicação interna (ADR-0079): sessão única por par, participação derivada dos
/// vínculos (e só dela), não-lidas sem herdar histórico, busca e anexo recortados por quem participa.
/// </summary>
[Collection(nameof(PostgresCollection))]
public class ComunicacaoInternaServiceTests(PostgresFixture fixture)
{
    [Fact]
    public async Task Conversa_de_unidade_e_a_mesma_nos_dois_sentidos()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var b = await CriarUnidadeAsync(db);
        var ana = await CriarUsuarioAsync(db, a);
        var bia = await CriarUsuarioAsync(db, b);

        var daAna = await Servico(db, ana).AbrirConversaUnidadeAsync(new AbrirConversaUnidadeRequest(b, a));
        var daBia = await Servico(db, bia).AbrirConversaUnidadeAsync(new AbrirConversaUnidadeRequest(a, null));

        Assert.Equal(daAna.Id, daBia.Id);
        Assert.Equal(TipoConversaInterna.Unidade, daAna.Tipo);
    }

    [Fact]
    public async Task Conversa_de_unidade_e_das_duas_equipes_e_de_mais_ninguem()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var b = await CriarUnidadeAsync(db);
        var c = await CriarUnidadeAsync(db);
        var ana = await CriarUsuarioAsync(db, a);
        var bia = await CriarUsuarioAsync(db, b);
        var colegaDeB = await CriarUsuarioAsync(db, b);
        var carla = await CriarUsuarioAsync(db, c);

        var conversa = await Servico(db, ana).AbrirConversaUnidadeAsync(new AbrirConversaUnidadeRequest(b, a));
        await Servico(db, ana).EnviarAsync(conversa.Id, "Tem vaga de leito?", null);

        foreach (var membro in new[] { bia, colegaDeB })
        {
            var lista = await Servico(db, membro).ListarConversasAsync();
            var item = Assert.Single(lista, i => i.Id == conversa.Id);
            Assert.Equal(1, item.NaoLidas);
        }

        var deFora = Servico(db, carla);
        Assert.DoesNotContain(await deFora.ListarConversasAsync(), i => i.Id == conversa.Id);
        await Assert.ThrowsAsync<NaoEncontradoException>(() => deFora.ListarMensagensAsync(conversa.Id, null, 50));
        await Assert.ThrowsAsync<NaoEncontradoException>(() => deFora.EnviarAsync(conversa.Id, "oi", null));
    }

    [Fact]
    public async Task Direta_e_unica_por_par_e_so_com_quem_tem_o_modulo()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var ana = await CriarUsuarioAsync(db, a);
        var bia = await CriarUsuarioAsync(db, a);
        var semModulo = await CriarUsuarioAsync(db, a, comModulo: false);

        var ida = await Servico(db, ana).AbrirConversaDiretaAsync(new AbrirConversaDiretaRequest(bia));
        var volta = await Servico(db, bia).AbrirConversaDiretaAsync(new AbrirConversaDiretaRequest(ana));

        Assert.Equal(ida.Id, volta.Id);
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            Servico(db, ana).AbrirConversaDiretaAsync(new AbrirConversaDiretaRequest(semModulo)));
        await Assert.ThrowsAsync<ValidacaoException>(() =>
            Servico(db, ana).AbrirConversaDiretaAsync(new AbrirConversaDiretaRequest(ana)));
        Assert.DoesNotContain(
            await Servico(db, ana).ListarUsuariosDaUnidadeAsync(a), u => u.Id == semModulo);
    }

    [Fact]
    public async Task Marcar_lida_zera_e_conversa_silenciada_nao_conta_no_resumo()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var ana = await CriarUsuarioAsync(db, a);
        var bia = await CriarUsuarioAsync(db, a);
        var conversa = await Servico(db, ana).AbrirConversaDiretaAsync(new AbrirConversaDiretaRequest(bia));
        await Servico(db, ana).EnviarAsync(conversa.Id, "um", null);
        await Servico(db, ana).EnviarAsync(conversa.Id, "dois", null);

        var daBia = Servico(db, bia);
        Assert.Equal(2, (await daBia.ObterResumoAsync()).NaoLidas);

        await daBia.SilenciarAsync(conversa.Id, true);
        Assert.Equal(0, (await daBia.ObterResumoAsync()).NaoLidas);
        Assert.Equal(2, (await daBia.ListarConversasAsync()).Single(i => i.Id == conversa.Id).NaoLidas);

        await daBia.MarcarLidaAsync(conversa.Id);
        var item = (await daBia.ListarConversasAsync()).Single(i => i.Id == conversa.Id);
        Assert.Equal(0, item.NaoLidas);
        Assert.True(item.Silenciada);

        // A Ana vê até onde a Bia leu (✓✓).
        var detalhe = await Servico(db, ana).ObterConversaAsync(conversa.Id);
        Assert.NotNull(detalhe.OutroLidaAte);
    }

    [Fact]
    public async Task Quem_entra_na_equipe_ve_o_historico_mas_nao_como_nao_lido()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var b = await CriarUnidadeAsync(db);
        var ana = await CriarUsuarioAsync(db, a);
        await CriarUsuarioAsync(db, b);
        var conversa = await Servico(db, ana).AbrirConversaUnidadeAsync(new AbrirConversaUnidadeRequest(b, a));
        await Servico(db, ana).EnviarAsync(conversa.Id, "antes de você chegar", null);

        var novato = await CriarUsuarioAsync(db, b);

        var servico = Servico(db, novato);
        Assert.Single((await servico.ListarMensagensAsync(conversa.Id, null, 50)).Mensagens);
        Assert.Equal(0, (await servico.ObterResumoAsync()).NaoLidas);
    }

    [Fact]
    public async Task Busca_ignora_acento_e_so_acha_onde_participo()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var ana = await CriarUsuarioAsync(db, a);
        var bia = await CriarUsuarioAsync(db, a);
        var carla = await CriarUsuarioAsync(db, await CriarUnidadeAsync(db));
        var marcador = $"Remoção {Guid.NewGuid():N}"[..20];
        var conversa = await Servico(db, ana).AbrirConversaDiretaAsync(new AbrirConversaDiretaRequest(bia));
        await Servico(db, ana).EnviarAsync(conversa.Id, $"Paciente aguardando {marcador} para o hospital", null);

        var termo = marcador.Replace("Remoção", "remocao");
        var achados = await Servico(db, bia).BuscarAsync(termo);
        var achado = Assert.Single(achados);
        Assert.Equal(conversa.Id, achado.ConversaId);
        Assert.Contains("Remoção", achado.Trecho);

        Assert.Empty(await Servico(db, carla).BuscarAsync(termo));
    }

    [Fact]
    public async Task Anexo_so_para_quem_participa_e_html_sai_como_download()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var ana = await CriarUsuarioAsync(db, a);
        var bia = await CriarUsuarioAsync(db, a);
        var carla = await CriarUsuarioAsync(db, await CriarUnidadeAsync(db));
        var armazenamento = new ArmazenamentoFake();
        var conversa = await Servico(db, ana, armazenamento).AbrirConversaDiretaAsync(new AbrirConversaDiretaRequest(bia));

        var html = Encoding.UTF8.GetBytes("<script>alert(1)</script>");
        var enviada = await Servico(db, ana, armazenamento).EnviarAsync(
            conversa.Id, null, new AnexoEnviadoInterno("../../pagina.html", "text/html", html, null, false));
        var audio = await Servico(db, ana, armazenamento).EnviarAsync(
            conversa.Id, null, new AnexoEnviadoInterno("audio.webm", "audio/webm;codecs=opus", [1, 2, 3], 7, true));

        Assert.Equal("pagina.html", enviada.Anexo!.Nome);
        Assert.Equal(7, audio.Anexo!.DuracaoSegundos);
        Assert.True(audio.Anexo.EhAudio);

        var baixado = await Servico(db, bia, armazenamento).ObterAnexoAsync(enviada.Id);
        Assert.Equal("application/octet-stream", baixado.Mime);
        Assert.Equal(html, baixado.Conteudo);
        Assert.Equal("audio/webm", (await Servico(db, bia, armazenamento).ObterAnexoAsync(audio.Id)).Mime);

        await Assert.ThrowsAsync<NaoEncontradoException>(() => Servico(db, carla, armazenamento).ObterAnexoAsync(enviada.Id));
    }

    [Fact]
    public async Task Pagina_vem_em_ordem_cronologica_e_antesDe_traz_as_anteriores()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var ana = await CriarUsuarioAsync(db, a);
        var bia = await CriarUsuarioAsync(db, a);
        var servico = Servico(db, ana);
        var conversa = await servico.AbrirConversaDiretaAsync(new AbrirConversaDiretaRequest(bia));
        for (var i = 1; i <= 5; i++) await servico.EnviarAsync(conversa.Id, $"m{i}", null);

        var ultima = await servico.ListarMensagensAsync(conversa.Id, null, 2);
        Assert.True(ultima.TemMais);
        Assert.Equal(new[] { "m4", "m5" }, ultima.Mensagens.Select(m => m.Texto!));

        var anterior = await servico.ListarMensagensAsync(conversa.Id, ultima.Mensagens[0].EnviadaEm, 10);
        Assert.False(anterior.TemMais);
        Assert.Equal(new[] { "m1", "m2", "m3" }, anterior.Mensagens.Select(m => m.Texto!));
    }

    [Fact]
    public async Task Pessoa_so_aparece_e_so_participa_pela_unidade_padrao()
    {
        // O técnico de regulação: padrão no Complexo, vinculado às unidades para enxergar os pedidos.
        await using var db = fixture.CriarDbContext();
        var complexo = await CriarUnidadeAsync(db);
        var ubs = await CriarUnidadeAsync(db);
        var hospital = await CriarUnidadeAsync(db);
        var tecnico = await CriarUsuarioAsync(db, complexo, outrasUnidades: [ubs, hospital]);
        var daUbs = await CriarUsuarioAsync(db, ubs);
        await CriarUsuarioAsync(db, hospital);

        var servico = Servico(db, daUbs);
        Assert.DoesNotContain(await servico.ListarUsuariosDaUnidadeAsync(ubs), p => p.Id == tecnico);
        Assert.Contains(await servico.ListarUsuariosDaUnidadeAsync(complexo), p => p.Id == tecnico);
        var unidades = await servico.ListarUnidadesAsync();
        Assert.Equal(1, unidades.Single(u => u.Id == ubs).Usuarios);
        Assert.Equal(1, unidades.Single(u => u.Id == complexo).Usuarios);

        // UBS ↔ hospital é das duas equipes; o vínculo comum do técnico não o põe na conversa.
        var conversa = await servico.AbrirConversaUnidadeAsync(new AbrirConversaUnidadeRequest(hospital, null));
        await servico.EnviarAsync(conversa.Id, "Chegou o paciente?", null);
        var doTecnico = Servico(db, tecnico);
        Assert.DoesNotContain(await doTecnico.ListarConversasAsync(), i => i.Id == conversa.Id);
        Assert.Equal(0, (await doTecnico.ObterResumoAsync()).NaoLidas);

        // E o que ele abre sai do Complexo.
        var contexto = await doTecnico.ObterContextoAsync();
        Assert.Equal(complexo, Assert.Single(contexto.MinhasUnidades).UnidadeId);
        Assert.Equal(complexo, contexto.UnidadeAtualId);
        var dele = await doTecnico.AbrirConversaUnidadeAsync(new AbrirConversaUnidadeRequest(ubs, null));
        Assert.Contains(complexo, new[] { dele.UnidadeAId, dele.UnidadeBId });
        var enviada = await doTecnico.EnviarAsync(dele.Id, "Pedido devolvido para ajuste", null);
        Assert.Equal(1, (await servico.ListarConversasAsync()).Single(i => i.Id == dele.Id).NaoLidas);
        Assert.NotNull(enviada.AutorUnidadeNome);
    }

    [Fact]
    public async Task WebM_sem_o_primeiro_byte_e_consertado_na_entrada_e_na_leitura()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var ana = await CriarUsuarioAsync(db, a);
        var bia = await CriarUsuarioAsync(db, a);
        var armazenamento = new ArmazenamentoFake();
        var conversa = await Servico(db, ana, armazenamento).AbrirConversaDiretaAsync(new AbrirConversaDiretaRequest(bia));

        // O que se viu em produção (23/10/2026): o EBML começando em 45 DF A3, sem o 0x1A.
        byte[] semPrimeiro = [0x45, 0xDF, 0xA3, 0x9F, 0x42, 0x86, 0x81, 0x01];
        var enviada = await Servico(db, ana, armazenamento).EnviarAsync(
            conversa.Id, null, new AnexoEnviadoInterno("audio.webm", "audio/webm", semPrimeiro, 2, true, semPrimeiro.Length + 1));

        var baixado = await Servico(db, bia, armazenamento).ObterAnexoAsync(enviada.Id);
        Assert.Equal(new byte[] { 0x1A, 0x45, 0xDF, 0xA3 }, baixado.Conteudo[..4]);
        Assert.Equal(semPrimeiro.Length + 1, baixado.Conteudo.Length);

        // Arquivo íntegro não é mexido; e o conserto só vale para WebM/áudio.
        byte[] inteiro = [0x1A, 0x45, 0xDF, 0xA3, 0x9F];
        Assert.Same(inteiro, ComunicacaoInternaService.RepararWebmSemPrimeiroByte(inteiro, "audio/webm", true, out var r1));
        Assert.False(r1);
        Assert.Same(semPrimeiro, ComunicacaoInternaService.RepararWebmSemPrimeiroByte(semPrimeiro, "application/pdf", false, out var r2));
        Assert.False(r2);
    }

    [Fact]
    public async Task Transcricao_fica_guardada_vale_para_todos_e_entra_na_busca()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var ana = await CriarUsuarioAsync(db, a);
        var bia = await CriarUsuarioAsync(db, a);
        var carla = await CriarUsuarioAsync(db, await CriarUnidadeAsync(db));
        var armazenamento = new ArmazenamentoFake();
        var marcador = $"leito {Guid.NewGuid():N}"[..18];
        var stt = new TranscricaoFake($"Confirma o {marcador} para amanhã");
        var conversa = await Servico(db, ana, armazenamento).AbrirConversaDiretaAsync(new AbrirConversaDiretaRequest(bia));
        var audio = await Servico(db, ana, armazenamento).EnviarAsync(
            conversa.Id, null, new AnexoEnviadoInterno("audio.webm", "audio/webm", [0x45, 0xDF, 0xA3, 0x9F], 3, true));

        var primeira = await Servico(db, bia, armazenamento, stt).TranscreverAsync(audio.Id);
        var segunda = await Servico(db, ana, armazenamento, stt).TranscreverAsync(audio.Id);

        Assert.Contains(marcador, primeira.Anexo!.Transcricao);
        Assert.Equal(primeira.Anexo.Transcricao, segunda.Anexo!.Transcricao);
        Assert.Equal(1, stt.Chamadas); // a segunda não pagou de novo
        Assert.Equal(0x1A, stt.UltimoAudio![0]); // a STT recebeu o arquivo consertado

        var achado = Assert.Single(await Servico(db, ana, armazenamento).BuscarAsync(marcador));
        Assert.Equal(audio.Id, achado.MensagemId);

        await Assert.ThrowsAsync<NaoEncontradoException>(() => Servico(db, carla, armazenamento, stt).TranscreverAsync(audio.Id));
    }

    [Fact]
    public async Task Transcricao_so_de_audio_e_falha_vira_mensagem_clara()
    {
        await using var db = fixture.CriarDbContext();
        var a = await CriarUnidadeAsync(db);
        var ana = await CriarUsuarioAsync(db, a);
        var bia = await CriarUsuarioAsync(db, a);
        var armazenamento = new ArmazenamentoFake();
        var conversa = await Servico(db, ana, armazenamento).AbrirConversaDiretaAsync(new AbrirConversaDiretaRequest(bia));
        var texto = await Servico(db, ana, armazenamento).EnviarAsync(conversa.Id, "só texto", null);
        var audio = await Servico(db, ana, armazenamento).EnviarAsync(
            conversa.Id, null, new AnexoEnviadoInterno("audio.webm", "audio/webm", [0x1A, 0x45, 0xDF, 0xA3], 1, true));

        await Assert.ThrowsAsync<ValidacaoException>(() => Servico(db, bia, armazenamento).TranscreverAsync(texto.Id));
        var falha = await Assert.ThrowsAsync<ValidacaoException>(() =>
            Servico(db, bia, armazenamento, new TranscricaoFake(null, "HTTP 500")).TranscreverAsync(audio.Id));
        Assert.Contains("Tente de novo", string.Join(" ", falha.Erros.SelectMany(e => e.Value)));
    }

    [Fact]
    public async Task Sem_vinculo_nao_abre_conversa_de_unidade()
    {
        await using var db = fixture.CriarDbContext();
        var destino = await CriarUnidadeAsync(db);
        var solto = await CriarUsuarioAsync(db, unidadeId: null);

        await Assert.ThrowsAsync<ValidacaoException>(() =>
            Servico(db, solto).AbrirConversaUnidadeAsync(new AbrirConversaUnidadeRequest(destino, null)));
    }

    // ------------------------------------------------------------------ apoio

    private static ComunicacaoInternaService Servico(
        SmsMaisDbContext db, Guid usuarioId, IArmazenamentoArquivos? armazenamento = null,
        IElevenLabsTranscricaoService? transcricao = null) =>
        new(
            db,
            new UsuarioAtualAccessorFake(usuarioId),
            new PresencaInterna(),
            armazenamento ?? new ArmazenamentoFake(),
            new NotificadorComunicacaoInternaNulo(),
            transcricao ?? new TranscricaoFake("texto"),
            NullLogger<ComunicacaoInternaService>.Instance);

    /// <summary>STT de mentira: conta as chamadas e guarda o que recebeu.</summary>
    private sealed class TranscricaoFake(string? texto, string? erro = null) : IElevenLabsTranscricaoService
    {
        public int Chamadas { get; private set; }
        public byte[]? UltimoAudio { get; private set; }

        public Task<TranscricaoResultado> TranscreverAsync(
            byte[] audio, string? mimeType, CancellationToken ct = default, string? nomeArquivo = null)
        {
            Chamadas++;
            UltimoAudio = audio;
            return Task.FromResult(new TranscricaoResultado(texto, erro));
        }
    }

    private static async Task<Guid> CriarUnidadeAsync(SmsMaisDbContext db)
    {
        var unidade = new Unidade
        {
            Id = Guid.NewGuid(),
            Nome = $"UNID {Guid.NewGuid():N}"[..24],
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Unidades.Add(unidade);
        await db.SaveChangesAsync();
        return unidade.Id;
    }

    /// <summary>
    /// Usuário ativo com <paramref name="unidadeId"/> como unidade PADRÃO, vínculos comuns em
    /// <paramref name="outrasUnidades"/> e — por padrão — num perfil com o módulo 86.
    /// </summary>
    private static async Task<Guid> CriarUsuarioAsync(
        SmsMaisDbContext db, Guid? unidadeId, bool comModulo = true, params Guid[] outrasUnidades)
    {
        var u = new Usuario
        {
            Id = Guid.NewGuid(),
            NomeCompleto = $"PESSOA {Guid.NewGuid():N}"[..20],
            Email = $"{Guid.NewGuid():N}@teste.local",
            SenhaHash = "x",
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
        };
        db.Usuarios.Add(u);
        if (unidadeId is { } unidade)
            db.UsuarioUnidades.Add(new UsuarioUnidade { UsuarioId = u.Id, UnidadeId = unidade, Principal = true, CriadoEm = DateTime.UtcNow });
        foreach (var outra in outrasUnidades)
            db.UsuarioUnidades.Add(new UsuarioUnidade { UsuarioId = u.Id, UnidadeId = outra, CriadoEm = DateTime.UtcNow });
        if (comModulo)
        {
            var perfil = new Perfil { Id = Guid.NewGuid(), Nome = $"PERFIL {Guid.NewGuid():N}", Ativo = true, CriadoEm = DateTime.UtcNow };
            perfil.Permissoes.Add(new PermissaoPerfil
            {
                PerfilId = perfil.Id,
                Modulo = ModuloPermissao.ComunicacaoInterna,
                Acoes = AcoesPermissao.Consulta | AcoesPermissao.Inclusao,
            });
            db.Add(perfil);
            db.UsuariosPerfis.Add(new UsuarioPerfil { UsuarioId = u.Id, PerfilId = perfil.Id });
        }
        await db.SaveChangesAsync();
        return u.Id;
    }

    private sealed class ArmazenamentoFake : IArmazenamentoArquivos
    {
        private readonly ConcurrentDictionary<string, byte[]> _arquivos = new();

        public string MontarChaveDocumento(Guid pacienteId, Guid documentoId, string extensao = "pdf") =>
            $"teste/{pacienteId:D}/{documentoId:D}.{extensao}";

        public string MontarChaveComunicacaoInterna(Guid conversaId, Guid mensagemId, string extensao) =>
            $"teste/comunicacao-interna/{conversaId:D}/{mensagemId:D}.{extensao}";

        public Task SalvarAsync(string chave, byte[] conteudo, CancellationToken cancellationToken = default)
        {
            _arquivos[chave] = conteudo;
            return Task.CompletedTask;
        }

        public Task<byte[]?> LerAsync(string chave, CancellationToken cancellationToken = default) =>
            Task.FromResult(_arquivos.TryGetValue(chave, out var c) ? c : null);

        public Task ExcluirAsync(string chave, CancellationToken cancellationToken = default)
        {
            _arquivos.TryRemove(chave, out _);
            return Task.CompletedTask;
        }
    }
}
