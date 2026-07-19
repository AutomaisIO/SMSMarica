using MySqlConnector;

namespace Automais.Pabx.Api.Cdr;

public sealed record CdrRegistroDto(
    DateTime DataHora,
    string Origem,
    string Destino,
    string? CallerId,
    int DuracaoSegundos,
    int FaturadoSegundos,
    string Disposicao,
    string? Canal,
    string? CanalDestino,
    string UniqueId);

public sealed record CdrPaginaDto(
    IReadOnlyList<CdrRegistroDto> Itens,
    long Total,
    int Pagina,
    int TamanhoPagina);

public sealed record CdrFiltro(string? Ramal, string? Numero, DateOnly? De, DateOnly? Ate, int Pagina, int TamanhoPagina);

public interface ICdrService
{
    bool Configurado { get; }
    Task<CdrPaginaDto> ListarAsync(CdrFiltro filtro, CancellationToken ct = default);
}

/// <summary>
/// Lê a tabela cdr do MySQL local do Asterisk (cdr_mysql já ativo no servidor), só-leitura.
/// calldate é wall-clock local do PABX (Brasília) e é devolvido como está — regra única
/// de fuso do projeto: wall-clock não sofre conversão.
/// É o alicerce do futuro histórico de chamadas por paciente no SMSMarica
/// (correlação pelo telefone normalizado em dígitos).
/// </summary>
public sealed class CdrService(IConfiguration configuration) : ICdrService
{
    private string? ConnectionString => configuration.GetConnectionString("CdrDb");

    public bool Configurado => !string.IsNullOrWhiteSpace(ConnectionString);

    public async Task<CdrPaginaDto> ListarAsync(CdrFiltro filtro, CancellationToken ct = default)
    {
        var tamanho = Math.Clamp(filtro.TamanhoPagina, 1, 200);
        var pagina = Math.Max(filtro.Pagina, 1);

        var where = new List<string>();
        var parametros = new List<MySqlParameter>();

        if (!string.IsNullOrWhiteSpace(filtro.Ramal))
        {
            where.Add("(src = @ramal OR dst = @ramal)");
            parametros.Add(new MySqlParameter("@ramal", filtro.Ramal));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Numero))
        {
            // Normaliza para dígitos e compara por sufixo: cobre prefixos de operadora/DDI.
            var digitos = new string([.. filtro.Numero.Where(char.IsAsciiDigit)]);
            where.Add("(src LIKE @numero OR dst LIKE @numero)");
            parametros.Add(new MySqlParameter("@numero", $"%{digitos}"));
        }

        if (filtro.De is not null)
        {
            where.Add("calldate >= @de");
            parametros.Add(new MySqlParameter("@de", filtro.De.Value.ToDateTime(TimeOnly.MinValue)));
        }

        if (filtro.Ate is not null)
        {
            where.Add("calldate < @ate");
            parametros.Add(new MySqlParameter("@ate", filtro.Ate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue)));
        }

        var clausula = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        await using var conexao = new MySqlConnection(ConnectionString);
        await conexao.OpenAsync(ct);

        long total;
        await using (var contagem = conexao.CreateCommand())
        {
            contagem.CommandText = $"SELECT COUNT(*) FROM cdr {clausula}";
            foreach (var p in parametros)
                contagem.Parameters.Add(p.Clone());
            total = Convert.ToInt64(await contagem.ExecuteScalarAsync(ct));
        }

        var itens = new List<CdrRegistroDto>(tamanho);
        await using (var comando = conexao.CreateCommand())
        {
            comando.CommandText =
                $"""
                 SELECT calldate, src, dst, clid, duration, billsec, disposition, channel, dstchannel, uniqueid
                 FROM cdr {clausula}
                 ORDER BY calldate DESC
                 LIMIT @take OFFSET @skip
                 """;
            foreach (var p in parametros)
                comando.Parameters.Add(p.Clone());
            comando.Parameters.AddWithValue("@take", tamanho);
            comando.Parameters.AddWithValue("@skip", (pagina - 1) * tamanho);

            await using var reader = await comando.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                itens.Add(new CdrRegistroDto(
                    DataHora: reader.GetDateTime(0),
                    Origem: reader.IsDBNull(1) ? "" : reader.GetString(1),
                    Destino: reader.IsDBNull(2) ? "" : reader.GetString(2),
                    CallerId: reader.IsDBNull(3) ? null : reader.GetString(3),
                    DuracaoSegundos: reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
                    FaturadoSegundos: reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    Disposicao: reader.IsDBNull(6) ? "" : reader.GetString(6),
                    Canal: reader.IsDBNull(7) ? null : reader.GetString(7),
                    CanalDestino: reader.IsDBNull(8) ? null : reader.GetString(8),
                    UniqueId: reader.IsDBNull(9) ? "" : reader.GetString(9)));
            }
        }

        return new CdrPaginaDto(itens, total, pagina, tamanho);
    }
}
