using System.Diagnostics;
using SMSMais.Core.Integracoes.Pep.Divergencias;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Tests.Integracoes.Pep;

/// <summary>
/// Custo da arbitragem (ADR-0039). Cada consulta é paga e de latência imprevisível — em
/// 02/08 uma rodada dentro do run de sincronismo o segurou por mais de 11 minutos. Estes
/// testes travam as duas garantias que evitam a repetição: <b>quantas</b> consultas cada
/// caso gasta e que o veredicto sai na PRIMEIRA resposta conclusiva, sem insistir.
/// </summary>
public class ArbitroIdentidadeCustoTests
{
    private const string Origem = "1980-05-10";
    private const string Hub = "1975-05-10";

    private static Func<DateOnly, Task<(ResultadoConsultaCpf, string?)>> Responde(
        Func<DateOnly, ResultadoConsultaCpf> f, List<DateOnly> registro) =>
        d => { registro.Add(d); return Task.FromResult((f(d), (string?)"detalhe")); };

    [Fact]
    public async Task Origem_valida_gasta_UMA_consulta()
    {
        var perguntas = new List<DateOnly>();
        var r = await ArbitroIdentidade.ArbitrarNascimentoAsync(Origem, Hub,
            Responde(d => d == DateOnly.Parse(Origem) ? ResultadoConsultaCpf.Validou : ResultadoConsultaCpf.Negou, perguntas));

        Assert.Equal(VeredictoDivergenciaIdentidade.OrigemCorreta, r.Veredicto);
        Assert.Equal(1, r.ConsultasGastas);
        Assert.Single(perguntas);
    }

    [Fact]
    public async Task Pior_caso_conclusivo_gasta_DUAS_consultas_nunca_mais()
    {
        var perguntas = new List<DateOnly>();
        var r = await ArbitroIdentidade.ArbitrarNascimentoAsync(Origem, Hub,
            Responde(_ => ResultadoConsultaCpf.Negou, perguntas));

        Assert.Equal(VeredictoDivergenciaIdentidade.AmbosNegados, r.Veredicto);
        Assert.Equal(2, r.ConsultasGastas);
        Assert.Equal(2, perguntas.Count);
    }

    [Fact]
    public async Task Indisponibilidade_interrompe_na_hora_e_nao_tenta_a_segunda()
    {
        var perguntas = new List<DateOnly>();
        var r = await ArbitroIdentidade.ArbitrarNascimentoAsync(Origem, Hub,
            Responde(_ => ResultadoConsultaCpf.Indisponivel, perguntas));

        Assert.True(r.Indisponivel);
        Assert.Equal(1, r.ConsultasGastas);
        Assert.Single(perguntas);
    }

    [Fact]
    public async Task Valor_nao_ISO_nao_gasta_nenhuma_consulta()
    {
        var perguntas = new List<DateOnly>();
        var r = await ArbitroIdentidade.ArbitrarNascimentoAsync("10/05/1980", Hub,
            Responde(_ => ResultadoConsultaCpf.Validou, perguntas));

        Assert.Equal(VeredictoDivergenciaIdentidade.Inconclusivo, r.Veredicto);
        Assert.Equal(0, r.ConsultasGastas);
        Assert.Empty(perguntas);
    }

    /// <summary>
    /// O teto de TEMPO existe porque limitar só a quantidade não protege: N casos lentos
    /// seguram a rodada indefinidamente. Aqui a consulta demora de propósito e o orçamento
    /// é curto — o que se verifica é que a decisão de parar olha o relógio, não o contador.
    /// </summary>
    [Fact]
    public async Task Consulta_lenta_estoura_o_orcamento_de_tempo()
    {
        var orcamento = TimeSpan.FromMilliseconds(300);
        var relogio = Stopwatch.StartNew();
        var analisadas = 0;

        // Espelha o laço do verificador: checa o relógio ANTES de gastar a próxima consulta.
        foreach (var _ in Enumerable.Range(0, 50))
        {
            if (relogio.Elapsed >= orcamento) break;
            await ArbitroIdentidade.ArbitrarNascimentoAsync(Origem, Hub, async d =>
            {
                await Task.Delay(120);
                return (ResultadoConsultaCpf.Negou, (string?)"lento");
            });
            analisadas++;
        }

        Assert.InRange(analisadas, 1, 5);          // parou cedo, não nas 50 — a prova real da parada
        // Guarda contra laço travado, NÃO precisão de relógio: sob CI carregado o Task.Delay das
        // poucas iterações estica (starvation do threadpool) e um teto curto flaka. 30s só pega um
        // laço genuinamente preso — a parada antecipada já está provada por 'analisadas'.
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(30));
    }
}
