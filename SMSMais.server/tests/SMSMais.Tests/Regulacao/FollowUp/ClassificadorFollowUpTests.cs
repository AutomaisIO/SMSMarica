using SMSMais.Core.Regulacao.FollowUp;

namespace SMSMais.Tests.Regulacao.FollowUp;

/// <summary>
/// O classificador de follow-up (plano 09, tarefa 4.6) rodando sobre a <b>semente medida no spike
/// d</b> — as mesmas regras que serão gravadas na configuração, não uma versão de brinquedo.
///
/// <para>Não precisa de banco: é função pura. É por isso que dá para cobrir a ordem das regras e a
/// normalização caso a caso, que é onde a classificação erra na prática.</para>
/// </summary>
public class ClassificadorFollowUpTests
{
    private static readonly IReadOnlyList<RegraFollowUp> Semente =
        ClassificadorFollowUp.Ler(SementeFollowUp.Json);

    [Fact]
    public void A_semente_do_spike_d_e_json_valido_com_as_nove_categorias_ordenadas()
    {
        Semente.Should().HaveCount(8);   // oito regras; a nona categoria é o "Outro" do fallback
        Semente.Select(r => r.Ordem).Should().OnlyHaveUniqueItems();
        Semente.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.Padrao));
    }

    [Theory]
    [InlineData("Risco reclassificado pelo Regulador.", "ReclassificacaoRisco")]
    [InlineData("Não atende", "FalhaContato")]
    [InlineData("Contato realizado, paciente ciente da data.", "ContatoRealizado")]
    [InlineData("Favor anexar o laudo do cardiologista.", "SolicitacaoAoSolicitante")]
    [InlineData("Sem vaga disponível no momento.", "SemVaga")]
    [InlineData("Paciente agendado para 12/09.", "Agendamento")]
    [InlineData("bom dia", "Outro")]
    public void Classifica_os_textos_reais_de_cada_categoria(string texto, string esperado) =>
        ClassificadorFollowUp.Classificar(texto, Semente).Categoria.Should().Be(esperado);

    [Fact]
    public void A_ordem_decide_quando_o_texto_fala_de_duas_coisas()
    {
        // Os operadores empilham assunto na mesma caixa. Aqui cabem FalhaContato (2) e SemVaga (7):
        // quem vence é a que pede ação da unidade — inverter a ordem mudaria se abre pendência.
        var r = ClassificadorFollowUp.Classificar(
            "Sem contato com o paciente; permanece em fila de espera.", Semente);

        r.Categoria.Should().Be("FalhaContato");
        r.ViraPendencia.Should().Be("contato");
        r.OrdemDaRegra.Should().Be(2);
    }

    [Fact]
    public void Informar_ao_paciente_nao_e_pedido_a_unidade()
    {
        // O que mais parecia "documento criticado" no corpus era isto: orientação ao paciente,
        // que não pede nada de quem solicitou. Confundir as duas foi o que inflou a fila em 5,5×
        // na primeira medição do spike d.
        var r = ClassificadorFollowUp.Classificar(
            "Favor informar ao paciente que deve levar documentos de identificação.", Semente);

        r.Categoria.Should().Be("OrientacaoAoPaciente");
        r.ViraPendencia.Should().BeNull();
    }

    [Theory]
    [InlineData("NÃO ATENDE")]
    [InlineData("não   atende")]
    [InlineData("Nao Atende")]
    public void Acento_caixa_e_espaco_a_mais_nao_mudam_a_classificacao(string texto)
    {
        // O campo é digitado à mão por centenas de operadores. As regras do spike d foram escritas
        // sobre a forma normalizada; aplicá-las ao texto cru perderia a maioria das ocorrências.
        ClassificadorFollowUp.Classificar(texto, Semente).Categoria.Should().Be("FalhaContato");
    }

    [Fact]
    public void Sem_regras_gravadas_o_classificador_fica_desligado()
    {
        // Estado de fábrica: `regras_followup_json` nasce vazio. Nada vira pendência sem curadoria.
        ClassificadorFollowUp.Classificar("Não atende", []).Categoria.Should().Be("Outro");
        ClassificadorFollowUp.Ler(null).Should().BeEmpty();
        ClassificadorFollowUp.Ler("isto não é json").Should().BeEmpty();
    }

    [Fact]
    public void Regra_quebrada_nao_derruba_as_seguintes()
    {
        // Se uma regex inválida tivesse sido gravada antes de o validador existir, ela não pode
        // levar junto a classificação de todo o resto — em produção, isso seria a varredura
        // noturna devolvendo "Outro" para 19 mil eventos.
        List<RegraFollowUp> comLixo =
        [
            new("SemVaga", 1, "((((", null),
            .. Semente,
        ];

        ClassificadorFollowUp.Classificar("Não atende", comLixo).Categoria.Should().Be("FalhaContato");
    }

    [Fact]
    public void So_duas_categorias_abrem_pendencia()
    {
        // As outras sete são estado da fila externa: viram informação na linha do tempo, não
        // tarefa para a unidade.
        Semente.Where(r => r.ViraPendencia is not null)
            .Select(r => r.Categoria)
            .Should().BeEquivalentTo(["FalhaContato", "SolicitacaoAoSolicitante"]);
    }

    [Fact]
    public void Texto_vazio_cai_em_outro_sem_estourar()
    {
        ClassificadorFollowUp.Classificar(null, Semente).Categoria.Should().Be("Outro");
        ClassificadorFollowUp.Classificar("   ", Semente).Categoria.Should().Be("Outro");
        ClassificadorFollowUp.Normalizar(null).Should().BeEmpty();
    }
}
