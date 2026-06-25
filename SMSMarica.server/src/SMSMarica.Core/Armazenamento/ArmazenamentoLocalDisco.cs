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
    private readonly string _prefixo;

    public ArmazenamentoLocalDisco(IConfiguration configuration)
    {
        _prefixo = (configuration["Armazenamento:Prefixo"] ?? "arquivos").Trim('/');

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

    public string MontarChaveDocumento(Guid pacienteId, Guid documentoId, string extensao = "pdf")
        => ChavesArmazenamento.Documento(_prefixo, pacienteId, documentoId, extensao);

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

// Implementação de objeto (DigitalOcean Spaces / S3): ver ArmazenamentoSpaces.cs.
// Seleção via Armazenamento:Provedor ("local" | "spaces") em Core/DependencyInjection.
