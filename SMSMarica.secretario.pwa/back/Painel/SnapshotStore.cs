using System.Text.Json;

namespace SMSMarica.Secretario.Api.Painel;

/// <summary>
/// Guarda o snapshot corrente do painel em memória (troca atômica sob lock) e cuida da
/// persistência em disco — restart não serve tela vazia nem martela o Oracle.
/// </summary>
public sealed class SnapshotStore
{
    // Web defaults (camelCase) também no arquivo: o que está em disco é o mesmo JSON servido.
    private static readonly JsonSerializerOptions OpcoesJson =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly object _trava = new();
    private readonly string _caminhoArquivo;
    private readonly ILogger<SnapshotStore> _logger;
    private PainelSnapshot? _atual;

    public SnapshotStore(IConfiguration configuration, IHostEnvironment ambiente, ILogger<SnapshotStore> logger)
    {
        _logger = logger;
        var caminho = configuration["Painel:SnapshotPath"] ?? "snapshot-painel.json";
        _caminhoArquivo = Path.IsPathRooted(caminho)
            ? caminho
            : Path.Combine(ambiente.ContentRootPath, caminho);
    }

    public PainelSnapshot? Atual
    {
        get { lock (_trava) { return _atual; } }
    }

    /// <summary>
    /// Troca o snapshot corrente. Quem monta é o atualizador, que mantém o estado vivo de
    /// cada base — falha de uma delas chega aqui como snapshot completo com a fonte
    /// marcada em erro, nunca como snapshot vazio.
    /// </summary>
    public void Definir(PainelSnapshot snapshot)
    {
        lock (_trava)
        {
            _atual = snapshot;
        }
    }

    /// <summary>Carrega o snapshot persistido no startup, se existir (best-effort).</summary>
    public void CarregarDeDisco()
    {
        try
        {
            if (!File.Exists(_caminhoArquivo))
            {
                _logger.LogInformation("Nenhum snapshot persistido em {Caminho} — aguardando primeira carga.", _caminhoArquivo);
                return;
            }

            var json = File.ReadAllText(_caminhoArquivo);
            var snapshot = JsonSerializer.Deserialize<PainelSnapshot>(json, OpcoesJson);

            // Arquivo de uma versão anterior do contrato desserializa SEM erro, só com os
            // campos novos nulos (foi o caso na virada para multi-unidade em 25/07/2026).
            // Servir isso quebraria o painel de um jeito difícil de ler; melhor ignorar e
            // subir 503 até o primeiro ciclo — que leva um minuto.
            if (snapshot is null || snapshot.Unidades is not { Count: > 0 } || snapshot.Status is null)
            {
                _logger.LogWarning(
                    "Snapshot persistido em {Caminho} é de um contrato anterior — ignorado; aguardando primeira carga.",
                    _caminhoArquivo);
                return;
            }

            lock (_trava) { _atual = snapshot; }
            _logger.LogInformation("Snapshot persistido carregado (gerado em {GeradoEm:o}).", snapshot.GeradoEm);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao carregar snapshot persistido — seguindo sem ele.");
        }
    }

    /// <summary>Persiste o snapshot corrente em disco (chamado após cada ciclo lento OK).</summary>
    public async Task PersistirAsync(CancellationToken cancellationToken)
    {
        PainelSnapshot? snapshot;
        lock (_trava) { snapshot = _atual; }
        if (snapshot is null)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(snapshot, OpcoesJson);

            // Escrita atômica: tmp no MESMO diretório + File.Move com overwrite. Escrever
            // direto no destino podia deixar um snapshot truncado em queda de energia —
            // e o boot seguinte ficava sem cache (deserialização falha).
            var caminhoTmp = _caminhoArquivo + ".tmp";
            await File.WriteAllTextAsync(caminhoTmp, json, cancellationToken);
            File.Move(caminhoTmp, _caminhoArquivo, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao persistir snapshot em {Caminho} — seguindo só em memória.", _caminhoArquivo);
        }
    }
}
