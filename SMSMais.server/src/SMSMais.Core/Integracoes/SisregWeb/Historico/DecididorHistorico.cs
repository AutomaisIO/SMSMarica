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

    /// <summary>
    /// A mesma fatia falhou vezes demais. <b>Desliga o histórico da unidade</b> em vez de seguir
    /// queimando orçamento numa janela que não passa.
    /// </summary>
    Bloquear,
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
    /// <param name="idadeDaExecucao">
    /// Há quanto tempo a execução começou. É o que distingue "rodando" de "abandonada por um
    /// restart" — e a distinção precisa ser por IDADE, não por "tem algo vivo agora".
    ///
    /// <para>A versão por liveness causou um laço em 06/09/2026: a linha é criada antes de o runner
    /// registrar-se como vivo, então o tick seguinte lia a execução recém-criada como órfã,
    /// disparava outra, e a nova matava a anterior em <c>FecharOrfasAsync</c>. 57 requisições numa
    /// hora, sempre a mesma fatia, nada avançando.</para>
    /// </param>
    /// <param name="tentativasNaFatia">Execuções que já falharam nesta mesma janela.</param>
    /// <param name="opcoes">Limites de idade, espera entre tentativas e teto de tentativas.</param>
    public static PassoHistorico Decidir(
        StatusVarredura? execucaoStatus,
        int registrosEncontrados,
        int fatiasVaziasAtuais,
        int fatiasParaConcluir,
        TimeSpan? idadeDaExecucao = null,
        int tentativasNaFatia = 0,
        HistoricoOpcoes? opcoes = null)
    {
        if (execucaoStatus is null) return PassoHistorico.Pedir;

        var o = opcoes ?? new HistoricoOpcoes();
        var idade = idadeDaExecucao ?? TimeSpan.Zero;

        if (execucaoStatus is StatusVarredura.Pendente or StatusVarredura.EmExecucao)
        {
            // Nova o bastante para estar mesmo rodando: esperar é o certo, e é o que impede o laço.
            return idade < TimeSpan.FromMinutes(o.MinutosParaAbandonada)
                ? PassoHistorico.Esperar
                : PassoHistorico.Repetir;
        }

        if (execucaoStatus != StatusVarredura.Concluida)
        {
            // Teto duro: a mesma janela não pode falhar para sempre consumindo orçamento.
            if (tentativasNaFatia >= Math.Max(1, o.TentativasPorFatia)) return PassoHistorico.Bloquear;

            // Recuo entre tentativas: sem isto um erro determinístico repete a cada tick.
            return idade < TimeSpan.FromMinutes(o.MinutosEntreTentativas)
                ? PassoHistorico.Esperar
                : PassoHistorico.Repetir;
        }

        if (registrosEncontrados > 0) return PassoHistorico.Avancar;

        return fatiasVaziasAtuais + 1 >= Math.Max(1, fatiasParaConcluir)
            ? PassoHistorico.Concluir
            : PassoHistorico.Avancar;
    }

    /// <summary>Contagem de vazias depois desta fatia: zera assim que aparece registro.</summary>
    public static int ProximasVazias(int atuais, int registrosEncontrados) =>
        registrosEncontrados > 0 ? 0 : atuais + 1;
}
