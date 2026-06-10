using SMSMarica.Core.Integracoes.Pep.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.Pep.Progresso;

/// <summary>
/// Estado vivo (singleton) do run de importação em andamento. O runner publica o início,
/// mantém uma referência ao <see cref="ProgressoImportacao"/> sendo mutado e marca o fim.
/// O endpoint de status lê um snapshot consistente sob lock. Quando não há run ativo,
/// <see cref="ObterAtual"/> devolve null e o serviço cai pro último registro persistido.
/// </summary>
public sealed class PepSincronizacaoEstadoVivo
{
    private readonly object _lock = new();

    private Guid? _execucaoId;
    private Guid? _fonteId;
    private string? _fonteNome;
    private ModoSincronizacao _modo;
    private EscopoSincronizacao _escopo;
    private DateTime? _iniciadoEm;
    private bool _emExecucao;
    private ProgressoImportacao? _progresso;
    private CancellationTokenSource? _cts;

    public void Iniciar(Guid execucaoId, Guid fonteId, string fonteNome,
        ModoSincronizacao modo, EscopoSincronizacao escopo, DateTime iniciadoEm,
        ProgressoImportacao progresso, CancellationTokenSource cts)
    {
        lock (_lock)
        {
            _execucaoId = execucaoId;
            _fonteId = fonteId;
            _fonteNome = fonteNome;
            _modo = modo;
            _escopo = escopo;
            _iniciadoEm = iniciadoEm;
            _progresso = progresso;
            _cts = cts;
            _emExecucao = true;
        }
    }

    public void Finalizar()
    {
        lock (_lock)
        {
            _emExecucao = false;
            _cts = null;
        }
    }

    /// <summary>
    /// Solicita o cancelamento do run vivo (botão "parar"). Devolve <c>false</c> se
    /// não há run cancelável. O cancelamento é cooperativo: a estratégia para no
    /// próximo ponto de checagem do token.
    /// </summary>
    public bool Cancelar()
    {
        lock (_lock)
        {
            if (!_emExecucao || _cts is null) return false;
            try { _cts.Cancel(); return true; }
            catch (ObjectDisposedException) { return false; }
        }
    }

    /// <summary>Snapshot do run vivo, ou null se nenhum está em execução.</summary>
    public StatusImportacaoDto? ObterAtual()
    {
        lock (_lock)
        {
            if (!_emExecucao || _progresso is null || _execucaoId is null)
            {
                return null;
            }

            var p = _progresso;
            var contadores = new ContadoresImportacaoDto(
                p.Medicos, p.Pacientes, p.Encounters, p.Conditions,
                p.MedicationRequests, p.DocumentReferences, p.Observations, p.Falhas.Count,
                p.Retentativas);

            // Snapshot das últimas falhas (cópia sob lock) — diagnóstico em tempo real.
            var ultimasFalhas = p.Falhas.Count == 0
                ? []
                : p.Falhas.TakeLast(5).Select(f => $"cd {f.Cd}: {f.Mensagem}").ToList();

            return new StatusImportacaoDto(
                ExecucaoId: _execucaoId,
                EmExecucao: true,
                FonteId: _fonteId,
                FonteNome: _fonteNome,
                Modo: _modo.ToString(),
                Escopo: _escopo.ToString(),
                Status: StatusSincronizacao.EmExecucao.ToString(),
                FaseAtual: p.FaseAtual,
                IniciadoEm: _iniciadoEm,
                FinalizadoEm: null,
                DecorridoSegundos: _iniciadoEm is { } ini ? (DateTime.UtcNow - ini).TotalSeconds : null,
                Contadores: contadores,
                MensagemErro: null,
                UltimasFalhas: ultimasFalhas);
        }
    }
}
