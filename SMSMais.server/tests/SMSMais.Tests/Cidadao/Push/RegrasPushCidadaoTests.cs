using SMSMais.Core.Cidadao.Push;
using SMSMais.Core.Cidadao.Push.Dtos;
using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Tests.Cidadao.Push;

/// <summary>
/// Regras do push sem banco nem rede: o que a equipe pode escrever (título, mensagem, tela do app),
/// o que o app pode registrar, a conferência do JSON da conta de serviço ao gravar em Integrações e
/// o resumo da falha que vai para o histórico.
/// </summary>
public sealed class RegrasPushCidadaoTests
{
    // ---------- envio ----------

    [Fact]
    public void Envio_valido_volta_aparado_e_rota_vazia_vira_inicio()
    {
        var (titulo, mensagem, rota) = PushCidadaoService.ValidarEnvio(
            new EnviarNotificacaoAppRequest("  Aviso da Secretaria  ", "  Seu cadastro foi atualizado.  ", "  "));

        Assert.Equal("Aviso da Secretaria", titulo);
        Assert.Equal("Seu cadastro foi atualizado.", mensagem);
        Assert.Null(rota);
    }

    [Fact]
    public void Limites_contam_depois_de_aparar()
    {
        var titulo = new string('t', 65);
        var mensagem = new string('m', 240);

        var r = PushCidadaoService.ValidarEnvio(new EnviarNotificacaoAppRequest($" {titulo} ", $" {mensagem} ", "/exames"));

        Assert.Equal(titulo, r.Titulo);
        Assert.Equal(mensagem, r.Mensagem);
        Assert.Equal("/exames", r.Rota);
    }

    [Fact]
    public void Titulo_e_mensagem_vazios_ou_longos_e_rota_fora_da_lista_sao_recusados_por_campo()
    {
        var vazios = Assert.Throws<ValidacaoException>(() =>
            PushCidadaoService.ValidarEnvio(new EnviarNotificacaoAppRequest(" ", null, "/admin")));
        Assert.Equal(new[] { "mensagem", "rota", "titulo" }, vazios.Erros.Keys.Order());

        var longos = Assert.Throws<ValidacaoException>(() =>
            PushCidadaoService.ValidarEnvio(new EnviarNotificacaoAppRequest(
                new string('t', 66), new string('m', 241), null)));
        Assert.Equal(new[] { "mensagem", "titulo" }, longos.Erros.Keys.Order());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/agendados/consultas")]
    [InlineData("/agendados/exames")]
    [InlineData("/atendimentos")]
    [InlineData("/exames")]
    [InlineData("/documentos")]
    [InlineData("/transporte")]
    [InlineData("/chat")]
    [InlineData("/perfil")]
    public void Toda_rota_da_lista_fixa_passa(string rota)
    {
        var r = PushCidadaoService.ValidarEnvio(new EnviarNotificacaoAppRequest("Título", "Mensagem", rota));
        Assert.Equal(rota, r.Rota);
    }

    [Theory]
    [InlineData("/exames/123")]
    [InlineData("https://exemplo.com")]
    [InlineData("exames")]
    [InlineData("/EXAMES")]
    public void Rota_fora_da_lista_fixa_nao_passa(string rota)
    {
        var ex = Assert.Throws<ValidacaoException>(() =>
            PushCidadaoService.ValidarEnvio(new EnviarNotificacaoAppRequest("Título", "Mensagem", rota)));
        Assert.Equal(new[] { "rota" }, ex.Erros.Keys);
    }

    // ---------- registro do aparelho ----------

    [Fact]
    public void Aparelho_normaliza_plataforma_e_apara_o_token()
    {
        var (token, plataforma) = PushCidadaoService.ValidarAparelho(new RegistrarDispositivoRequest("  abc:123  ", "IOS"));

        Assert.Equal("abc:123", token);
        Assert.Equal("ios", plataforma);
    }

    [Fact]
    public void Aparelho_sem_token_ou_com_plataforma_desconhecida_e_recusado()
    {
        var ex = Assert.Throws<ValidacaoException>(() =>
            PushCidadaoService.ValidarAparelho(new RegistrarDispositivoRequest(" ", "windows")));
        Assert.Equal(new[] { "plataforma", "token" }, ex.Erros.Keys.Order());

        var longo = Assert.Throws<ValidacaoException>(() =>
            PushCidadaoService.ValidarAparelho(new RegistrarDispositivoRequest(new string('x', 4097), "android")));
        Assert.Equal(new[] { "token" }, longo.Erros.Keys);
    }

    // ---------- conta de serviço ----------

    [Fact]
    public void Conta_de_servico_valida_e_lida()
    {
        var (json, _) = ContaServicoFcmFabrica.Gerar();

        var conta = ContaServicoFcm.Ler(json);

        Assert.Equal(ContaServicoFcmFabrica.ProjectId, conta.ProjectId);
        Assert.Equal(ContaServicoFcmFabrica.ClientEmail, conta.ClientEmail);
        Assert.Equal(ContaServicoFcmFabrica.PrivateKeyId, conta.PrivateKeyId);
        Assert.Equal("https://oauth2.googleapis.com/token", conta.TokenUri);
        Assert.DoesNotContain("PRIVATE KEY", conta.ToString());
    }

    [Fact]
    public void Sem_token_uri_usa_o_do_google()
    {
        var (json, _) = ContaServicoFcmFabrica.Gerar(tokenUri: null);
        Assert.Equal(ContaServicoFcm.TokenUriPadrao, ContaServicoFcm.Ler(json).TokenUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("não é json")]
    [InlineData("[1,2]")]
    [InlineData("{\"type\":\"authorized_user\"}")]
    public void Texto_que_nao_e_conta_de_servico_e_recusado(string json)
    {
        var ex = Assert.Throws<ValidacaoException>(() => ContaServicoFcm.Ler(json));
        Assert.Equal(new[] { "fcm.conta_servico_invalida" }, ex.Erros.Keys);
    }

    [Fact]
    public void Conta_sem_chave_diz_o_campo_que_falta()
    {
        var (json, _) = ContaServicoFcmFabrica.Gerar(semChave: true);

        var ex = Assert.Throws<ValidacaoException>(() => ContaServicoFcm.Ler(json));

        Assert.Contains("private_key", ex.Message);
    }

    [Fact]
    public void Chave_que_nao_e_pem_e_recusada()
    {
        var (json, _) = ContaServicoFcmFabrica.Gerar();
        var corrompido = System.Text.RegularExpressions.Regex.Replace(
            json, "\"private_key\":\"[^\"]*\"", "\"private_key\":\"nao-e-uma-chave\"");

        var ex = Assert.Throws<ValidacaoException>(() => ContaServicoFcm.Ler(corrompido));

        Assert.Contains("chave privada", ex.Message);
    }

    [Theory]
    [InlineData("https://exemplo.com/token")]
    [InlineData("http://oauth2.googleapis.com/token")]
    [InlineData("https://oauth2.googleapis.com.exemplo.com/token")]
    public void Token_uri_fora_do_google_e_recusado(string tokenUri)
    {
        var (json, _) = ContaServicoFcmFabrica.Gerar(tokenUri: tokenUri);

        var ex = Assert.Throws<ValidacaoException>(() => ContaServicoFcm.Ler(json));

        Assert.Contains("token_uri", ex.Message);
    }

    // ---------- histórico ----------

    [Fact]
    public void Falha_do_historico_resume_quantos_e_por_que()
    {
        var removido = InterpretadorRespostaFcm.Interpretar(System.Net.HttpStatusCode.NotFound,
            "{\"error\":{\"code\":404,\"status\":\"NOT_FOUND\",\"message\":\"x\",\"details\":[{\"@type\":\"type.googleapis.com/google.firebase.fcm.v1.FcmError\",\"errorCode\":\"UNREGISTERED\"}]}}");

        Assert.Null(PushCidadaoService.ResumirFalha(1, [DesfechoEnvioFcm.Aceito]));
        Assert.Equal("1 de 2 aparelhos recusou: app desinstalado",
            PushCidadaoService.ResumirFalha(2, [DesfechoEnvioFcm.Aceito, removido]));
        Assert.Equal("2 de 2 aparelhos recusaram: app desinstalado; Firebase indisponível",
            PushCidadaoService.ResumirFalha(2, [removido, DesfechoEnvioFcm.Indisponivel("t")]));
    }

    [Fact]
    public void Sender_id_mismatch_entra_no_historico_como_configuracao_e_nao_como_desinstalado()
    {
        var mismatch = InterpretadorRespostaFcm.Interpretar(System.Net.HttpStatusCode.Forbidden,
            "{\"error\":{\"code\":403,\"status\":\"PERMISSION_DENIED\",\"message\":\"SenderId mismatch\",\"details\":[{\"@type\":\"type.googleapis.com/google.firebase.fcm.v1.FcmError\",\"errorCode\":\"SENDER_ID_MISMATCH\"}]}}");

        Assert.False(mismatch.AparelhoRemovido);
        Assert.Equal("2 de 2 aparelhos recusaram: app ligado a outro projeto do Firebase",
            PushCidadaoService.ResumirFalha(2, [mismatch, DesfechoEnvioFcm.OutroProjeto("t")]));
    }
}
