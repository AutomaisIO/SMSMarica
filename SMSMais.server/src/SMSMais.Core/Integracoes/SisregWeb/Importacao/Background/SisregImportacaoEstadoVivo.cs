namespace SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;

/// <summary>Progresso mutável de um lote — o runner incrementa, o endpoint tira snapshot sob lock.</summary>
public sealed class ProgressoLote
{
    public required Guid LoteId { get; init; }
    public required int TotalArquivos { get; init; }
    public int ArquivosFeitos;
    public string? ArquivoAtual;
    public int Validos;
    public int Invalidos;
}

/// <summary>Snapshot imutável devolvido pelo GET de status.</summary>
public sealed record StatusLote(
    Guid LoteId,
    bool EmExecucao,
    int TotalArquivos,
    int ArquivosFeitos,
    string? ArquivoAtual,
    int Validos,
    int Invalidos);

/// <summary>
/// Verdade do "está rodando" — memória, não banco (o banco não sabe se o processo morreu).
/// Singleton. Guarda a referência do progresso e o CTS do cancelamento pelo operador.
/// </summary>
public sealed class SisregImportacaoEstadoVivo
{
    private readonly Lock _trava = new();
    private ProgressoLote? _progresso;
    private CancellationTokenSource? _cts;

    public void Iniciar(ProgressoLote progresso, CancellationTokenSource cts)
    {
        lock (_trava)
        {
            _progresso = progresso;
            _cts = cts;
        }
    }

    public void Finalizar()
    {
        lock (_trava)
        {
            _progresso = null;
            _cts = null;
        }
    }

    /// <summary>Parada pedida pelo operador (≠ app caindo) — ver o catch discriminado no serviço.</summary>
    public bool Cancelar()
    {
        lock (_trava)
        {
            if (_cts is null) return false;
            try { _cts.Cancel(); return true; }
            catch (ObjectDisposedException) { return false; }
        }
    }

    public StatusLote? ObterAtual()
    {
        lock (_trava)
        {
            if (_progresso is not { } p) return null;
            return new StatusLote(p.LoteId, true, p.TotalArquivos, p.ArquivosFeitos, p.ArquivoAtual, p.Validos, p.Invalidos);
        }
    }
}
