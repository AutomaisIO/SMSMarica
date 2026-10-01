using System.IO.Compression;
using System.Text.Json;
using SMSMais.Core.Common.Excecoes;

namespace SMSMais.Core.Extensao.Distribuicao;

/// <summary>
/// Confere o .zip da extensão na hora de publicar. As regras são as mesmas que o atualizador
/// aplica no computador antes de trocar os arquivos — um pacote que ele recusaria não pode chegar
/// a ser publicado, senão a versão fica anunciada e nenhum computador a instala.
/// </summary>
public static class PacoteDaExtensao
{
    private const string Manifesto = "manifest.json";
    private const long LimiteDescompactado = 100L * 1024 * 1024;
    private static readonly string[] ExtensoesIgnoradas = [".bat", ".cmd", ".ps1", ".exe"];

    /// <summary>Devolve a versão lida do manifest. Lança <see cref="ValidacaoException"/> se o pacote não se sustenta.</summary>
    public static string LerVersao(byte[] zip)
    {
        var arquivos = LerArquivos(zip);
        if (!arquivos.TryGetValue(Manifesto, out var bruto))
            throw Recusa("o pacote não tem manifest.json");

        JsonDocument manifesto;
        try
        {
            manifesto = JsonDocument.Parse(bruto);
        }
        catch (JsonException)
        {
            throw Recusa("o manifest.json do pacote não é um JSON válido");
        }

        using (manifesto)
        {
            var raiz = manifesto.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
                throw Recusa("o manifest.json do pacote não é um JSON válido");
            var versao = Texto(raiz, "version")
                ?? throw Recusa("o manifest.json do pacote não tem \"version\"");
            if (!VersaoPublicada.Valida(versao))
                throw Recusa($"a versão \"{versao}\" do manifest não está no formato 1.2.3");

            foreach (var pedido in Referenciados(raiz))
            {
                if (!arquivos.ContainsKey(pedido))
                    throw Recusa($"o manifest manda carregar \"{pedido}\" e o arquivo não está no pacote");
            }
            return versao;
        }
    }

    private static Dictionary<string, byte[]> LerArquivos(byte[] zip)
    {
        var entradas = new List<(string[] Partes, byte[] Conteudo)>();
        try
        {
            using var arquivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
            long total = 0;
            foreach (var entrada in arquivo.Entries)
            {
                var nome = entrada.FullName.Replace('\\', '/');
                if (nome.EndsWith('/')) continue; // pasta
                var partes = nome.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (partes.Length == 0) continue;
                if (nome.StartsWith('/') || partes.Any(p => p is "." or ".." || p.Contains(':')))
                    throw Recusa($"o pacote tem um caminho inválido (\"{entrada.FullName}\")");

                total += entrada.Length;
                if (total > LimiteDescompactado)
                    throw Recusa("o pacote é grande demais");
                using var origem = entrada.Open();
                using var destino = new MemoryStream();
                origem.CopyTo(destino);
                entradas.Add((partes, destino.ToArray()));
            }
        }
        catch (InvalidDataException)
        {
            throw Recusa("o arquivo não é um .zip válido");
        }

        // O "Download ZIP" de um repositório traz tudo dentro de uma pasta; ela sai do caminho.
        if (entradas.Count > 0 && entradas.All(e => e.Partes.Length > 1 && e.Partes[0] == entradas[0].Partes[0]))
            entradas = entradas.Select(e => (e.Partes[1..], e.Conteudo)).ToList();

        return entradas
            .Where(e => !Ignorado(e.Partes))
            .GroupBy(e => string.Join('/', e.Partes))
            .ToDictionary(g => g.Key, g => g.Last().Conteudo);
    }

    private static bool Ignorado(string[] partes)
    {
        if (partes.Any(p => p.StartsWith('.'))) return true;
        var nome = partes[^1].ToLowerInvariant();
        return nome == "atualizador.json" || ExtensoesIgnoradas.Any(nome.EndsWith);
    }

    private static IEnumerable<string> Referenciados(JsonElement raiz)
    {
        if (raiz.TryGetProperty("background", out var fundo) && Texto(fundo, "service_worker") is { } trabalhador)
            yield return Limpar(trabalhador);
        if (raiz.TryGetProperty("action", out var acao) && Texto(acao, "default_popup") is { } popup)
            yield return Limpar(popup);
        if (Texto(raiz, "options_page") is { } opcoes)
            yield return Limpar(opcoes);
        if (!raiz.TryGetProperty("content_scripts", out var scripts) || scripts.ValueKind != JsonValueKind.Array)
            yield break;
        foreach (var script in scripts.EnumerateArray())
        {
            foreach (var lista in new[] { "js", "css" })
            {
                if (script.ValueKind != JsonValueKind.Object
                    || !script.TryGetProperty(lista, out var nomes)
                    || nomes.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var nome in nomes.EnumerateArray())
                {
                    if (nome.ValueKind == JsonValueKind.String && nome.GetString() is { Length: > 0 } arquivo)
                        yield return Limpar(arquivo);
                }
            }
        }
    }

    private static string Limpar(string caminho)
    {
        while (caminho.StartsWith("./", StringComparison.Ordinal)) caminho = caminho[2..];
        return caminho.TrimStart('/');
    }

    private static string? Texto(JsonElement objeto, string propriedade) =>
        objeto.ValueKind == JsonValueKind.Object
        && objeto.TryGetProperty(propriedade, out var valor)
        && valor.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(valor.GetString())
            ? valor.GetString()!.Trim()
            : null;

    private static ValidacaoException Recusa(string motivo) => new("arquivo", $"Pacote recusado: {motivo}.");
}
