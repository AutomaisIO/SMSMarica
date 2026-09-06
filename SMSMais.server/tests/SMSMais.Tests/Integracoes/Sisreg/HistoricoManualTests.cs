using SMSMais.Core.Integracoes.SisregWeb.Historico;

namespace SMSMais.Tests.Integracoes.Sisreg;

/// <summary>
/// A regra que o motor de histórico quebrou em produção: <b>o que uma pessoa manda fazer não pode
/// ser calado pela chave que pausa a agenda automática.</b>
///
/// <para><b>O incidente (05/09/2026).</b> O botão "Importar o passado" gravava
/// <c>historico_ativo = true</c> e voltava; quem executava era o <c>HistoricoAgendaScheduler</c>,
/// que — sendo automático, e corretamente — para quando o sincronismo automático está desligado.
/// Como a chave-mestra nasceu desligada, o motor nunca rodou <b>uma única fatia</b>: 0 de 45
/// unidades com cobertura, 0 execuções com janela no passado desde a implantação. O operador
/// clicou várias vezes, não viu progresso nenhum e desistiu — e não havia como ele saber por quê,
/// porque todos os freios devolviam silêncio.</para>
/// </summary>
public class HistoricoManualTests
{
    /// <summary>
    /// O disparo manual e o automático precisam recortar a MESMA fatia.
    ///
    /// <para>O caminho manual não tem <c>IOptions</c> em mãos e usa a constante; o scheduler usa a
    /// opção configurável. Se os dois divergirem, alternar entre "Avançar agora" e o motor
    /// automático deixa <b>vão</b> (dias que ninguém buscou) ou <b>sobreposição</b> (requisição
    /// gasta duas vezes) na cobertura — e o vão é invisível, porque a tela continua dizendo
    /// "coberto desde X".</para>
    /// </summary>
    [Fact]
    public void Manual_e_automatico_recortam_a_mesma_fatia()
    {
        var padraoDasOpcoes = new HistoricoOpcoes().DiasPorFatia;

        Assert.Equal(HistoricoOpcoes.DiasPorFatiaPadrao, padraoDasOpcoes);

        var hoje = new DateOnly(2026, 9, 5);
        var comConstante = DecididorHistorico.ProximaFatia(null, hoje, HistoricoOpcoes.DiasPorFatiaPadrao);
        var comOpcao = DecididorHistorico.ProximaFatia(null, hoje, padraoDasOpcoes);

        Assert.Equal(comConstante, comOpcao);
    }

    /// <summary>
    /// A fatia manual emenda exatamente onde a cobertura parou — sem vão e sem repetir dia.
    ///
    /// <para>É o que permite alternar livremente entre o botão e o motor automático: os dois leem
    /// <c>HistoricoCobertoDe</c> e continuam dali.</para>
    /// </summary>
    [Fact]
    public void Fatia_seguinte_emenda_na_cobertura_sem_vao()
    {
        var hoje = new DateOnly(2026, 9, 5);

        var primeira = DecididorHistorico.ProximaFatia(null, hoje, 31);
        Assert.Equal(new DateOnly(2026, 9, 4), primeira.Fim);

        // A cobertura passa a ser o INÍCIO da fatia que acabou de rodar.
        var segunda = DecididorHistorico.ProximaFatia(primeira.Inicio, hoje, 31);

        Assert.Equal(primeira.Inicio.AddDays(-1), segunda.Fim);
        Assert.Equal(31, segunda.Fim.DayNumber - segunda.Inicio.DayNumber + 1);
    }

    /// <summary>
    /// <b>A chave-mestra só pode ser consultada de dentro de um Scheduler.</b>
    ///
    /// <para>Ela pausa a <i>agenda</i> de sincronismo — o disparo automático —, não o comando de
    /// uma pessoa. Este teste varre o código-fonte porque o defeito não é de valor, é de
    /// <b>lugar</b>: a mesma chamada está certa num scheduler e errada num método que responde a
    /// um clique. Se alguém puser o gate num serviço manual, o botão volta a mentir do mesmo jeito
    /// — e nenhum teste de comportamento pegaria isso sem montar as quinze dependências do
    /// <c>VarreduraAgendaService</c>.</para>
    /// </summary>
    [Fact]
    public void Chave_mestra_so_e_consultada_por_schedulers()
    {
        var core = RaizDoCore();
        var infratores = Directory
            .EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("SincronismoAutomaticoSisreg.LigadoAsync"))
            .Select(Path.GetFileName)
            .Where(nome => !nome!.EndsWith("Scheduler.cs", StringComparison.Ordinal)
                        && !nome.Equals("SincronismoAutomaticoSisreg.cs", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            infratores.Count == 0,
            "A chave-mestra pausa a agenda AUTOMÁTICA. Estes arquivos não são schedulers e não "
            + "podem consultá-la — se um deles atende a um clique, o operador manda executar e "
            + $"nada acontece: {string.Join(", ", infratores)}");
    }

    /// <summary>Sobe até achar <c>src/SMSMais.Core</c>: o teste roda de <c>bin/Debug/netX</c>.</summary>
    private static string RaizDoCore()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var alvo = Path.Combine(dir.FullName, "src", "SMSMais.Core");
            if (Directory.Exists(alvo)) return alvo;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Não achei src/SMSMais.Core a partir de " + AppContext.BaseDirectory);
    }
}
