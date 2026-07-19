using System.Text.RegularExpressions;
using Automais.Pabx.Api.Asterisk.Ami;
using Automais.Pabx.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Automais.Pabx.Api.Ramais;

public interface IStatusService
{
    Task<StatusGeralDto> ObterStatusAsync(CancellationToken ct = default);
}

/// <summary>
/// Consulta o registro SIP em tempo real via AMI (SIPpeers + CoreShowChannels)
/// e cruza com o inventário. O IP de origem identifica a unidade: sem NAT na VPN,
/// o Asterisk enxerga o 10.200.&lt;id&gt;.x real do aparelho.
/// </summary>
public sealed partial class StatusService(
    PabxDbContext db,
    IAmiClientFactory amiFactory,
    TimeProvider timeProvider) : IStatusService
{
    [GeneratedRegex(@"OK \((?<ms>\d+) ms\)")]
    private static partial Regex LatenciaRegex();

    [GeneratedRegex(@"^SIP/(?<numero>\d+)-")]
    private static partial Regex CanalRegex();

    public async Task<StatusGeralDto> ObterStatusAsync(CancellationToken ct = default)
    {
        var ramais = await db.Ramais.Include(r => r.Unidade).AsNoTracking()
            .OrderBy(r => r.Numero)
            .ToListAsync(ct);

        var agora = timeProvider.GetUtcNow().UtcDateTime;

        if (!amiFactory.Habilitado)
        {
            var offline = ramais.Select(r => new StatusRamalDto(
                r.Numero, r.Descricao, r.UnidadeId, r.Unidade?.Nome, null, r.Origem,
                Online: false, Ip: null, LatenciaMs: null, EmChamada: false, StatusBruto: "AMI desabilitado"));
            return new StatusGeralDto(AmiDisponivel: false, agora, [.. offline]);
        }

        await using var ami = await amiFactory.ConectarAsync(ct);

        var peers = await ami.ExecutarListaAsync("SIPpeers", "PeerlistComplete", ct: ct);
        var porNumero = peers
            .Where(p => p["ObjectName"] is not null)
            .ToDictionary(p => p["ObjectName"]!, StringComparer.Ordinal);

        var canais = await ami.ExecutarListaAsync("CoreShowChannels", "CoreShowChannelsComplete", ct: ct);
        var emChamada = new HashSet<string>(StringComparer.Ordinal);
        foreach (var canal in canais)
        {
            var m = CanalRegex().Match(canal["Channel"] ?? "");
            if (m.Success)
                emChamada.Add(m.Groups["numero"].Value);
        }

        var resultado = new List<StatusRamalDto>(ramais.Count);
        foreach (var ramal in ramais)
        {
            porNumero.TryGetValue(ramal.Numero, out var peer);
            var statusBruto = peer?["Status"];
            var ip = peer?["IPaddress"];
            if (ip is "-none-" or "")
                ip = null;

            var latencia = statusBruto is not null && LatenciaRegex().Match(statusBruto) is { Success: true } lm
                ? int.Parse(lm.Groups["ms"].Value)
                : (int?)null;

            resultado.Add(new StatusRamalDto(
                ramal.Numero,
                ramal.Descricao,
                ramal.UnidadeId,
                ramal.Unidade?.Nome,
                DetectarUnidade(ip),
                ramal.Origem,
                Online: statusBruto?.StartsWith("OK", StringComparison.OrdinalIgnoreCase) == true,
                Ip: ip,
                LatenciaMs: latencia,
                EmChamada: emChamada.Contains(ramal.Numero),
                StatusBruto: statusBruto ?? "não registrado no chan_sip"));
        }

        return new StatusGeralDto(AmiDisponivel: true, agora, resultado);
    }

    /// <summary>10.200.&lt;id&gt;.x → id da unidade (plano de endereçamento do hub WireGuard).</summary>
    private static int? DetectarUnidade(string? ip)
    {
        if (ip is null)
            return null;
        var partes = ip.Split('.');
        return partes.Length == 4 && partes[0] == "10" && partes[1] == "200" && int.TryParse(partes[2], out var id)
            ? id
            : null;
    }
}
