namespace SMSMais.Core.Integracoes.SisregWeb;

/// <summary>
/// Parâmetros do orçamento anti-robô do SISREG (seção <c>Sisreg:Orcamento</c> do appsettings).
/// </summary>
public sealed class SisregOrcamentoOpcoes
{
    public const string Secao = "Sisreg:Orcamento";

    /// <summary>
    /// Teto de requisições ao SISREG numa janela de 60 minutos, somando <b>todos</b> os motores.
    /// O CAPTCHA foi observado por volta de 700 por operador (ver
    /// <c>Automais.SISREG/docs/APRENDIZADOS.md</c>); 500 deixa folga.
    /// </summary>
    public int TetoPorHora { get; set; } = 500;

    /// <summary>
    /// Fatia do teto que os motores automáticos <b>não</b> encostam, guardada para o que o humano
    /// dispara na tela (botão "Importar" de um procedimento, teste de credencial, varredura manual).
    /// Sem essa reserva, o lote noturno consumiria o orçamento inteiro e o operador chegaria de
    /// manhã com o SISREG bloqueado sem nunca ter clicado em nada.
    /// </summary>
    public int ReservaOperador { get; set; } = 100;

    /// <summary>Teto efetivo dos motores automáticos.</summary>
    public int TetoAutomatico => Math.Max(1, TetoPorHora - ReservaOperador);
}

/// <summary>
/// Contador rolante de requisições ao SISREG — <b>uma verdade só</b> para todos os motores.
///
/// <para><b>Por que precisa ser global:</b> mapeamento, varredura de agenda, importação por
/// arquivo e a consulta de cadastro saem pelo mesmo operador e pelo mesmo IP (túnel WireGuard,
/// <c>docs/sisreg-egress.md</c>) e dividem o mesmo orçamento anti-robô. Cada motor tinha o seu
/// próprio teto e nenhum enxergava o gasto do outro: dava para o lote do mapeamento começar logo
/// depois de uma varredura que já tinha queimado 400 requisições, e o CAPTCHA aparecia sem que
/// nenhum dos dois tivesse "estourado" o seu limite. Um teto por motor, sozinho, é ficção.</para>
///
/// <para><b>É memória, não banco</b>, e de propósito: a janela é de 60 minutos e reiniciar o
/// serviço é raro. O custo do erro é subestimar o gasto logo após um restart — aceitável perto de
/// gravar uma linha por requisição. A instrumentação fica em <see cref="ISisregWebSessao"/>, que é
/// por onde <b>toda</b> ida ao SISREG passa; nenhum motor precisa lembrar de contar.</para>
/// </summary>
public sealed class SisregOrcamentoRequisicoes
{
    private static readonly TimeSpan Janela = TimeSpan.FromHours(1);

    /// <summary>Guarda contra vazamento se algo disparar requisições muito acima do teto.</summary>
    private const int MaxRegistros = 20_000;

    private readonly Lock _trava = new();
    private readonly Queue<DateTime> _carimbos = new();

    /// <summary>Registra uma ida ao SISREG. Chamado pela sessão, não pelos motores.</summary>
    public void Registrar()
    {
        lock (_trava)
        {
            var agora = DateTime.UtcNow;
            Expirar(agora);
            if (_carimbos.Count < MaxRegistros) _carimbos.Enqueue(agora);
        }
    }

    /// <summary>Requisições feitas nos últimos 60 minutos.</summary>
    public int GastasNaUltimaHora()
    {
        lock (_trava)
        {
            Expirar(DateTime.UtcNow);
            return _carimbos.Count;
        }
    }

    /// <summary>Quanto ainda cabe antes de encostar em <paramref name="teto"/>. Nunca negativo.</summary>
    public int Restante(int teto) => Math.Max(0, teto - GastasNaUltimaHora());

    /// <summary>
    /// Quando a requisição mais antiga da janela sai dela — ou seja, daqui a quanto tempo o
    /// orçamento volta a abrir. <c>null</c> quando não há nada na janela.
    /// </summary>
    public TimeSpan? EsperaAteLiberar()
    {
        lock (_trava)
        {
            var agora = DateTime.UtcNow;
            Expirar(agora);
            if (!_carimbos.TryPeek(out var maisAntigo)) return null;
            var espera = maisAntigo + Janela - agora;
            return espera > TimeSpan.Zero ? espera : TimeSpan.Zero;
        }
    }

    private void Expirar(DateTime agora)
    {
        var corte = agora - Janela;
        while (_carimbos.TryPeek(out var c) && c < corte) _carimbos.Dequeue();
    }
}
