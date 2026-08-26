using SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Background;

/// <summary>Progresso mutável do lote — o serviço incrementa, o endpoint tira snapshot.</summary>
public sealed class ProgressoMapeamentoLote
{
    public required DisparoSincronizacao Disparo { get; init; }
    public required int UnidadesTotal { get; init; }
    public required DateTime IniciadoEm { get; init; }
    public int UnidadesFeitas;
    public int RequisicoesFeitas;
    public int ProfissionaisEncontrados;
    public int ProfissionaisNovos;
    public int PractitionersCriados;
    public int PractitionersVinculados;
    public int UnidadesComErro;
    public string? UnidadeAtual;
    public string? UltimoErro;
}

/// <summary>
/// Verdade do "está sincronizando o mapeamento de todas as unidades" — memória, não banco (o banco
/// não sabe se o processo morreu). Singleton, <b>um lote por vez em toda a instalação</b>: as
/// unidades saem para o SISREG pelo mesmo IP e dividem o orçamento anti-robô com a varredura de
/// agenda, então paralelizar só aproximaria o CAPTCHA.
/// </summary>
public sealed class MapeamentoLoteEstadoVivo
{
    private readonly Lock _trava = new();
    private ProgressoMapeamentoLote? _progresso;
    private CancellationTokenSource? _cts;

    public void Iniciar(ProgressoMapeamentoLote progresso, CancellationTokenSource cts)
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

    public MapeamentoLoteStatusDto? ObterAtual()
    {
        lock (_trava)
        {
            if (_progresso is not { } p) return null;
            return new MapeamentoLoteStatusDto(
                true, p.Disparo, p.UnidadesTotal, p.UnidadesFeitas, p.UnidadeAtual,
                p.RequisicoesFeitas, p.ProfissionaisEncontrados, p.ProfissionaisNovos,
                p.PractitionersCriados, p.PractitionersVinculados, p.UnidadesComErro,
                p.IniciadoEm, p.UltimoErro);
        }
    }
}
