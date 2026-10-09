using FluentAssertions;
using SMSMais.Core.Integracoes.SisregWeb.Rede;

namespace SMSMais.Tests.Integracoes;

/// <summary>
/// Verificador do endereço do SISREG. Caso real: 09/10/2026 o SISREG foi para trás do F5
/// (189.28.130.13 → 159.60.146.75), a produção ficou fora do túnel e o login falhou — parecia o IP
/// da Eveo bloqueado. O que está sob teste é o que decide o que a tela mostra e quando o celular toca.
/// </summary>
public class EnderecoSisregTests
{
    private const string Tunel = "wg-eveo";
    private static readonly DateTime Agora = new(2026, 10, 9, 12, 48, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("159.60.146.75 dev wg-eveo src 10.206.0.2 uid 1001 \\    cache ", "wg-eveo")]
    [InlineData("159.60.146.75 via 146.190.64.1 dev eth0 src 146.190.65.73 uid 1001 \\    cache ", "eth0")]
    [InlineData("RTNETLINK answers: Network is unreachable", null)]
    [InlineData("", null)]
    public void LerInterface_tira_o_dev_da_saida_do_ip_route_get(string saida, string? esperado) =>
        SondaEnderecoSisreg.LerInterface(saida).Should().Be(esperado);

    [Fact]
    public void Reconciliar_ip_novo_abre_e_o_antigo_recente_continua_aberto()
    {
        var antigo = new EnderecoSisregService.PeriodoAberto(Guid.NewGuid(), "189.28.130.13", Agora.AddMinutes(-2));

        var plano = EnderecoSisregService.Reconciliar([antigo], ["159.60.146.75"], Agora, TimeSpan.FromMinutes(30));

        plano.Abrir.Should().Equal("159.60.146.75");
        plano.Encerrar.Should().BeEmpty("sumiu do DNS há 2 min — pode ser alternância, não troca");
    }

    [Fact]
    public void Reconciliar_ip_que_sumiu_ha_mais_que_o_limite_encerra()
    {
        var antigo = new EnderecoSisregService.PeriodoAberto(Guid.NewGuid(), "189.28.130.13", Agora.AddMinutes(-31));
        var atual = new EnderecoSisregService.PeriodoAberto(Guid.NewGuid(), "159.60.146.75", Agora.AddMinutes(-2));

        var plano = EnderecoSisregService.Reconciliar([antigo, atual], ["159.60.146.75"], Agora, TimeSpan.FromMinutes(30));

        plano.Abrir.Should().BeEmpty("o IP atual já tem período aberto");
        plano.Encerrar.Should().Equal(antigo.Id);
    }

    [Fact]
    public void Reconciliar_dns_vazio_nao_encerra_nada()
    {
        var antigo = new EnderecoSisregService.PeriodoAberto(Guid.NewGuid(), "159.60.146.75", Agora.AddHours(-5));

        var plano = EnderecoSisregService.Reconciliar([antigo], [], Agora, TimeSpan.FromMinutes(30));

        plano.Encerrar.Should().BeEmpty("DNS fora do ar não é troca de IP");
        plano.Abrir.Should().BeEmpty();
    }

    public static TheoryData<string, UltimaVerificacaoEndereco?, SituacaoEnderecoSisreg> Situacoes => new()
    {
        { "sem verificação", null, SituacaoEnderecoSisreg.NaoVerificado },
        { "DNS falhou", new(Agora, [], Tunel, "O DNS não resolveu"), SituacaoEnderecoSisreg.SemDns },
        { "no túnel", new(Agora, [new("159.60.146.75", Tunel)], Tunel, null), SituacaoEnderecoSisreg.NoTunel },
        { "fora do túnel (09/10)", new(Agora, [new("159.60.146.75", "eth0")], Tunel, null), SituacaoEnderecoSisreg.ForaDoTunel },
        { "servidor sem túnel", new(Agora, [new("159.60.146.75", "eth0")], null, null), SituacaoEnderecoSisreg.SaidaDireta },
        { "rota não lida", new(Agora, [new("159.60.146.75", null)], Tunel, null), SituacaoEnderecoSisreg.RotaDesconhecida },
    };

    [Theory]
    [MemberData(nameof(Situacoes))]
    public void Situacao_do_card(string caso, UltimaVerificacaoEndereco? ultima, SituacaoEnderecoSisreg esperada) =>
        EnderecoSisregService.Situacao(ultima).Should().Be(esperada, caso);

    private static MudancaEndereco Troca(bool primeiraVez = false) =>
        new(["159.60.146.75"], ["189.28.130.13"], [], primeiraVez);

    private static readonly MudancaEndereco Nada = new([], ["159.60.146.75"], [], false);

    [Fact]
    public void Decidir_primeira_verificacao_da_historia_so_registra() =>
        VerificadorEnderecoSisregWorker.Decidir(Troca(primeiraVez: true), false, false, false).AvisarTroca
            .Should().BeFalse();

    [Fact]
    public void Decidir_ip_novo_avisa_a_troca() =>
        VerificadorEnderecoSisregWorker.Decidir(Troca(), false, false, false).AvisarTroca.Should().BeTrue();

    [Fact]
    public void Decidir_fora_do_tunel_uma_vez_nao_avisa_duas_seguidas_avisa()
    {
        // A primeira pode ser o minuto entre a troca e o conserto do timer do servidor.
        VerificadorEnderecoSisregWorker.Decidir(Nada, foraAgora: true, foraNaAnterior: false, foraAvisado: false)
            .AvisarForaDoTunel.Should().BeFalse();
        VerificadorEnderecoSisregWorker.Decidir(Nada, foraAgora: true, foraNaAnterior: true, foraAvisado: false)
            .AvisarForaDoTunel.Should().BeTrue();
    }

    [Fact]
    public void Decidir_fora_ja_avisado_nao_repete_e_avisa_quando_volta()
    {
        VerificadorEnderecoSisregWorker.Decidir(Nada, foraAgora: true, foraNaAnterior: true, foraAvisado: true)
            .Should().Be(new VerificadorEnderecoSisregWorker.DecisaoAvisos(false, false, false));
        VerificadorEnderecoSisregWorker.Decidir(Nada, foraAgora: false, foraNaAnterior: true, foraAvisado: true)
            .AvisarTunelOk.Should().BeTrue();
    }

    [Fact]
    public void Aviso_de_troca_diz_o_ip_novo_o_antigo_e_o_que_fazer()
    {
        var (titulo, detalhe) = VerificadorEnderecoSisregWorker.MontarTroca(
            "sisregiii.saude.gov.br", Troca(), new ObservacaoEndereco([new("159.60.146.75", "eth0")], Tunel));

        titulo.Should().Contain("159.60.146.75");
        detalhe.Should().Contain("antes: 189.28.130.13").And.Contain(Tunel).And.Contain("Integrações");
    }

    [Fact]
    public void Aviso_de_fora_do_tunel_diz_por_onde_esta_saindo()
    {
        var (_, detalhe) = VerificadorEnderecoSisregWorker.MontarForaDoTunel(
            "sisregiii.saude.gov.br", [new("159.60.146.75", "eth0")], Tunel);

        detalhe.Should().Contain("159.60.146.75 (sai por eth0)").And.Contain("sisreg-egress-verificar");
    }
}
