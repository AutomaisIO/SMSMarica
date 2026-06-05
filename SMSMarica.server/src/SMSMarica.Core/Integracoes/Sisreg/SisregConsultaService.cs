using SMSMarica.Core.Integracoes.Sisreg.Configuracao;
using SMSMarica.Core.Integracoes.Sisreg.Dtos;

namespace SMSMarica.Core.Integracoes.Sisreg;

/// <summary>
/// Monta os corpos de consulta Elasticsearch descritos no Manual da API SISREG v2.1 §4
/// e delega a execução ao <see cref="ISisregClient"/>. O filtro de centrais reguladoras
/// vem da configuração (não hardcoded), o que mantém a integração reutilizável.
/// Não projetamos <c>_source</c>: a projeção fica nos DTOs do índice (campos extras são ignorados).
/// </summary>
public sealed class SisregConsultaService(
    ISisregClient client,
    ISisregConfiguracaoService configuracao) : ISisregConsultaService
{
    // Status ambulatoriais (Manual §6.3) usados nos filtros das consultas.
    private static readonly string[] StatusAgendadas =
    [
        "SOLICITAÇÃO / AGENDADA / FILA DE ESPERA",
        "SOLICITAÇÃO / AGENDADA / SOLICITANTE",
        "SOLICITAÇÃO / AUTORIZADA / REGULADOR",
        "AGENDAMENTO / PENDENTE CONFIRMAÇÃO / EXECUTANTE",
        "SOLICITAÇÃO / AGENDADA / COORDENADOR",
    ];

    private static readonly string[] StatusAtendidas =
    [
        "AGENDAMENTO / CONFIRMADO / EXECUTANTE",
    ];

    private static readonly string[] StatusFila =
    [
        "SOLICITAÇÃO / PENDENTE / FILA DE ESPERA",
        "SOLICITAÇÃO / PENDENTE / REGULADOR",
        "SOLICITAÇÃO / REENVIADA / REGULADOR",
    ];

    private static readonly string[] StatusCanceladasDevolvidas =
    [
        "SOLICITAÇÃO / CANCELADA / SOLICITANTE",
        "SOLICITAÇÃO / CANCELADA / REGULADOR",
        "SOLICITAÇÃO / CANCELADA / COORDENADOR",
        "AGENDAMENTO / CANCELADO / REGULADOR",
        "AGENDAMENTO / CANCELADO / SOLICITANTE",
        "AGENDAMENTO / CANCELADO / COORDENADOR",
        "SOLICITAÇÃO / NEGADA / REGULADOR",
        "SOLICITAÇÃO / DEVOLVIDA / REGULADOR",
    ];

    public async Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> NovasSolicitacoesAmbulatoriaisAsync(
        IntervaloDatas intervalo, int tamanho = 100, CancellationToken cancellationToken = default)
    {
        var must = new List<object> { await TermsCentraisAsync(cancellationToken) };
        must.Add(Range("data_solicitacao", intervalo));
        var corpo = MontarCorpo(must, tamanho, ordenarPor: "data_solicitacao");
        return await client.BuscarAsync<MarcacaoAmbulatorialSisregDto>(
            TipoIndiceSisreg.MarcacaoAmbulatorial, corpo, cancellationToken);
    }

    public async Task<SisregBuscaResultado<SolicitacaoAmbulatorialSisregDto>> FilaSolicitacoesAmbulatoriaisAsync(
        int tamanho = 100, CancellationToken cancellationToken = default)
    {
        var must = new List<object> { await TermsCentraisAsync(cancellationToken) };
        must.Add(TermsStatus(StatusFila));
        var corpo = MontarCorpo(must, tamanho);
        return await client.BuscarAsync<SolicitacaoAmbulatorialSisregDto>(
            TipoIndiceSisreg.SolicitacaoAmbulatorial, corpo, cancellationToken);
    }

    public async Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> SolicitacoesAgendadasAsync(
        IntervaloDatas intervalo, int tamanho = 100, CancellationToken cancellationToken = default)
    {
        var must = new List<object>
        {
            Range("data_aprovacao", intervalo),
            await TermsCentraisAsync(cancellationToken),
            TermsStatus(StatusAgendadas),
        };
        var corpo = MontarCorpo(must, tamanho, ordenarPor: "data_aprovacao");
        return await client.BuscarAsync<MarcacaoAmbulatorialSisregDto>(
            TipoIndiceSisreg.MarcacaoAmbulatorial, corpo, cancellationToken);
    }

    public async Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> SolicitacoesAtendidasAsync(
        IntervaloDatas intervalo, int tamanho = 100, CancellationToken cancellationToken = default)
    {
        var must = new List<object>
        {
            Range("data_confirmacao", intervalo),
            await TermsCentraisAsync(cancellationToken),
            TermsStatus(StatusAtendidas),
        };
        var corpo = MontarCorpo(must, tamanho, ordenarPor: "data_confirmacao");
        return await client.BuscarAsync<MarcacaoAmbulatorialSisregDto>(
            TipoIndiceSisreg.MarcacaoAmbulatorial, corpo, cancellationToken);
    }

    public async Task<SisregBuscaResultado<MarcacaoAmbulatorialSisregDto>> SolicitacoesCanceladasDevolvidasAsync(
        int tamanho = 100, CancellationToken cancellationToken = default)
    {
        var must = new List<object>
        {
            await TermsCentraisAsync(cancellationToken),
            TermsStatus(StatusCanceladasDevolvidas),
        };
        var corpo = MontarCorpo(must, tamanho);
        return await client.BuscarAsync<MarcacaoAmbulatorialSisregDto>(
            TipoIndiceSisreg.MarcacaoAmbulatorial, corpo, cancellationToken);
    }

    public async Task<SisregBuscaResultado<SolicitacaoHospitalarSisregDto>> InternacoesHospitalaresAsync(
        IntervaloDatas intervalo, int tamanho = 100, CancellationToken cancellationToken = default)
    {
        var must = new List<object>
        {
            await TermsCentraisAsync(cancellationToken),
            Range("data_solicitacao", intervalo),
        };
        var corpo = MontarCorpo(must, tamanho, ordenarPor: "data_solicitacao");
        return await client.BuscarAsync<SolicitacaoHospitalarSisregDto>(
            TipoIndiceSisreg.SolicitacaoHospitalar, corpo, cancellationToken);
    }

    // ---- Builders Elasticsearch ----

    private async Task<object> TermsCentraisAsync(CancellationToken cancellationToken)
    {
        var ctx = await configuracao.ObterContextoAsync(cancellationToken);
        return new
        {
            terms = new Dictionary<string, object>
            {
                ["codigo_central_reguladora"] = ctx.CentraisReguladoras,
            },
        };
    }

    private static object TermsStatus(string[] valores) => new
    {
        terms = new Dictionary<string, object> { ["status_solicitacao.keyword"] = valores },
    };

    private static object Range(string campo, IntervaloDatas intervalo) => new
    {
        range = new Dictionary<string, object>
        {
            [campo] = new
            {
                gte = intervalo.Inicio.ToString("yyyy-MM-dd"),
                lte = intervalo.Fim.ToString("yyyy-MM-dd"),
            },
        },
    };

    private static Dictionary<string, object> MontarCorpo(
        List<object> must, int tamanho, string? ordenarPor = null)
    {
        var corpo = new Dictionary<string, object>
        {
            ["query"] = new { @bool = new { must } },
            ["size"] = Math.Clamp(tamanho, 1, 1000),
        };

        if (ordenarPor is not null)
        {
            corpo["sort"] = new[]
            {
                new Dictionary<string, object> { [ordenarPor] = new { order = "asc" } },
            };
        }

        return corpo;
    }
}
