using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace SMSMais.Core.Integracoes.SisregWeb.Rede;

/// <summary>Um IP devolvido pelo DNS e a interface por onde a rota do servidor até ele sai.</summary>
public sealed record IpObservado(string Ip, string? InterfaceRota);

/// <summary>O que uma verificação viu. <see cref="TunelEsperado"/> nulo = este servidor não tem túnel.</summary>
public sealed record ObservacaoEndereco(IReadOnlyList<IpObservado> Ips, string? TunelEsperado);

/// <summary>A parte que toca o sistema operacional (DNS e tabela de rotas) — separada para teste.</summary>
public interface ISondaEnderecoSisreg
{
    /// <summary>Resolve o host (só IPv4) e lê a rota de cada IP. Lança se o DNS falhar.</summary>
    Task<ObservacaoEndereco> ObservarAsync(string host, string interfaceTunel, CancellationToken ct);
}

public sealed partial class SondaEnderecoSisreg : ISondaEnderecoSisreg
{
    private static readonly TimeSpan TempoDoComando = TimeSpan.FromSeconds(5);

    public async Task<ObservacaoEndereco> ObservarAsync(string host, string interfaceTunel, CancellationToken ct)
    {
        var enderecos = await Dns.GetHostAddressesAsync(host, AddressFamily.InterNetwork, ct);
        var ips = enderecos.Select(e => e.ToString()).Distinct().Order(StringComparer.Ordinal).ToList();

        var observados = new List<IpObservado>(ips.Count);
        foreach (var ip in ips)
            observados.Add(new IpObservado(ip, await InterfaceDaRotaAsync(ip, ct)));

        var tunel = OperatingSystem.IsLinux() && !string.IsNullOrWhiteSpace(interfaceTunel)
                    && Directory.Exists($"/sys/class/net/{interfaceTunel}")
            ? interfaceTunel
            : null;
        return new ObservacaoEndereco(observados, tunel);
    }

    /// <summary><c>ip -o route get &lt;ip&gt;</c> — não precisa de root. Nulo fora do Linux ou se falhar.</summary>
    private static async Task<string?> InterfaceDaRotaAsync(string ip, CancellationToken ct)
    {
        if (!OperatingSystem.IsLinux()) return null;
        Process? processo = null;
        try
        {
            processo = Process.Start(new ProcessStartInfo("ip", ["-o", "route", "get", ip])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (processo is null) return null;

            using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limite.CancelAfter(TempoDoComando);
            var saida = await processo.StandardOutput.ReadToEndAsync(limite.Token);
            await processo.WaitForExitAsync(limite.Token);
            return processo.ExitCode == 0 ? LerInterface(saida) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Comando ausente, sem permissão ou preso além do limite: rota "não lida", não erro.
            return null;
        }
        finally
        {
            if (processo is { HasExited: false })
            {
                try { processo.Kill(); } catch (InvalidOperationException) { }
            }
            processo?.Dispose();
        }
    }

    /// <summary>
    /// Tira o <c>dev X</c> da saída do <c>ip route get</c>. Ex.: <c>159.60.146.75 dev wg-eveo src 10.206.0.2 uid 1001</c>
    /// → <c>wg-eveo</c>; <c>… via 146.190.64.1 dev eth0 src …</c> → <c>eth0</c>.
    /// </summary>
    public static string? LerInterface(string saida)
    {
        var m = RegexDev().Match(saida);
        return m.Success ? m.Groups[1].Value : null;
    }

    [GeneratedRegex(@"\bdev\s+(\S+)")]
    private static partial Regex RegexDev();
}
