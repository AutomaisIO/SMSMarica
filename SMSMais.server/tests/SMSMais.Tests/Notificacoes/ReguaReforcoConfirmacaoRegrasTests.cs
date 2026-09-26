using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.WhatsApp;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// Régua de reforço da confirmação — as regras puras (sem banco, sem rede): o que vai em cada
/// modelo, qual modelo sai, quem está no público de cada toque e as formas do número.
///
/// <para>O que mais importa aqui é o {{2}}: é uma CONSTANTE pelo tipo do agendamento. Nome do
/// procedimento, data ou unidade numa mensagem para número não provado é o vazamento que o
/// ADR-0057 fecha — um teste que quebra se alguém "melhorar" a mensagem com o nome do exame.</para>
/// </summary>
public sealed class ReguaReforcoConfirmacaoRegrasTests
{
    private static readonly ComunicacaoPacienteOptions Opts = new();

    private static TemplateWhatsApp Modelo(string nome) => new(nome, "pt_BR", "UTILITY", null, 2, []);

    // ---------- (a) {{2}} é constante pelo tipo ----------

    [Theory]
    [InlineData(FinalidadeComunicacao.ReforcoConfirmacao, TipoAgendamento.Exame, "agendamento_aguardando_resposta", "sobre seu exame")]
    [InlineData(FinalidadeComunicacao.ReforcoConfirmacao, TipoAgendamento.Consulta, "agendamento_aguardando_resposta", "sobre sua consulta")]
    [InlineData(FinalidadeComunicacao.OrientacaoPosto, TipoAgendamento.Exame, "agendamento_procure_posto", "do seu exame")]
    [InlineData(FinalidadeComunicacao.OrientacaoPosto, TipoAgendamento.Consulta, "agendamento_procure_posto", "da sua consulta")]
    public void Parametros_sao_o_tratamento_e_uma_constante_do_tipo(
        FinalidadeComunicacao finalidade, TipoAgendamento tipo, string modeloEsperado, string complemento)
    {
        var envio = ReguaReforcoConfirmacao.MontarEnvio(
            finalidade, tipo, "Sra. Maria", principalLida: true, catalogo: [], Opts);

        Assert.Equal(modeloEsperado, envio.Modelo);
        Assert.Equal(new[] { "Sra. Maria", complemento }, envio.Parametros);
        // O texto da thread é o corpo aprovado com os valores — e nada além deles.
        Assert.Contains("*Sra. Maria*", envio.ConteudoLegivel);
        Assert.Contains(complemento, envio.ConteudoLegivel);
        Assert.DoesNotContain("{{", envio.ConteudoLegivel);
    }

    [Fact]
    public void Modelo_A_leva_do_seu_exame_e_o_rodape()
    {
        var envio = ReguaReforcoConfirmacao.MontarEnvio(
            FinalidadeComunicacao.ReforcoConfirmacao, TipoAgendamento.Exame, "Sr. João", principalLida: false,
            [Modelo("agendamento_aviso_pendente")], Opts);

        Assert.Equal("agendamento_aviso_pendente", envio.Modelo);
        Assert.Equal(new[] { "Sr. João", "do seu exame" }, envio.Parametros);
        Assert.Contains("A confirmação *do seu exame* continua pendente", envio.ConteudoLegivel);
        Assert.EndsWith("Nunca pedimos CPF completo, senha ou pagamento.", envio.ConteudoLegivel);
    }

    [Fact]
    public void Orientacao_ao_posto_oferece_os_tres_botoes_no_texto()
    {
        var texto = ReguaReforcoConfirmacao.MontarEnvio(
            FinalidadeComunicacao.OrientacaoPosto, TipoAgendamento.Exame, "Maria", true, [], Opts).ConteudoLegivel;

        Assert.Contains("*Quero mais informações*", texto);
        Assert.Contains("*Vou ao posto*", texto);
        Assert.Contains("*Não sou essa pessoa*", texto);
        Assert.Contains("não vamos mais insistir por mensagem", texto);
    }

    /// <summary>
    /// O texto gravado na thread é o corpo APROVADO na Meta, palavra por palavra — é o que a
    /// atendente lê como "o que o cidadão recebeu". Os três corpos abaixo são cópia dos submetidos
    /// em 24/09/2026; se o modelo mudar lá, este teste obriga a mudar aqui também.
    /// </summary>
    [Theory]
    [InlineData("agendamento_aguardando_resposta", "sobre seu exame",
        "Olá *Sra. Maria*, esse é o canal oficial do *Alô Maricá* da Secretaria Municipal de Saúde.\n\nNossa mensagem sobre o seu agendamento chegou, mas ainda não recebemos sua resposta. Sem ela, não podemos enviar as informações *sobre seu exame* por aqui: é uma regra de segurança para proteger os dados do paciente.\n\nToque em *Quero mais informações*.\n\nSe preferir não responder, tudo bem: o agendamento continua valendo, e a guia com dia, hora e local pode ser retirada no posto de saúde onde o paciente tem cadastro.\n\nSe esta mensagem não é para você, toque em *Não sou essa pessoa*.")]
    [InlineData("agendamento_procure_posto", "do seu exame",
        "Olá *Sra. Maria*, esse é o canal oficial do *Alô Maricá* da Secretaria Municipal de Saúde.\n\nTentamos falar com você sobre o seu agendamento, mas não conseguimos confirmar os dados por aqui. Tudo bem: não vamos mais insistir por mensagem.\n\nO seu agendamento continua valendo. Para saber o dia, a hora e o local e retirar a guia, procure o *posto de saúde onde o paciente tem cadastro*, com um documento com foto.\n\nSe preferir ver as informações do seu exame por aqui, ainda dá: toque em *Quero mais informações*. Se vai ao posto, toque em *Vou ao posto*. Se esta mensagem não é para você, toque em *Não sou essa pessoa*")]
    [InlineData("agendamento_aviso_pendente", "do seu exame",
        "Olá *Sra. Maria*, esse é o canal oficial do *Alô Maricá* da Secretaria Municipal de Saúde.\n\nA confirmação *do seu exame* continua pendente: enviamos uma mensagem sobre o seu agendamento e ainda não tivemos resposta.\n\nSe esta mensagem não é para você, toque em *Não sou essa pessoa*\n\nNunca pedimos CPF completo, senha ou pagamento.")]
    public void Texto_da_thread_e_o_corpo_aprovado_com_os_valores(string modelo, string complemento, string esperado)
    {
        Assert.Equal(esperado, ReguaReforcoConfirmacao.TextoDoModelo(modelo, "Sra. Maria", complemento, Opts));
    }

    // ---------- (b) escolha A/B ----------

    [Theory]
    // Não leu + A aprovado (presente no catálogo) → A.
    [InlineData(false, true, "agendamento_aviso_pendente")]
    // Não leu + A ainda em recurso (ausente) → B, cujo texto vale para os dois casos.
    [InlineData(false, false, "agendamento_aguardando_resposta")]
    // Leu → B, mesmo com A aprovado: "a confirmação continua pendente" soaria como se não tivesse chegado.
    [InlineData(true, true, "agendamento_aguardando_resposta")]
    [InlineData(true, false, "agendamento_aguardando_resposta")]
    public void Reforco_so_usa_o_A_para_quem_nao_leu_e_com_o_A_aprovado(bool lida, bool aAprovado, string esperado)
    {
        IReadOnlyList<TemplateWhatsApp> catalogo = aAprovado
            ? [Modelo("agendamento_aguardando_resposta"), Modelo("agendamento_aviso_pendente")]
            : [Modelo("agendamento_aguardando_resposta")];

        Assert.Equal(esperado, ReguaReforcoConfirmacao.ModeloDoReforco(lida, catalogo, Opts));
    }

    [Fact]
    public void Catalogo_indisponivel_e_B()
    {
        // O relay fora (ou modo simulado) devolve lista vazia: na dúvida, o modelo que existe.
        Assert.Equal("agendamento_aguardando_resposta", ReguaReforcoConfirmacao.ModeloDoReforco(false, [], Opts));
    }

    // ---------- público de cada toque ----------

    private static VerificacaoCadastralEstado Estado(
        EtapaVerificacaoCadastral etapa, Guid comunicacaoId, DateTime atualizadoEm, Guid? pacienteId = null) => new()
    {
        Id = Guid.NewGuid(),
        TelefoneCanonical = "5521987654321",
        ComunicacaoPacienteId = comunicacaoId,
        PacienteId = pacienteId,
        Etapa = etapa,
        ExpiraEm = atualizadoEm.AddDays(7),
        CriadoEm = atualizadoEm,
    };

    [Fact]
    public void Reforco_so_para_quem_nao_mandou_nada_e_nao_tem_dialogo_andando()
    {
        var agora = DateTime.UtcNow;
        var principal = Guid.NewGuid();

        Assert.True(ReguaReforcoConfirmacao.ReforcoAlcanca(false, null));
        Assert.True(ReguaReforcoConfirmacao.ReforcoAlcanca(false,
            Estado(EtapaVerificacaoCadastral.AguardandoInteresse, principal, agora)));
        Assert.False(ReguaReforcoConfirmacao.ReforcoAlcanca(true, null));
        Assert.False(ReguaReforcoConfirmacao.ReforcoAlcanca(false,
            Estado(EtapaVerificacaoCadastral.AguardandoNascimento, principal, agora.AddDays(-5))));
    }

    [Fact]
    public void Orientacao_alcanca_quem_parou_no_meio_ha_dias_mas_nunca_o_esgotado()
    {
        var agora = DateTime.UtcNow;
        var principal = Guid.NewGuid();
        var paciente = Guid.NewGuid();

        // Parado no nascimento há 3 dias, apontando para este paciente.
        Assert.True(ReguaReforcoConfirmacao.OrientacaoAlcanca(true,
            Estado(EtapaVerificacaoCadastral.AguardandoNascimento, principal, agora.AddHours(-73)),
            principal, paciente, agora));
        // Parado há pouco: ainda pode voltar sozinho.
        Assert.False(ReguaReforcoConfirmacao.OrientacaoAlcanca(true,
            Estado(EtapaVerificacaoCadastral.AguardandoNascimento, principal, agora.AddHours(-10)),
            principal, paciente, agora));
        // Parado, mas o diálogo é de OUTRO paciente do número.
        Assert.False(ReguaReforcoConfirmacao.OrientacaoAlcanca(true,
            Estado(EtapaVerificacaoCadastral.AguardandoNome, Guid.NewGuid(), agora.AddDays(-4), Guid.NewGuid()),
            principal, paciente, agora));
        // Esgotado já ouviu "procure o posto" da própria máquina — nem sem entrada nenhuma.
        Assert.False(ReguaReforcoConfirmacao.OrientacaoAlcanca(false,
            Estado(EtapaVerificacaoCadastral.Esgotado, principal, agora.AddDays(-4)),
            principal, paciente, agora));
        // Nunca respondeu nada: alcança.
        Assert.True(ReguaReforcoConfirmacao.OrientacaoAlcanca(false, null, principal, paciente, agora));
    }

    // ---------- domingo ----------

    [Fact]
    public void Domingo_e_o_de_Brasilia_e_a_espera_vai_para_segunda_na_abertura()
    {
        // Domingo 27/09/2026, 01:00 UTC = sábado 22:00 em Brasília: ainda NÃO é domingo lá.
        Assert.False(ReguaReforcoConfirmacao.EhDomingo(new DateTime(2026, 9, 27, 1, 0, 0, DateTimeKind.Utc)));
        // Domingo 27/09/2026, 13:00 UTC = 10:00 em Brasília.
        var domingo = new DateTime(2026, 9, 27, 13, 0, 0, DateTimeKind.Utc);
        Assert.True(ReguaReforcoConfirmacao.EhDomingo(domingo));

        // Segunda 28/09/2026 às 08:00 de Brasília = 11:00 UTC.
        Assert.Equal(new DateTime(2026, 9, 28, 11, 0, 0, DateTimeKind.Utc),
            ReguaReforcoConfirmacao.ProximaAberturaForaDoDomingo(domingo, new TimeOnly(8, 0)));
    }

    // ---------- o número ----------

    [Fact]
    public void Formas_do_numero_incluem_o_wa_id_antigo_sem_o_nono_digito()
    {
        var formas = ReguaReforcoConfirmacao.FormasDoNumero("(21) 98765-4321");

        Assert.Contains("5521987654321", formas);
        // O WhatsApp ainda identifica muito celular antigo SEM o 9: a resposta chega assim.
        Assert.Contains("552187654321", formas);
        Assert.Equal("5521987654321", ReguaReforcoConfirmacao.ChaveDoNumero("552187654321"));
    }
}
