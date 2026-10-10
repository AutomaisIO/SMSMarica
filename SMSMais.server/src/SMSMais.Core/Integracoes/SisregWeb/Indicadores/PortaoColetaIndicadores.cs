using System.Globalization;

namespace SMSMais.Core.Integracoes.SisregWeb.Indicadores;

/// <summary>Por que o coletor não tocou neste tick (para o log e para a tela).</summary>
public enum EsperaColetaIndicadores
{
    Desligada = 1,
    ChaveMestraDesligada = 2,
    PausadaPorCaptcha = 3,
    ForaDoHorario = 4,
    OutroMotorUsandoASessao = 5,
    TetoDoColetor = 6,
    OrcamentoGlobalCurto = 7,

    /// <summary>O SISREG recusou o login há pouco: espera uns minutos antes de tentar de novo.</summary>
    LoginRecusado = 8,
}

/// <summary>O que o portão enxerga num tick.</summary>
/// <param name="HoraLocal">Hora de Brasília.</param>
/// <param name="GastasPeloColetorNaUltimaHora">Requisições do próprio coletor nos últimos 60 minutos.</param>
/// <param name="RestanteGlobal">Folga do orçamento de TODOS os motores (<see cref="SisregOrcamentoRequisicoes"/>).</param>
/// <param name="LoginRecusadoAteUtc">Fim da espera depois de um login recusado (memória do agendador).</param>
public sealed record EntradaPortaoColeta(
    bool Ativa,
    bool ChaveMestraLigada,
    DateTime? PausadaAteUtc,
    DateTime AgoraUtc,
    TimeOnly HoraLocal,
    bool OutroMotorVivo,
    int GastasPeloColetorNaUltimaHora,
    int RestanteGlobal,
    DateTime? LoginRecusadoAteUtc = null);

/// <summary>
/// Decide se o coletor pode dar UM passo agora. Puro (sem relógio nem banco) — é o que dá para testar
/// com relógio falso, e é onde moram as regras que protegem o operador do CAPTCHA:
/// <list type="bullet">
///   <item>nasce desligado, e a chave-mestra do sincronismo automático vale por cima;</item>
///   <item>CAPTCHA pausa por um dia inteiro — relogar não resolve, só um humano no navegador;</item>
///   <item>login recusado espera alguns minutos — insistir a cada tick não ajuda e, se for a senha, piora;</item>
///   <item>fora da faixa da varredura das agendas (18:00–01:20);</item>
///   <item>cede a vez a qualquer outro motor que esteja usando a sessão (a sessão é única por operador);</item>
///   <item>teto próprio por hora E folga larga no orçamento global.</item>
/// </list>
/// </summary>
public static class PortaoColetaIndicadores
{
    public static EsperaColetaIndicadores? Decidir(EntradaPortaoColeta e, ColetaIndicadoresOpcoes opcoes)
    {
        if (!e.Ativa) return EsperaColetaIndicadores.Desligada;
        if (!e.ChaveMestraLigada) return EsperaColetaIndicadores.ChaveMestraDesligada;
        if (e.PausadaAteUtc is { } pausa && e.AgoraUtc < pausa) return EsperaColetaIndicadores.PausadaPorCaptcha;
        if (e.LoginRecusadoAteUtc is { } login && e.AgoraUtc < login) return EsperaColetaIndicadores.LoginRecusado;
        if (!DentroDoHorario(e.HoraLocal, opcoes)) return EsperaColetaIndicadores.ForaDoHorario;
        if (e.OutroMotorVivo) return EsperaColetaIndicadores.OutroMotorUsandoASessao;
        if (e.GastasPeloColetorNaUltimaHora >= opcoes.TetoPorHora) return EsperaColetaIndicadores.TetoDoColetor;
        if (e.RestanteGlobal < opcoes.OrcamentoMinimo) return EsperaColetaIndicadores.OrcamentoGlobalCurto;
        return null;
    }

    /// <summary>[início, fim) em Brasília. Faixa que atravessa a meia-noite também é aceita.</summary>
    public static bool DentroDoHorario(TimeOnly agora, ColetaIndicadoresOpcoes opcoes)
    {
        var ini = Hora(opcoes.HoraInicio, new TimeOnly(1, 20));
        var fim = Hora(opcoes.HoraFim, new TimeOnly(18, 0));
        return ini <= fim ? agora >= ini && agora < fim : agora >= ini || agora < fim;
    }

    private static TimeOnly Hora(string? texto, TimeOnly padrao) =>
        TimeOnly.TryParseExact(texto?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : padrao;
}

/// <summary>Requisições do próprio coletor numa janela rolante de 60 minutos (memória, como o orçamento global).</summary>
public sealed class ContadorRequisicoesColeta
{
    private static readonly TimeSpan Janela = TimeSpan.FromHours(1);
    private readonly Lock _trava = new();
    private readonly Queue<DateTime> _carimbos = new();

    public void Registrar(DateTime agoraUtc)
    {
        lock (_trava)
        {
            Expirar(agoraUtc);
            _carimbos.Enqueue(agoraUtc);
        }
    }

    public int NaUltimaHora(DateTime agoraUtc)
    {
        lock (_trava)
        {
            Expirar(agoraUtc);
            return _carimbos.Count;
        }
    }

    private void Expirar(DateTime agora)
    {
        while (_carimbos.TryPeek(out var c) && c <= agora - Janela) _carimbos.Dequeue();
    }
}

/// <summary>
/// "Janela lida substitui a anterior" — só com prova. Quem grava troca TODAS as linhas de uma janela
/// (faltas de uma semana, cotas de uma competência); errar aqui apaga dado bom com leitura ruim.
/// </summary>
public static class SubstituicaoDeJanela
{
    /// <param name="anteriores">Linhas que a janela tem hoje no banco.</param>
    /// <param name="novas">Linhas lidas agora.</param>
    /// <param name="provaDeLeituraCompleta">A tela provou que a leitura é inteira (total declarado, "nenhum registro" etc.).</param>
    /// <returns><c>null</c> = pode substituir; senão, o motivo da recusa.</returns>
    public static string? Recusar(int anteriores, int novas, bool provaDeLeituraCompleta, double fracaoMinima)
    {
        if (!provaDeLeituraCompleta)
            return "a leitura não provou que veio inteira — nada foi substituído";
        if (novas == 0 && anteriores > 0)
            return $"a leitura voltou vazia e a janela tinha {anteriores} linha(s) — nada foi apagado";
        if (anteriores >= 20 && novas < anteriores * fracaoMinima)
            return $"a leitura encolheu demais ({novas} contra {anteriores} gravadas) — nada foi substituído";
        return null;
    }
}
