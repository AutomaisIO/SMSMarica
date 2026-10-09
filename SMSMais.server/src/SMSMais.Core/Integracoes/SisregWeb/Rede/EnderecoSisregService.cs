using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Rede;

public enum SituacaoEnderecoSisreg
{
    /// <summary>Ainda não houve verificação desde que o servidor subiu.</summary>
    NaoVerificado,

    /// <summary>Todos os IPs atuais saem pelo túnel esperado.</summary>
    NoTunel,

    /// <summary>Este servidor não tem túnel: sai direto (instância no Brasil).</summary>
    SaidaDireta,

    /// <summary>Há túnel, mas a rota de algum IP atual sai por outra interface — o SISREG não responde.</summary>
    ForaDoTunel,

    /// <summary>O DNS do SISREG não resolveu na última verificação.</summary>
    SemDns,

    /// <summary>Há túnel, mas não deu para ler a rota (comando indisponível).</summary>
    RotaDesconhecida,
}

public sealed record EnderecoSisregIpDto(string Ip, string? InterfaceRota, bool? NoTunel, DateTime? DesdeEm);

public sealed record EnderecoSisregPeriodoDto(
    string Ip, DateTime DesdeEm, DateTime UltimaVezVistoEm, DateTime? AteEm, string? InterfaceRota);

/// <summary>O que o card do SISREG em Integrações mostra.</summary>
public sealed record EnderecoSisregDto(
    string Host,
    SituacaoEnderecoSisreg Situacao,
    DateTime? VerificadoEm,
    string? TunelEsperado,
    string? Erro,
    IReadOnlyList<EnderecoSisregIpDto> Atuais,
    IReadOnlyList<EnderecoSisregPeriodoDto> Historico);

/// <summary>O que mudou numa verificação. <see cref="PrimeiraVez"/> = tabela vazia: só registra, não avisa.</summary>
public sealed record MudancaEndereco(
    IReadOnlyList<string> Entraram, IReadOnlyList<string> Anteriores, IReadOnlyList<string> Sairam, bool PrimeiraVez);

/// <summary>Última verificação (em memória — some no restart e volta na próxima, em até 2 min).</summary>
public sealed record UltimaVerificacaoEndereco(
    DateTime Em, IReadOnlyList<IpObservado> Ips, string? TunelEsperado, string? Erro);

/// <summary>Singleton: a última verificação, para a tela saber "agora" sem ir ao banco nem ao DNS.</summary>
public sealed class EstadoEnderecoSisreg
{
    private UltimaVerificacaoEndereco? _ultima;

    public UltimaVerificacaoEndereco? Ultima => Volatile.Read(ref _ultima);

    public void Registrar(UltimaVerificacaoEndereco verificacao) => Volatile.Write(ref _ultima, verificacao);
}

public interface IEnderecoSisregService
{
    /// <summary>Grava a observação nos períodos (abre IP novo, encerra IP que sumiu) e diz o que mudou.</summary>
    Task<MudancaEndereco> RegistrarAsync(ObservacaoEndereco observacao, DateTime agora, CancellationToken ct);

    Task<EnderecoSisregDto> ObterAsync(CancellationToken ct);
}

public sealed class EnderecoSisregService(
    SmsMaisDbContext db,
    EstadoEnderecoSisreg estado,
    IOptions<EnderecoSisregOpcoes> opcoes) : IEnderecoSisregService
{
    /// <summary>Quantos períodos a tela mostra. Troca de IP é rara; isso é anos de histórico.</summary>
    private const int PeriodosNaTela = 20;

    public async Task<MudancaEndereco> RegistrarAsync(ObservacaoEndereco observacao, DateTime agora, CancellationToken ct)
    {
        var primeiraVez = !await db.SisregEnderecosIp.AnyAsync(ct);
        var abertos = await db.SisregEnderecosIp.Where(x => x.AteEm == null).ToListAsync(ct);

        var plano = Reconciliar(
            abertos.Select(a => new PeriodoAberto(a.Id, a.Ip, a.UltimaVezVistoEm)).ToList(),
            observacao.Ips.Select(i => i.Ip).ToList(),
            agora,
            TimeSpan.FromMinutes(Math.Max(1, opcoes.Value.MinutosParaEncerrar)));

        foreach (var linha in abertos.Where(a => plano.Encerrar.Contains(a.Id)))
            linha.AteEm = agora;

        foreach (var ip in plano.Abrir)
        {
            var novo = new SisregEnderecoIp { Id = Guid.NewGuid(), Ip = ip, DesdeEm = agora };
            db.SisregEnderecosIp.Add(novo);
            abertos.Add(novo);
        }

        foreach (var obs in observacao.Ips)
        {
            var linha = abertos.First(a => a.Ip == obs.Ip && a.AteEm == null);
            linha.UltimaVezVistoEm = agora;
            linha.InterfaceRota = obs.InterfaceRota;
        }

        await db.SaveChangesAsync(ct);

        var sairam = abertos.Where(a => plano.Encerrar.Contains(a.Id)).Select(a => a.Ip).ToList();
        var anteriores = abertos
            .Where(a => !plano.Abrir.Contains(a.Ip) && !plano.Encerrar.Contains(a.Id))
            .Select(a => a.Ip)
            .Concat(sairam)
            .Distinct()
            .ToList();
        return new MudancaEndereco(plano.Abrir, anteriores, sairam, primeiraVez);
    }

    public async Task<EnderecoSisregDto> ObterAsync(CancellationToken ct)
    {
        var periodos = await db.SisregEnderecosIp.AsNoTracking()
            .OrderByDescending(x => x.DesdeEm)
            .Take(PeriodosNaTela)
            .ToListAsync(ct);

        var ultima = estado.Ultima;
        var atuais = (ultima?.Ips ?? [])
            .Select(i => new EnderecoSisregIpDto(
                i.Ip,
                i.InterfaceRota,
                ultima!.TunelEsperado is null || i.InterfaceRota is null ? null : i.InterfaceRota == ultima.TunelEsperado,
                periodos.FirstOrDefault(p => p.Ip == i.Ip && p.AteEm == null)?.DesdeEm))
            .ToList();

        return new EnderecoSisregDto(
            opcoes.Value.Host,
            Situacao(ultima),
            ultima?.Em,
            ultima?.TunelEsperado,
            ultima?.Erro,
            atuais,
            periodos.Select(p => new EnderecoSisregPeriodoDto(p.Ip, p.DesdeEm, p.UltimaVezVistoEm, p.AteEm, p.InterfaceRota))
                .ToList());
    }

    public sealed record PeriodoAberto(Guid Id, string Ip, DateTime UltimaVezVistoEm);

    public sealed record PlanoPeriodos(IReadOnlyList<string> Abrir, IReadOnlyList<Guid> Encerrar);

    /// <summary>
    /// Quais períodos abrir e quais encerrar. Puro, para teste. IP observado sem período aberto abre;
    /// período aberto cujo IP não aparece há mais de <paramref name="paraEncerrar"/> encerra. Sem
    /// nenhum IP observado (DNS vazio) não encerra nada — sumiço do DNS não é troca de IP.
    /// </summary>
    public static PlanoPeriodos Reconciliar(
        IReadOnlyCollection<PeriodoAberto> abertos, IReadOnlyCollection<string> observados, DateTime agora, TimeSpan paraEncerrar)
    {
        var abrir = observados.Distinct().Where(ip => abertos.All(a => a.Ip != ip)).ToList();
        var encerrar = observados.Count == 0
            ? []
            : abertos.Where(a => !observados.Contains(a.Ip) && agora - a.UltimaVezVistoEm > paraEncerrar)
                .Select(a => a.Id)
                .ToList();
        return new PlanoPeriodos(abrir, encerrar);
    }

    /// <summary>A situação mostrada no card. Pura, para teste.</summary>
    public static SituacaoEnderecoSisreg Situacao(UltimaVerificacaoEndereco? ultima)
    {
        if (ultima is null) return SituacaoEnderecoSisreg.NaoVerificado;
        if (ultima.Erro is not null || ultima.Ips.Count == 0) return SituacaoEnderecoSisreg.SemDns;
        if (ultima.TunelEsperado is null) return SituacaoEnderecoSisreg.SaidaDireta;
        if (IpsForaDoTunel(ultima.Ips, ultima.TunelEsperado).Count > 0) return SituacaoEnderecoSisreg.ForaDoTunel;
        return ultima.Ips.All(i => i.InterfaceRota == ultima.TunelEsperado)
            ? SituacaoEnderecoSisreg.NoTunel
            : SituacaoEnderecoSisreg.RotaDesconhecida;
    }

    /// <summary>IPs cuja rota foi LIDA e não sai pelo túnel. Rota não lida não conta como fora.</summary>
    public static IReadOnlyList<IpObservado> IpsForaDoTunel(IReadOnlyList<IpObservado> ips, string? tunelEsperado) =>
        tunelEsperado is null
            ? []
            : ips.Where(i => i.InterfaceRota is not null && i.InterfaceRota != tunelEsperado).ToList();
}
