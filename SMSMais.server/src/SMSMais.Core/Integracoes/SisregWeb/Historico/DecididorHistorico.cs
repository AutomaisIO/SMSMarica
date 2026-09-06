using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Historico;

/// <summary>O que o motor faz com a fatia corrente.</summary>
public enum PassoHistorico
{
    /// <summary>A fatia ainda não foi pedida ao SISREG.</summary>
    Pedir,

    /// <summary>Pedida e rodando — espera o próximo tick.</summary>
    Esperar,

    /// <summary>Terminou mal (erro, CAPTCHA, cancelamento). <b>Não avança a cobertura</b>: repete.</summary>
    Repetir,

    /// <summary>Terminou bem e ainda há passado a buscar.</summary>
    Avancar,

    /// <summary>Terminou bem e a unidade chegou ao começo — encerra o histórico dela.</summary>
    Concluir,
}

/// <summary>
/// Decisão do motor de histórico, isolada do banco e do relógio para poder ser testada.
///
/// <para>Mesma escolha do <c>DecididorVarreduraSisreg</c>: o que decide fica em código puro; o
/// scheduler só lê estado, chama isto e aplica. Sem essa separação, as regras de "quando parar" e
/// "quando NÃO avançar" só seriam exercitadas com um SISREG de verdade do outro lado.</para>
/// </summary>
public static class DecididorHistorico
{
    /// <summary>
    /// A próxima fatia a buscar: sempre termina um dia antes do que já está coberto.
    ///
    /// <para>Sem cobertura, começa colada em <paramref name="hoje"/> — a varredura diária cuida do
    /// futuro e este motor só anda para trás. Emendar em <c>hoje</c> em vez de em <c>hoje - N</c>
    /// evita deixar um vão entre o que a diária traz e onde o histórico começa.</para>
    /// </summary>
    public static (DateOnly Inicio, DateOnly Fim) ProximaFatia(
        DateOnly? cobertoDe, DateOnly hoje, int diasPorFatia)
    {
        var dias = Math.Max(1, diasPorFatia);
        var fim = (cobertoDe ?? hoje).AddDays(-1);
        return (fim.AddDays(-(dias - 1)), fim);
    }

    /// <summary>
    /// O que fazer diante do estado da fatia. <paramref name="execucaoStatus"/> nulo = a fatia
    /// ainda não foi pedida.
    /// </summary>
    /// <param name="fatiasVaziasAtuais">Quantas fatias seguidas já vieram vazias antes desta.</param>
    /// <param name="registrosEncontrados">Registros que a fatia trouxe (só vale se concluída).</param>
    /// <param name="fatiasParaConcluir">Quantas vazias seguidas encerram a unidade.</param>
    /// <param name="nenhumTrabalhoVivo">
    /// O chamador confirmou que <b>nada</b> está rodando neste instante. Nesse caso uma linha em
    /// <c>Pendente</c>/<c>EmExecucao</c> só pode ser <b>órfã</b>, e a fatia é repetida.
    ///
    /// <para>Isto existe porque o estado "rodando" vive em memória e o banco não: um deploy no meio
    /// de uma fatia reinicia o processo e deixa a execução eternamente em <c>EmExecucao</c>.
    /// Aconteceu em 06/09/2026 no CDT — 21 requisições, 4.272 registros lidos e nenhum
    /// <c>finalizado_em</c> —, e sem esta saída o motor esperaria para sempre por uma fatia que
    /// nunca mais ia terminar.</para>
    ///
    /// <para>Não é heurística de tempo: os dois chamadores já verificam os <c>EstadoVivo</c> antes
    /// de reconciliar, então "nada vivo + banco diz rodando" é conclusão, não palpite. O custo de
    /// errar seria repetir uma fatia, uma requisição — contra travar o motor indefinidamente.</para>
    /// </param>
    public static PassoHistorico Decidir(
        StatusVarredura? execucaoStatus,
        int registrosEncontrados,
        int fatiasVaziasAtuais,
        int fatiasParaConcluir,
        bool nenhumTrabalhoVivo = false)
    {
        if (execucaoStatus is null) return PassoHistorico.Pedir;

        if (execucaoStatus is StatusVarredura.Pendente or StatusVarredura.EmExecucao)
            return nenhumTrabalhoVivo ? PassoHistorico.Repetir : PassoHistorico.Esperar;

        // Avançar a cobertura sobre uma fatia que falhou deixaria um buraco silencioso: a tela diria
        // "coberto desde X" e o dado não estaria lá. Repetir custa uma requisição; o buraco custa a
        // análise inteira, e ninguém teria como saber que ele existe.
        if (execucaoStatus != StatusVarredura.Concluida) return PassoHistorico.Repetir;

        if (registrosEncontrados > 0) return PassoHistorico.Avancar;

        return fatiasVaziasAtuais + 1 >= Math.Max(1, fatiasParaConcluir)
            ? PassoHistorico.Concluir
            : PassoHistorico.Avancar;
    }

    /// <summary>Contagem de vazias depois desta fatia: zera assim que aparece registro.</summary>
    public static int ProximasVazias(int atuais, int registrosEncontrados) =>
        registrosEncontrados > 0 ? 0 : atuais + 1;
}
