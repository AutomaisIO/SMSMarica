namespace SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;

/// <summary>Progresso mutável de uma resolução em lote — o runner incrementa, o GET tira snapshot.</summary>
public sealed class ProgressoResolucao
{
    public required Guid ExecucaoId { get; init; }
    public required int Total { get; init; }
    public required DateTime IniciadoEm { get; init; }
    public int Feitas;
    public int Resolvidas;
    public int Continuam;
}

/// <summary>
/// Snapshot devolvido pelo GET de status. <c>EmExecucao=false</c> com <c>ConcluidoEm</c>
/// preenchido é o resumo da última rodada.
/// </summary>
public sealed record StatusResolucaoPendencias(
    Guid ExecucaoId,
    bool EmExecucao,
    int Total,
    int Feitas,
    int Resolvidas,
    int Continuam,
    DateTime IniciadoEm,
    DateTime? ConcluidoEm,
    bool Cancelado,
    string? Mensagem);

/// <summary>
/// Verdade do "está resolvendo" — memória, não banco, pelo mesmo motivo do lote de arquivos: o
/// banco não sabe se o processo morreu.
///
/// <para><b>O resumo da última rodada também é memória, e some no restart.</b> Guardá-lo em tabela
/// exigiria migration para uma informação que só serve enquanto o operador tem a tela aberta —
/// o resultado durável do lote são as próprias pendências saindo da lista. Depois de um restart o
/// status volta nulo e a tela mostra a lista, que é a fonte de verdade.</para>
/// </summary>
public sealed class ResolucaoPendenciasEstadoVivo
{
    private readonly Lock _trava = new();
    private ProgressoResolucao? _progresso;
    private CancellationTokenSource? _cts;
    private StatusResolucaoPendencias? _ultimo;

    public void Iniciar(ProgressoResolucao progresso, CancellationTokenSource cts)
    {
        lock (_trava)
        {
            _progresso = progresso;
            _cts = cts;
        }
    }

    public void Finalizar(bool cancelado, string mensagem)
    {
        lock (_trava)
        {
            if (_progresso is { } p)
            {
                _ultimo = new StatusResolucaoPendencias(
                    p.ExecucaoId, false, p.Total, p.Feitas, p.Resolvidas, p.Continuam,
                    p.IniciadoEm, DateTime.UtcNow, cancelado, mensagem);
            }
            _progresso = null;
            _cts = null;
        }
    }

    /// <summary>Parada pedida pelo operador. False = não havia nada rodando.</summary>
    public bool Cancelar()
    {
        lock (_trava)
        {
            if (_cts is null) return false;
            try { _cts.Cancel(); return true; }
            catch (ObjectDisposedException) { return false; }
        }
    }

    public bool EmExecucao
    {
        get { lock (_trava) { return _progresso is not null; } }
    }

    public StatusResolucaoPendencias? ObterAtual()
    {
        lock (_trava)
        {
            if (_progresso is not { } p) return _ultimo;
            return new StatusResolucaoPendencias(
                p.ExecucaoId, true, p.Total, p.Feitas, p.Resolvidas, p.Continuam,
                p.IniciadoEm, null, false, null);
        }
    }
}
