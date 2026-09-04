using SMSMais.Core.Integracoes.SisregWeb.Escalas.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.Escalas.Background;

/// <summary>Progresso mutável — o serviço incrementa, o endpoint tira snapshot.</summary>
public sealed class ProgressoEscalas
{
    public required DisparoSincronizacao Disparo { get; init; }
    public required DateTime IniciadoEm { get; init; }
    public required Guid ExecucaoId { get; init; }

    /// <summary>
    /// Fase em texto humano. A maior parte do tempo é o download do arquivo, quando ainda não há
    /// denominador — sem a frase a tela ficaria em "0 de 0" parecendo travada.
    /// </summary>
    public volatile string Fase = FaseBaixando;

    public const string FaseBaixando = "Baixando a grade de escalas do SISREG";
    public const string FaseLendo = "Lendo o arquivo";
    public const string FaseGravando = "Atualizando as escalas";
    public const string FaseAusentes = "Marcando as escalas que sumiram do SISREG";

    public int EscalasLidas;
    public int EscalasGravadas;
    public int EscalasNovas;
    public int EscalasAtualizadas;
    public int LinhasRejeitadas;
    public int UnidadesNaoEncontradas;
    public string? UltimoErro;
}

/// <summary>
/// Verdade do "está sincronizando escalas" — memória, não banco: o banco não sabe se o processo
/// morreu. Singleton, uma por vez em toda a instalação, porque divide com os outros motores a
/// sessão única do operador e o orçamento anti-robô do SISREG.
/// </summary>
public sealed class EscalasSincronizacaoEstadoVivo
{
    private readonly Lock _trava = new();
    private ProgressoEscalas? _progresso;
    private CancellationTokenSource? _cts;

    public void Iniciar(ProgressoEscalas progresso, CancellationTokenSource cts)
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

    /// <summary>Parada pedida pelo operador (≠ app caindo).</summary>
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

    public EscalasSincronizacaoStatusDto? ObterAtual()
    {
        lock (_trava)
        {
            if (_progresso is not { } p) return null;
            return new EscalasSincronizacaoStatusDto(
                true, p.Disparo, p.Fase, p.EscalasLidas, p.EscalasGravadas, p.EscalasNovas,
                p.EscalasAtualizadas, p.LinhasRejeitadas, p.UnidadesNaoEncontradas,
                p.IniciadoEm, p.UltimoErro);
        }
    }
}
