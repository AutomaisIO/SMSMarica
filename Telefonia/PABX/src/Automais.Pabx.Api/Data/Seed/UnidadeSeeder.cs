using Automais.Pabx.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Automais.Pabx.Api.Data.Seed;

/// <summary>
/// Sincroniza a tabela unidade com o registro-mestre unidades.csv (cópia de
/// Telefonia/registro/unidades.csv embarcada no deploy). Upsert por id: roda em
/// todo startup, então atualizar o CSV + redeploy atualiza o serviço.
/// </summary>
public static class UnidadeSeeder
{
    public static async Task SeedAsync(PabxDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var caminho = Path.Combine(AppContext.BaseDirectory, "Data", "Seed", "unidades.csv");
        if (!File.Exists(caminho))
        {
            logger.LogWarning("Seed de unidades ignorado: {Caminho} não encontrado", caminho);
            return;
        }

        var linhas = await File.ReadAllLinesAsync(caminho, ct);
        var existentes = await db.Unidades.ToDictionaryAsync(u => u.Id, ct);
        var quantidade = 0;

        foreach (var linha in linhas.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(linha))
                continue;

            var campos = ParseCsv(linha);
            if (campos.Count < 10 || !int.TryParse(campos[0], out var id))
                continue;

            if (!existentes.TryGetValue(id, out var unidade))
            {
                unidade = new Unidade { Id = id, Nome = "", Grupo = "", Lan = "", GatewayMk = "", TunnelIp = "", Status = "" };
                db.Unidades.Add(unidade);
            }

            unidade.Nome = campos[1];
            unidade.Grupo = campos[2];
            unidade.Lan = campos[3];
            unidade.GatewayMk = campos[4];
            unidade.TunnelIp = campos[6];
            unidade.Status = campos[9];
            unidade.Endereco = campos.Count > 10 ? Vazio(campos[10]) : null;
            unidade.Gestor = campos.Count > 11 ? Vazio(campos[11]) : null;
            quantidade++;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seed de unidades: {Qtde} sincronizadas", quantidade);
    }

    private static string? Vazio(string s) => string.IsNullOrWhiteSpace(s) ? null : s;

    /// <summary>Parser CSV mínimo com suporte a aspas (endereços contêm vírgula).</summary>
    private static List<string> ParseCsv(string linha)
    {
        var campos = new List<string>();
        var atual = new System.Text.StringBuilder();
        var entreAspas = false;

        foreach (var c in linha)
        {
            if (c == '"')
                entreAspas = !entreAspas;
            else if (c == ',' && !entreAspas)
            {
                campos.Add(atual.ToString().Trim());
                atual.Clear();
            }
            else
                atual.Append(c);
        }

        campos.Add(atual.ToString().Trim());
        return campos;
    }
}
