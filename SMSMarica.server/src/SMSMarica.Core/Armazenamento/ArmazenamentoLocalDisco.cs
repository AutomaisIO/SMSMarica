using System.Globalization;
using Microsoft.Extensions.Configuration;
using SMSMarica.Core.Common.Excecoes;

namespace SMSMarica.Core.Armazenamento;

/// <summary>
/// Implementação local (disco) de <see cref="IArmazenamentoArquivos"/>. O diretório
/// base vem de <c>Armazenamento:Local:Diretorio</c> (absoluto em prod via env
/// <c>Armazenamento__Local__Diretorio</c>); caminho relativo é resolvido a partir do
/// diretório da aplicação. As chaves usam <c>/</c> como separador canônico
/// (compatível com S3) e são mapeadas para subpastas no disco.
/// </summary>
public sealed class ArmazenamentoLocalDisco : IArmazenamentoArquivos
{
    private readonly string _diretorioBase;

    public ArmazenamentoLocalDisco(IConfiguration configuration)
    {
        var configurado = configuration["Armazenamento:Local:Diretorio"];
        var dir = string.IsNullOrWhiteSpace(configurado)
            ? Path.Combine(AppContext.BaseDirectory, "App_Data", "arquivos")
            : configurado.Trim();

        // Caminho relativo → resolve a partir do diretório da aplicação (estável em dev/prod).
        if (!Path.IsPathRooted(dir))
        {
            dir = Path.Combine(AppContext.BaseDirectory, dir);
        }

        _diretorioBase = Path.GetFullPath(dir);
    }

    public string GerarChaveExame(string extensao = "pdf")
    {
        var agora = DateTime.UtcNow;
        var ext = (extensao ?? "pdf").Trim('.', ' ');
        if (string.IsNullOrEmpty(ext)) ext = "pdf";
        return string.Create(CultureInfo.InvariantCulture,
            $"exames/{agora:yyyy}/{agora:MM}/{Guid.NewGuid():N}.{ext}");
    }

    public async Task SalvarAsync(string chave, byte[] conteudo, CancellationToken cancellationToken = default)
    {
        var caminho = ResolverCaminho(chave);
        var pasta = Path.GetDirectoryName(caminho)!;
        Directory.CreateDirectory(pasta);
        await File.WriteAllBytesAsync(caminho, conteudo, cancellationToken);
    }

    public async Task<byte[]?> LerAsync(string chave, CancellationToken cancellationToken = default)
    {
        var caminho = ResolverCaminho(chave);
        if (!File.Exists(caminho)) return null;
        return await File.ReadAllBytesAsync(caminho, cancellationToken);
    }

    public Task ExcluirAsync(string chave, CancellationToken cancellationToken = default)
    {
        var caminho = ResolverCaminho(chave);
        if (File.Exists(caminho))
        {
            File.Delete(caminho);
        }
        return Task.CompletedTask;
    }

    /// <summary>Mapeia a chave (separada por <c>/</c>) para um caminho dentro do diretório base,
    /// recusando travessia de diretório (<c>..</c>).</summary>
    private string ResolverCaminho(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave))
        {
            throw new ValidacaoException("armazenamento.chave_invalida", "Chave de armazenamento vazia.");
        }

        var segmentos = chave.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segmentos.Any(s => s is "." or ".."))
        {
            throw new ValidacaoException("armazenamento.chave_invalida", "Chave de armazenamento inválida.");
        }

        var caminho = Path.GetFullPath(Path.Combine([_diretorioBase, .. segmentos]));

        // Defesa em profundidade: o caminho final precisa ficar dentro do diretório base.
        if (!caminho.StartsWith(_diretorioBase, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidacaoException("armazenamento.chave_invalida", "Chave de armazenamento fora do diretório base.");
        }

        return caminho;
    }
}

// TODO(arquivos): implementação S3 para DigitalOcean Spaces.
//
// Quando o volume de PDFs exigir armazenamento de objeto (ou múltiplas instâncias
// do backend), criar `ArmazenamentoS3 : IArmazenamentoArquivos` lendo as credenciais
// do provedor "digitalocean_spaces" (IIntegracaoCredencialService:
// clientId=accessKey, clientSecret=secretKey, parametrosJson={endpoint,region,bucket})
// e trocar o registro padrão no DI (Core/DependencyInjection) por configuração
// (ex.: Armazenamento:Provedor = "local" | "s3"). NÃO adicionar AWSSDK agora —
// este comentário é a costura (seam) intencional para essa evolução.
