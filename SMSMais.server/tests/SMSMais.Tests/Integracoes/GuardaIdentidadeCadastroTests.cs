using SMSMais.Core.Integracoes.Cadastro;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Cada teste aqui fixa um caso REAL medido na implantação do histórico do SISREG (07–08/09/2026),
/// quando o SER devolveu 595 fichas com identificador diferente do perguntado e o código usava o
/// que voltasse.
/// </summary>
public class GuardaIdentidadeCadastroTests
{
    [Fact]
    // O caminho normal não pode ficar mais caro por causa da guarda.
    public void Mesmo_identificador_confere()
    {
        Assert.Equal(VeredictoCadastro.Confere,
            GuardaIdentidadeCadastro.Conferir("07787916702", "07787916702", porCpf: true));
    }

    [Fact]
    // Caso 1 do dossiê: perguntamos pelo CPF de LUCIENE e o SER devolveu a ficha de FABIANA —
    // confirmado na Receita Federal como PESSOAS DIFERENTES. Sem a guarda, o cadastro de uma seria
    // completado com o nome, o nascimento e a mãe da outra.
    public void Cpf_diferente_e_troca_de_identidade()
    {
        Assert.Equal(VeredictoCadastro.TrocaDeIdentidade,
            GuardaIdentidadeCadastro.Conferir("07787916702", "094.769.827-21", porCpf: true));
    }

    [Fact]
    // 577 das 585 divergências de CNS eram a mesma pessoa com mais de um número (o provisório da
    // faixa 898 vira definitivo). Bloquear aqui quebraria 98,6% do caminho legítimo.
    public void Cns_diferente_nao_bloqueia_porque_a_pessoa_tem_mais_de_um()
    {
        Assert.Equal(VeredictoCadastro.OutroCns,
            GuardaIdentidadeCadastro.Conferir("898005880131086", "898004613582859", porCpf: false));
    }

    [Fact]
    // O defeito silencioso desta rotina: na implantação, comparar o CPF numa consulta feita por CNS
    // marcou como divergente TUDO que a fase trouxe — 49.683 respostas boas seriam descartadas.
    // O campo comparado tem de ser o campo perguntado.
    public void Compara_o_campo_perguntado_e_nao_o_outro()
    {
        // Perguntamos por CNS; o CPF da ficha é irrelevante para o veredito.
        Assert.Equal(VeredictoCadastro.Confere,
            GuardaIdentidadeCadastro.Conferir("700303973143430", "700303973143430", porCpf: false));
    }

    [Theory]
    [InlineData("000.000.000-00")]
    [InlineData("00000000000")]
    [InlineData("")]
    [InlineData(null)]
    // Primeiro falso positivo que apareceu na implantação: ficha sem o campo preenchido não é troca
    // de pessoa, é cadastro incompleto. Tratar como divergência descartaria cadastro bom.
    public void Campo_vazio_ou_de_preenchimento_nao_e_divergencia(string? devolvido)
    {
        Assert.Equal(VeredictoCadastro.Confere,
            GuardaIdentidadeCadastro.Conferir("07787916702", devolvido, porCpf: true));
    }

    [Fact]
    // As fontes alternam formato no MESMO campo. Comparar texto cru acusaria divergência em toda
    // ficha formatada — e o alarme que dispara sempre é o alarme que ninguém olha.
    public void Formatacao_diferente_nao_e_divergencia()
    {
        Assert.Equal(VeredictoCadastro.Confere,
            GuardaIdentidadeCadastro.Conferir("79936121791", "799.361.217-91", porCpf: true));
    }

    [Fact]
    // Sem chave não há o que conferir — e recusar aqui deixaria a consulta sem resposta por um
    // problema que é do chamador, não da fonte.
    public void Sem_chave_nao_ha_veredito()
    {
        Assert.Equal(VeredictoCadastro.Confere,
            GuardaIdentidadeCadastro.Conferir("", "07787916702", porCpf: true));
    }
}
