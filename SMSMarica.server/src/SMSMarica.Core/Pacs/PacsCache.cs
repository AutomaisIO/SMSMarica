using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SMSMarica.Core.Pacs;

/// <summary>
/// Implementação do <see cref="IPacsCache"/> em disco com evicção LRU.
/// Cada item vira dois arquivos no diretório: <c>{chave}.bin</c> (bytes) e
/// <c>{chave}.ct</c> (content-type). O "último acesso" é o mtime do .bin —
/// atualizado a cada hit — e serve de critério de evicção LRU.
/// Registrado como Singleton (estado/IO compartilhado entre requisições).
/// </summary>
public sealed class PacsCache : IPacsCache
{
    private readonly PacsCacheOptions _opcoes;
    private readonly ILogger<PacsCache> _logger;
    private readonly string _diretorio;
    private readonly long _tetoTotalBytes;
    private readonly long _tetoItemBytes;

    // Discriminante da representação: o transfer-syntax preferido entra na chave
    // para que ligar/desligar essa config não faça o cache servir bytes na sintaxe
    // antiga. É global (mesma config p/ GET e pré-aquecimento), então as chaves batem.
    private readonly string _discriminante;

    // Serializa escritas/evicções. Leituras (TryGet) só tocam arquivos próprios,
    // mas o lock também protege contra evicção concorrente removendo o item.
    private readonly object _trava = new();

    public PacsCache(IOptions<PacsCacheOptions> opcoes, IConfiguration configuration, ILogger<PacsCache> logger)
    {
        _opcoes = opcoes.Value;
        _logger = logger;
        _discriminante = configuration["Pacs:Dcm4chee:TransferSyntaxPreferido"] ?? string.Empty;
        _diretorio = string.IsNullOrWhiteSpace(_opcoes.Diretorio)
            ? Path.Combine(Path.GetTempPath(), "smsmarica-pacs-cache")
            : _opcoes.Diretorio!;
        _tetoTotalBytes = Math.Max(0, _opcoes.TamanhoMaximoMb) * 1024L * 1024L;
        _tetoItemBytes = Math.Max(0, _opcoes.TamanhoMaximoItemMb) * 1024L * 1024L;

        if (_opcoes.Habilitado)
        {
            try
            {
                Directory.CreateDirectory(_diretorio);
            }
            catch (Exception ex)
            {
                // Falha ao preparar o diretório não pode derrubar a aplicação:
                // o cache simplesmente vira best-effort (miss em tudo).
                _logger.LogWarning(ex, "Não foi possível criar o diretório de cache do PACS em {Dir}.", _diretorio);
            }
        }
    }

    public bool Habilitado => _opcoes.Habilitado;

    public long TetoItemBytes => _tetoItemBytes;

    public string CalcularChave(string metodo, string caminho, string queryString)
    {
        var bruto = $"{_discriminante}|{metodo}|{caminho}|{queryString}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(bruto));
        return Convert.ToHexString(hash); // hex maiúsculo, seguro como nome de arquivo
    }

    public bool Contains(string chave)
    {
        if (!_opcoes.Habilitado) return false;
        try
        {
            return File.Exists(CaminhoBin(chave));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao checar existência no cache do PACS {Chave}.", chave);
            return false;
        }
    }

    public bool TryGet(string chave, out string contentType, out byte[] bytes)
    {
        contentType = "application/octet-stream";
        bytes = [];
        if (!_opcoes.Habilitado) return false;

        var arquivoBin = CaminhoBin(chave);
        var arquivoCt = CaminhoCt(chave);
        try
        {
            lock (_trava)
            {
                if (!File.Exists(arquivoBin)) return false;

                bytes = File.ReadAllBytes(arquivoBin);
                if (File.Exists(arquivoCt))
                {
                    var ct = File.ReadAllText(arquivoCt);
                    if (!string.IsNullOrWhiteSpace(ct)) contentType = ct;
                }

                // Marca como recém-usado (LRU baseado no último acesso).
                File.SetLastWriteTimeUtc(arquivoBin, DateTime.UtcNow);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler item de cache do PACS {Chave}.", chave);
            return false;
        }
    }

    public void Set(string chave, string contentType, byte[] bytes)
    {
        if (!_opcoes.Habilitado) return;
        if (bytes is null || bytes.LongLength == 0) return;
        if (_tetoItemBytes > 0 && bytes.LongLength > _tetoItemBytes) return; // grande demais

        var arquivoBin = CaminhoBin(chave);
        var arquivoCt = CaminhoCt(chave);
        try
        {
            lock (_trava)
            {
                Directory.CreateDirectory(_diretorio);
                File.WriteAllBytes(arquivoBin, bytes);
                File.WriteAllText(arquivoCt, contentType ?? "application/octet-stream");
                EvictarSeNecessario();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao gravar item de cache do PACS {Chave}.", chave);
        }
    }

    /// <summary>
    /// Remove os itens menos recentemente usados até o total ficar abaixo do teto.
    /// Deve ser chamado sob <see cref="_trava"/>.
    /// </summary>
    private void EvictarSeNecessario()
    {
        if (_tetoTotalBytes <= 0) return;

        var binarios = new DirectoryInfo(_diretorio).GetFiles("*.bin");
        long total = 0;
        foreach (var f in binarios) total += f.Length;
        if (total <= _tetoTotalBytes) return;

        // Mais antigos primeiro (LRU = menor último acesso/escrita).
        foreach (var f in binarios.OrderBy(f => f.LastWriteTimeUtc))
        {
            if (total <= _tetoTotalBytes) break;
            try
            {
                var tamanho = f.Length;
                f.Delete();
                var ct = Path.ChangeExtension(f.FullName, ".ct");
                if (File.Exists(ct)) File.Delete(ct);
                total -= tamanho;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao evictar item de cache do PACS {Arquivo}.", f.Name);
            }
        }
    }

    private string CaminhoBin(string chave) => Path.Combine(_diretorio, chave + ".bin");
    private string CaminhoCt(string chave) => Path.Combine(_diretorio, chave + ".ct");
}
