using SMSMais.Core.Notificacoes.VerificacaoCadastral;

namespace SMSMais.Tests.Notificacoes;

/// <summary>
/// O interpretador é a fundação do fluxo determinístico de verificação cadastral: ele decide
/// se "0452", "045.288.227-33", "março de 80" ou "prefiro falar com atendente" avançam a
/// máquina de estados. Errar aqui = travar cidadão real; ser permissivo demais = validar a
/// pessoa errada. Testes puros, sem banco.
/// </summary>
public class InterpretadorRespostaCidadaoTests
{
    private const string Cpf = "04528822733";

    // ---------- Não sou essa pessoa ----------

    [Theory]
    [InlineData("Não sou essa pessoa.", true)]
    [InlineData("nao sou essa pessoa", true)]
    [InlineData("Não conheço essa pessoa", true)]
    [InlineData("não", false)]
    [InlineData("sou essa pessoa", false)]
    public void Reconhece_o_botao_nao_sou_essa_pessoa(string texto, bool esperado) =>
        Assert.Equal(esperado, InterpretadorRespostaCidadao.EhNaoSouEssaPessoa(texto));

    // ---------- CPF ----------

    [Theory]
    [InlineData("0452", "0452")]
    [InlineData("  0452  ", "0452")]
    [InlineData("045.288", "045288")]
    [InlineData("045.288.227-33", "04528822733")]
    [InlineData("04528822733", "04528822733")]
    [InlineData("cpf 0452", "0452")]
    [InlineData("meu cpf é 0452", "0452")]
    [InlineData("Meu CPF começa com 04528", "04528")]
    public void Cpf_aceita_formatos_comuns(string entrada, string esperado) =>
        Assert.Equal(esperado, InterpretadorRespostaCidadao.ExtrairDigitosCpf(entrada));

    [Theory]
    [InlineData("045")]                       // menos de 4 dígitos
    [InlineData("123456789012")]              // mais de 11
    [InlineData("03/1980")]                   // data, não CPF
    [InlineData("moro na rua 1234 casa 56")]  // frase com números
    [InlineData("")]
    [InlineData(null)]
    public void Cpf_rejeita_o_que_nao_e_resposta_de_cpf(string? entrada) =>
        Assert.Null(InterpretadorRespostaCidadao.ExtrairDigitosCpf(entrada));

    [Theory]
    [InlineData("0452", true)]
    [InlineData("04528", true)]
    [InlineData("04528822733", true)]
    [InlineData("0453", false)]  // 4º dígito errado
    [InlineData("1828", false)]
    public void Cpf_prefixo_compara_contra_o_cadastrado(string informado, bool esperado) =>
        Assert.Equal(esperado, InterpretadorRespostaCidadao.CpfPrefixoConfere(Cpf, informado));

    [Fact]
    public void Cpf_prefixo_exige_minimo_4_digitos() =>
        Assert.False(InterpretadorRespostaCidadao.CpfPrefixoConfere(Cpf, "045"));

    // ---------- Nascimento ----------

    [Theory]
    [InlineData("03/1980", null, 3, 1980)]
    [InlineData("3/1980", null, 3, 1980)]
    [InlineData("03/80", null, 3, 1980)]
    [InlineData("03-1980", null, 3, 1980)]
    [InlineData("03 1980", null, 3, 1980)]
    [InlineData("15/03/1980", 15, 3, 1980)]
    [InlineData("15/03/80", 15, 3, 1980)]
    [InlineData("15-03-1980", 15, 3, 1980)]
    [InlineData("março de 1980", null, 3, 1980)]
    [InlineData("Março 1980", null, 3, 1980)]
    [InlineData("mar/1980", null, 3, 1980)]
    [InlineData("15 de março de 1980", 15, 3, 1980)]
    [InlineData("3 de março de 80", 3, 3, 1980)]
    [InlineData("05/2005", null, 5, 2005)]
    [InlineData("05/05", null, 5, 2005)]
    public void Nascimento_aceita_formatos_comuns(string entrada, int? dia, int mes, int ano)
    {
        var r = InterpretadorRespostaCidadao.TentarLerNascimento(entrada);
        Assert.NotNull(r);
        Assert.Equal((dia, mes, ano), r!.Value);
    }

    [Theory]
    [InlineData("13/1980")]      // mês 13
    [InlineData("0452")]         // dígitos soltos (resposta de CPF)
    [InlineData("sim")]
    [InlineData("32/03/1980")]   // dia 32
    [InlineData("")]
    [InlineData(null)]
    public void Nascimento_rejeita_o_que_nao_entende(string? entrada) =>
        Assert.Null(InterpretadorRespostaCidadao.TentarLerNascimento(entrada));

    [Fact]
    public void Nascimento_confere_mes_e_ano()
    {
        var nasc = new DateOnly(1980, 3, 15);
        Assert.True(InterpretadorRespostaCidadao.NascimentoConfere(nasc, (null, 3, 1980)));
        Assert.True(InterpretadorRespostaCidadao.NascimentoConfere(nasc, (15, 3, 1980)));
        Assert.False(InterpretadorRespostaCidadao.NascimentoConfere(nasc, (null, 4, 1980)));
        Assert.False(InterpretadorRespostaCidadao.NascimentoConfere(nasc, (null, 3, 1981)));
    }

    [Fact]
    public void Nascimento_com_dia_errado_nao_confere()
    {
        // Dia informado e errado = provável pessoa errada; não é tolerância.
        var nasc = new DateOnly(1980, 3, 15);
        Assert.False(InterpretadorRespostaCidadao.NascimentoConfere(nasc, (14, 3, 1980)));
    }

    // ---------- Sim / Não / Nome ----------

    [Theory]
    [InlineData("sim")]
    [InlineData("Sim")]
    [InlineData("SIM!")]
    [InlineData("s")]
    [InlineData("isso")]
    [InlineData("isso mesmo")]
    [InlineData("sou eu")]
    [InlineData("confirmo")]
    [InlineData("correto")]
    public void Sim_reconhece_variacoes(string entrada) =>
        Assert.True(InterpretadorRespostaCidadao.EhSim(entrada));

    [Theory]
    [InlineData("não")]
    [InlineData("nao")]
    [InlineData("n")]
    [InlineData("não sou eu")]
    [InlineData("errado")]
    public void Nao_reconhece_variacoes(string entrada) =>
        Assert.True(InterpretadorRespostaCidadao.EhNao(entrada));

    [Fact]
    public void Sim_e_nao_sao_mutuamente_exclusivos()
    {
        Assert.False(InterpretadorRespostaCidadao.EhSim("não"));
        Assert.False(InterpretadorRespostaCidadao.EhNao("sim"));
        Assert.False(InterpretadorRespostaCidadao.EhSim("talvez"));
        Assert.False(InterpretadorRespostaCidadao.EhNao("talvez"));
    }

    [Theory]
    [InlineData("Vanessa Laterza", "vanessa laterza", true)]
    [InlineData("Vanessa Laterza", "VANESSA LATERZA", true)]
    [InlineData("Maria da Silva Santos", "maria santos", true)]
    [InlineData("Maria da Silva Santos", "joão da silva", false)]  // primeiro nome não bate
    [InlineData("Maria da Silva Santos", "maria", false)]           // um nome só não basta
    [InlineData("Maria da Silva Santos", "sim", false)]
    public void Nome_digitado_confere_quando_compartilha_primeiro_nome_e_mais_um(
        string cadastrado, string resposta, bool esperado) =>
        Assert.Equal(esperado, InterpretadorRespostaCidadao.NomeConfere(cadastrado, resposta));

    // ---------- Atendente ----------

    [Theory]
    [InlineData("Prefiro falar com um atendente")]
    [InlineData("prefiro falar com atendente")]
    [InlineData("atendente")]
    [InlineData("quero falar com uma pessoa")]
    [InlineData("preciso falar com alguém")]
    [InlineData("quero falar com um humano")]
    public void Atendente_reconhece_pedidos(string entrada) =>
        Assert.True(InterpretadorRespostaCidadao.PedeAtendente(entrada));

    [Theory]
    [InlineData("0452")]
    [InlineData("sim")]
    [InlineData("03/1980")]
    [InlineData("obrigado")]
    public void Atendente_nao_dispara_em_respostas_normais(string entrada) =>
        Assert.False(InterpretadorRespostaCidadao.PedeAtendente(entrada));
}
