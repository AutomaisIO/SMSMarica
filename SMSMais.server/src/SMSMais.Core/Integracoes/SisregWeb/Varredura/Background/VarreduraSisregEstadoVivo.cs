namespace SMSMais.Core.Integracoes.SisregWeb.Varredura.Background;

/// <summary>Progresso mutável de uma varredura — o serviço incrementa, o endpoint tira snapshot.</summary>
public sealed class ProgressoVarredura
{
    public required Guid ExecucaoId { get; init; }
    public required Guid UnidadeId { get; init; }
    public required string UnidadeNome { get; init; }
    public required int CombinacoesTotal { get; init; }

    /// <summary>Quando esta corrida começou. Vai para a tela porque um contador parado sem
    /// relógio ao lado não diz se é lentidão ou morte — foram 46 minutos de dúvida em
    /// 08/09/2026 justamente por falta deste dado.</summary>
    public DateTime IniciadoEm { get; init; } = DateTime.UtcNow;
    public int CombinacoesFeitas;
    public int Requisicoes;
    public int RegistrosEncontrados;
    public int Validos;
    public int Invalidos;
    public string? ProfissionalAtual;
    public string? ProcedimentoAtual;

    /// <summary>Chaves do par em curso — é o que vira cursor de retomada quando a varredura para.</summary>
    public string? CpfAtual;
    public string? CodigoAtual;
}

/// <summary>Snapshot imutável devolvido pelo GET de status.</summary>
public sealed record StatusVarreduraVivo(
    Guid ExecucaoId,
    Guid UnidadeId,
    string UnidadeNome,
    bool EmExecucao,
    int CombinacoesTotal,
    int CombinacoesFeitas,
    int Requisicoes,
    int RegistrosEncontrados,
    int Validos,
    int Invalidos,
    string? ProfissionalAtual,
    string? ProcedimentoAtual,
    /// <summary>Início da corrida, para a tela mostrar há quanto tempo ela dura.</summary>
    DateTime IniciadoEm);

/// <summary>
/// Verdade do "está varrendo" — memória, não banco (o banco não sabe se o processo morreu).
/// Singleton, uma varredura por vez em toda a instalação: as unidades compartilham o mesmo IP de
/// saída para o SISREG, então paralelizar só aproximaria o bloqueio anti-robô.
/// </summary>
public sealed class VarreduraSisregEstadoVivo
{
    private readonly Lock _trava = new();
    private ProgressoVarredura? _progresso;
    private CancellationTokenSource? _cts;

    public void Iniciar(ProgressoVarredura progresso, CancellationTokenSource cts)
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

    /// <summary>Parada pedida pelo operador (≠ app caindo) — o serviço discrimina no catch.</summary>
    public bool Cancelar()
    {
        lock (_trava)
        {
            if (_cts is null) return false;
            try { _cts.Cancel(); return true; }
            catch (ObjectDisposedException) { return false; }
        }
    }

    public StatusVarreduraVivo? ObterAtual()
    {
        lock (_trava)
        {
            if (_progresso is not { } p) return null;
            return new StatusVarreduraVivo(
                p.ExecucaoId, p.UnidadeId, p.UnidadeNome, true, p.CombinacoesTotal, p.CombinacoesFeitas,
                p.Requisicoes, p.RegistrosEncontrados, p.Validos, p.Invalidos,
                p.ProfissionalAtual, p.ProcedimentoAtual, p.IniciadoEm);
        }
    }
}
