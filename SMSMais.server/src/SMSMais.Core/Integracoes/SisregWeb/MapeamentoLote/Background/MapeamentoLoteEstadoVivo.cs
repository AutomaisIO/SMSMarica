using SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Background;

/// <summary>Progresso mutável do lote — o serviço incrementa, o endpoint tira snapshot.</summary>
public sealed class ProgressoMapeamentoLote
{
    public required DisparoSincronizacao Disparo { get; init; }
    public required DateTime IniciadoEm { get; init; }

    /// <summary>Linha da execução no banco, para a tela abrir o detalhe assim que termina.</summary>
    public required Guid ExecucaoId { get; init; }

    /// <summary>
    /// Em que pé está: a descoberta das unidades vem ANTES de existir denominador, e sem isto a
    /// tela mostraria "0/0 unidades" durante a primeira requisição, parecendo travada.
    /// </summary>
    public volatile string Fase = FaseDescoberta;

    public const string FaseDescoberta = "Descobrindo as unidades no SISREG";
    public const string FaseMapeamento = "Mapeando as unidades";

    /// <summary>Denominador do progresso. Só passa a valer depois da descoberta.</summary>
    public int UnidadesTotal;

    public int UnidadesFeitas;
    public int UnidadesNoSisreg;
    public int UnidadesCriadas;
    public int UnidadesMapeadas;
    public int UnidadesPuladas;
    public int RequisicoesFeitas;
    public int ProfissionaisEncontrados;
    public int ProfissionaisNovos;
    public int ProcedimentosEncontrados;
    public int ProcedimentosNovos;
    public int PractitionersCriados;
    public int PractitionersVinculados;
    public int UnidadesComErro;
    public int OrcamentoRestante;
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
                true, p.Disparo, p.Fase, p.UnidadesTotal, p.UnidadesFeitas, p.UnidadeAtual,
                p.UnidadesNoSisreg, p.UnidadesCriadas, p.UnidadesMapeadas, p.UnidadesPuladas,
                p.RequisicoesFeitas, p.ProfissionaisEncontrados, p.ProfissionaisNovos,
                p.ProcedimentosEncontrados, p.ProcedimentosNovos,
                p.PractitionersCriados, p.PractitionersVinculados, p.UnidadesComErro,
                p.OrcamentoRestante, p.IniciadoEm, p.UltimoErro);
        }
    }
}
