using System.Text.Json;

namespace SMSMarica.Secretario.Api.Painel;

/// <summary>
/// Guarda o snapshot corrente do painel em memória (troca atômica sob lock) e cuida da
/// persistência em disco — restart não serve tela vazia nem martela o Oracle.
/// </summary>
public sealed class SnapshotStore
{
    public const string FontePadrao = "Salux HIS — Hospital Municipal Conde Modesto Leal";

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

    /// <summary>Aplica uma mutação sobre o snapshot corrente (pode ser nulo) e troca a referência.</summary>
    public PainelSnapshot Atualizar(Func<PainelSnapshot?, PainelSnapshot> mutacao)
    {
        lock (_trava)
        {
            _atual = mutacao(_atual);
            return _atual;
        }
    }

    /// <summary>
    /// Marca falha do Oracle mantendo o último snapshot bom. Se ainda não há snapshot
    /// nenhum, não cria um vazio — o endpoint continua respondendo 503.
    /// </summary>
    public void MarcarFalha(string erroCurto)
    {
        lock (_trava)
        {
            if (_atual is null)
            {
                return;
            }

            _atual = _atual with
            {
                GeradoEm = FusoBrasilia.Agora(),
                Oracle = _atual.Oracle with { Ok = false, UltimoErro = erroCurto },
            };
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
            if (snapshot is not null)
            {
                lock (_trava) { _atual = snapshot; }
                _logger.LogInformation("Snapshot persistido carregado (gerado em {GeradoEm:o}).", snapshot.GeradoEm);
            }
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
