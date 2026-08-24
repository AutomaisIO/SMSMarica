using SMSMarica.Core.Integracoes.Pep.Divergencias;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Integracoes.Pep;

/// <summary>
/// Arbitragem de divergência de identidade (ADR-0039): mesmo CPF com data de nascimento
/// diferente entre a origem (PEP) e o hub. O árbitro é a consulta oficial de CPF, que só
/// valida quando o par CPF + nascimento confere — perguntar pelas duas datas resolve o
/// conflito sem opinião nossa.
///
/// Lógica pura: nada de banco, HTTP ou Docker. Cada teste injeta o comportamento da consulta.
/// </summary>
public class ArbitroIdentidadeTests
{
    private const string Origem = "1980-05-10";
    private const string Hub = "1975-05-10";

    /// <summary>Consulta dublê: mapeia datas → resultado, contando quantas vezes foi chamada.</summary>
    private sealed class ConsultaFake(Func<DateOnly, ResultadoConsultaCpf> mapa)
    {
        public int Chamadas { get; private set; }

        public Task<(ResultadoConsultaCpf, string?)> Consultar(DateOnly d)
        {
            Chamadas++;
            var r = mapa(d);
            var detalhe = r switch
            {
                ResultadoConsultaCpf.Negou => "Dados divergentes.",
                ResultadoConsultaCpf.Indisponivel => "O serviço não respondeu agora.",
                _ => null,
            };
            return Task.FromResult<(ResultadoConsultaCpf, string?)>((r, detalhe));
        }
    }

    private static ResultadoConsultaCpf ValidaSomente(DateOnly valida, DateOnly d) =>
        d == valida ? ResultadoConsultaCpf.Validou : ResultadoConsultaCpf.Negou;

    [Fact]
    public async Task Data_da_origem_confere_entao_origem_esta_certa_e_a_2a_consulta_e_dispensada()
    {
        var fake = new ConsultaFake(d => ValidaSomente(DateOnly.Parse(Origem), d));

        var r = await ArbitroIdentidade.ArbitrarNascimentoAsync(Origem, Hub, fake.Consultar);

        Assert.Equal(VeredictoDivergenciaIdentidade.OrigemCorreta, r.Veredicto);
        Assert.Equal(Origem, r.ValorCorreto);
        Assert.False(r.Indisponivel);
        // Data exata confere ⇒ a outra não pode conferir: 1 consulta basta (economia de saldo).
        Assert.Equal(1, r.ConsultasGastas);
        Assert.Equal(1, fake.Chamadas);
    }

    [Fact]
    public async Task Data_do_hub_confere_entao_a_origem_esta_errada_e_nao_pode_sobrescrever()
    {
        var fake = new ConsultaFake(d => ValidaSomente(DateOnly.Parse(Hub), d));

        var r = await ArbitroIdentidade.ArbitrarNascimentoAsync(Origem, Hub, fake.Consultar);

        Assert.Equal(VeredictoDivergenciaIdentidade.HubCorreto, r.Veredicto);
        Assert.Equal(Hub, r.ValorCorreto);
        Assert.Equal(2, r.ConsultasGastas); // precisou perguntar pelas duas
        Assert.Contains("NÃO pode sobrescrever", r.Detalhe);
    }

    [Fact]
    public async Task Nenhuma_das_datas_confere_entao_o_CPF_e_suspeito()
    {
        var fake = new ConsultaFake(_ => ResultadoConsultaCpf.Negou);

        var r = await ArbitroIdentidade.ArbitrarNascimentoAsync(Origem, Hub, fake.Consultar);

        Assert.Equal(VeredictoDivergenciaIdentidade.AmbosNegados, r.Veredicto);
        Assert.Null(r.ValorCorreto);
        Assert.False(r.Indisponivel);
        Assert.Contains("suspeito", r.Detalhe);
    }

    [Fact]
    public async Task Consulta_indisponivel_na_1a_pergunta_nao_arbitra_e_nao_gasta_a_2a()
    {
        var fake = new ConsultaFake(_ => ResultadoConsultaCpf.Indisponivel);

        var r = await ArbitroIdentidade.ArbitrarNascimentoAsync(Origem, Hub, fake.Consultar);

        Assert.True(r.Indisponivel);
        Assert.Equal(VeredictoDivergenciaIdentidade.Indefinido, r.Veredicto);
        Assert.Null(r.ValorCorreto);
        Assert.Equal(1, fake.Chamadas);
    }

    [Fact]
    public async Task Indisponibilidade_so_na_2a_pergunta_tambem_nao_arbitra()
    {
        var origemData = DateOnly.Parse(Origem);
        var fake = new ConsultaFake(d =>
            d == origemData ? ResultadoConsultaCpf.Negou : ResultadoConsultaCpf.Indisponivel);

        var r = await ArbitroIdentidade.ArbitrarNascimentoAsync(Origem, Hub, fake.Consultar);

        Assert.True(r.Indisponivel);
        Assert.Equal(VeredictoDivergenciaIdentidade.Indefinido, r.Veredicto);
        Assert.Equal(2, r.ConsultasGastas);
    }

    [Theory]
    [InlineData("10/05/1980", "1975-05-10")] // origem em formato brasileiro
    [InlineData("1980-05-10", "1975")]       // hub com data parcial
    [InlineData("", "1975-05-10")]
    public async Task Valor_que_nao_e_data_ISO_completa_nao_gasta_consulta(string origem, string hub)
    {
        var fake = new ConsultaFake(_ => ResultadoConsultaCpf.Validou);

        var r = await ArbitroIdentidade.ArbitrarNascimentoAsync(origem, hub, fake.Consultar);

        Assert.Equal(VeredictoDivergenciaIdentidade.Inconclusivo, r.Veredicto);
        Assert.Equal(0, r.ConsultasGastas);
        Assert.Equal(0, fake.Chamadas); // nenhuma consulta paga desperdiçada
    }

    [Fact]
    public async Task A_ordem_das_perguntas_e_origem_primeiro()
    {
        var perguntadas = new List<DateOnly>();
        Task<(ResultadoConsultaCpf, string?)> Consultar(DateOnly d)
        {
            perguntadas.Add(d);
            return Task.FromResult<(ResultadoConsultaCpf, string?)>((ResultadoConsultaCpf.Negou, "Dados divergentes."));
        }

        await ArbitroIdentidade.ArbitrarNascimentoAsync(Origem, Hub, Consultar);

        Assert.Equal([DateOnly.Parse(Origem), DateOnly.Parse(Hub)], perguntadas);
    }
}
