namespace SMSMais.Core.Integracoes.SisregWeb.Fila.Background;

/// <summary>Uma janela de data de solicitação — o que custa 2 requisições no SISREG.</summary>
public sealed record JanelaFila(DateOnly Inicio, DateOnly Fim);

/// <summary>O que a tela mostra sobre a leitura da fila.</summary>
/// <param name="Completa">A leitura em curso é a do acervo inteiro (e não só os últimos 31 dias).</param>
/// <param name="UltimoErro">Só quando a leitura <b>desistiu</b> (CAPTCHA ou falhas seguidas). Falha
/// que a nova tentativa resolveu não é erro — em 12/09/2026 a tela mostrava "leitura interrompida"
/// para uma janela relida com sucesso 22 s depois, e parecia que a carga tinha falhado.</param>
/// <param name="RelidasAposFalha">Janelas que falharam (sessão caída, conexão cortada) e foram
/// relidas com sucesso. Informação, não alarme.</param>
/// <param name="PessoasNaFila">Quem está na fila segundo a última leitura — do banco.</param>
/// <param name="UltimaLeitura">Nulo = a fila nunca foi lida, e a tela tem de dizer isso em vez de
/// mostrar "ninguém esperando".</param>
public sealed record FilaCargaStatusDto(
    bool EmExecucao,
    bool Completa,
    int JanelasTotal,
    int JanelasLidas,
    DateOnly? JanelaAtualInicio,
    DateOnly? JanelaAtualFim,
    DateTime? IniciadoEm,
    int PessoasLidas,
    string? UltimoErro,
    DateTime? UltimoErroEm,
    int PessoasNaFila,
    DateTime? UltimaLeitura,
    int RelidasAposFalha);

/// <summary>Como fatiar o tempo em janelas que o SISREG aceita.</summary>
public static class JanelasDaFila
{
    /// <summary>
    /// De onde a carga completa parte. Era jan/2024, com base numa medição de laboratório ("jul/2024
    /// = 0") que a carga completa de 12/09/2026 desmentiu: todo mês de 2024 tem gente esperando. Na
    /// tela do regulador, no mesmo dia: 2023 = 9 pessoas (8 reenviadas), 2022 = 2021 = 2020 = 0.
    /// Antes de 2023 é requisição gasta procurando ninguém; 2023 são poucas, mas são as que esperam
    /// há mais tempo.
    /// </summary>
    public static readonly DateOnly InicioDoAcervo = new(2023, 1, 1);

    public static IReadOnlyList<JanelaFila> Recente(DateOnly hoje) =>
        [new(hoje.AddDays(-(FilaPendenteSisregService.MaxDiasPorJanela - 1)), hoje)];

    /// <summary>
    /// Do mais recente para o mais antigo: quem pediu há pouco é a maior parte da fila, e a tela
    /// fica útil já nas primeiras janelas em vez de esperar a carga inteira.
    /// </summary>
    public static IReadOnlyList<JanelaFila> Completa(DateOnly hoje, DateOnly desde)
    {
        var janelas = new List<JanelaFila>();
        var fim = hoje;
        while (fim >= desde)
        {
            var inicio = fim.AddDays(-(FilaPendenteSisregService.MaxDiasPorJanela - 1));
            if (inicio < desde) inicio = desde;
            janelas.Add(new JanelaFila(inicio, fim));
            fim = inicio.AddDays(-1);
        }
        return janelas;
    }
}

/// <summary>
/// Janelas a ler e o progresso — em memória, como o estado vivo dos outros motores do SISREG.
///
/// <para>Reiniciar o servidor no meio de uma carga perde as janelas que faltavam, e isso é
/// aceitável: a leitura é idempotente (upsert por código), e a releitura do dia seguinte cobre.</para>
/// </summary>
public sealed class FilaPendenteEstadoVivo
{
    /// <summary>Falhas seguidas (rede, SISREG fora) antes de desistir da carga.</summary>
    private const int MaximoDeFalhasSeguidas = 3;

    private readonly Lock _trava = new();
    private readonly LinkedList<JanelaFila> _pendentes = new();
    private JanelaFila? _atual;
    private bool _completa;
    private int _total;
    private int _lidas;
    private int _pessoas;
    private int _falhasSeguidas;
    private int _relidasAposFalha;
    private DateTime? _iniciadoEm;
    private string? _ultimoErro;
    private DateTime? _ultimoErroEm;

    public bool EmExecucao
    {
        get { lock (_trava) { return _atual is not null || _pendentes.Count > 0; } }
    }

    /// <summary>Falso quando já há leitura em curso — duas cargas ao mesmo tempo só gastariam
    /// requisição relendo as mesmas janelas.</summary>
    public bool Enfileirar(IReadOnlyList<JanelaFila> janelas, bool completa)
    {
        lock (_trava)
        {
            if (_atual is not null || _pendentes.Count > 0) return false;
            foreach (var j in janelas) _pendentes.AddLast(j);
            _completa = completa;
            _total = janelas.Count;
            _lidas = 0;
            _pessoas = 0;
            _falhasSeguidas = 0;
            _relidasAposFalha = 0;
            _iniciadoEm = DateTime.UtcNow;
            _ultimoErro = null;
            _ultimoErroEm = null;
            return true;
        }
    }

    /// <summary>Pega a próxima janela. Nulo quando não há nada ou já há uma em leitura.</summary>
    public JanelaFila? Proxima()
    {
        lock (_trava)
        {
            if (_atual is not null || _pendentes.First is not { } primeiro) return null;
            _pendentes.RemoveFirst();
            _atual = primeiro.Value;
            return _atual;
        }
    }

    public void Concluir(LeituraFilaDto leitura)
    {
        lock (_trava)
        {
            _atual = null;
            _lidas++;
            _pessoas += leitura.Lidas;
            if (_falhasSeguidas > 0) _relidasAposFalha++;
            _falhasSeguidas = 0;
        }
    }

    /// <summary>Devolve a janela para a frente, sem contar como falha (orçamento curto).</summary>
    public void Adiar()
    {
        lock (_trava)
        {
            if (_atual is { } j) _pendentes.AddFirst(j);
            _atual = null;
        }
    }

    /// <param name="desistir">CAPTCHA: continuar seria insistir num bloqueio de 24 horas.</param>
    public void Falhar(string erro, bool desistir)
    {
        lock (_trava)
        {
            var janela = _atual;
            _atual = null;
            _falhasSeguidas++;

            if (desistir || _falhasSeguidas >= MaximoDeFalhasSeguidas)
            {
                // Só aqui vira erro para a tela: a leitura parou e alguém precisa saber.
                _pendentes.Clear();
                _ultimoErro = erro;
                _ultimoErroEm = DateTime.UtcNow;
            }
            else if (janela is not null)
            {
                _pendentes.AddFirst(janela);
            }
        }
    }

    public FilaCargaStatusDto Snapshot(ResumoFilaDto resumo)
    {
        lock (_trava)
        {
            return new FilaCargaStatusDto(
                _atual is not null || _pendentes.Count > 0,
                _completa,
                _total,
                _lidas,
                _atual?.Inicio,
                _atual?.Fim,
                _iniciadoEm,
                _pessoas,
                _ultimoErro,
                _ultimoErroEm,
                resumo.PessoasNaFila,
                resumo.UltimaLeitura,
                _relidasAposFalha);
        }
    }
}
