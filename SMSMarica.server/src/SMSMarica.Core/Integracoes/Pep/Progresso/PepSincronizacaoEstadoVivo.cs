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

    public void Iniciar(Guid execucaoId, Guid fonteId, string fonteNome,
        ModoSincronizacao modo, EscopoSincronizacao escopo, DateTime iniciadoEm, ProgressoImportacao progresso)
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
            _emExecucao = true;
        }
    }

    public void Finalizar()
    {
        lock (_lock)
        {
            _emExecucao = false;
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
                p.MedicationRequests, p.DocumentReferences, p.Observations, p.Falhas.Count);

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
                MensagemErro: null);
        }
    }
}
