using AngleSharp.Html.Dom;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Integracoes.SerWeb;
using SMSMais.Core.Ser.Dtos;
using SMSMais.Core.Sernit;
using SMSMais.Core.Sernit.Dtos;

namespace SMSMais.Core.Ser.Criacao;

/// <summary>
/// O que o motor da tela de criação (<see cref="SerCriacaoSolicitacao"/>) precisa da sessão do
/// sistema. O SER-RJ e o SERNIT são a MESMA aplicação (JSF 1.2 + RichFaces 3.3 + Seam) em
/// instâncias diferentes, com sessões próprias (o SERNIT tem o login com Referer); daqui para
/// cima, o caminho é um só.
/// </summary>
public interface ITransporteTelaCriacao
{
    Task<string> AbrirTelaAsync(string caminho, CancellationToken ct);

    /// <summary>POST de LEITURA — passa pela trava de somente-leitura da sessão.</summary>
    Task<string> SubmeterLeituraAsync(
        string html, string formId, IReadOnlyDictionary<string, string> extras, string? viewState,
        CancellationToken ct);

    /// <summary>POST de ESCRITA, nomeado no log.</summary>
    Task<string> SubmeterEscritaAsync(
        string html, string formId, IReadOnlyDictionary<string, string> extras, string? viewState,
        string operacao, CancellationToken ct);

    /// <summary>Upload do <c>rich:fileUpload</c> — ESCRITA.</summary>
    Task<string> EnviarArquivoAsync(
        string html, string formId, string campoArquivo, string nomeArquivo, string contentType,
        byte[] conteudo, IReadOnlyDictionary<string, string> parametrosUrl, string? viewState,
        string operacao, CancellationToken ct);
}

public sealed class TransporteSer(ISerWebSessao sessao) : ITransporteTelaCriacao
{
    public Task<string> AbrirTelaAsync(string caminho, CancellationToken ct) => sessao.AbrirTelaAsync(caminho, ct);

    public async Task<string> SubmeterLeituraAsync(
        string html, string formId, IReadOnlyDictionary<string, string> extras, string? viewState, CancellationToken ct)
    {
        var r = await sessao.SubmeterFormAsync(html, formId, extras, viewState, ct);
        return await TelaCriacaoRedirect.SeguirAsync(r.Location, r.Texto, sessao.AbrirTelaAsync, ct);
    }

    public async Task<string> SubmeterEscritaAsync(
        string html, string formId, IReadOnlyDictionary<string, string> extras, string? viewState,
        string operacao, CancellationToken ct)
    {
        var r = await sessao.SubmeterEscritaAsync(html, formId, extras, viewState, operacao, ct);
        return await TelaCriacaoRedirect.SeguirAsync(r.Location, r.Texto, sessao.AbrirTelaAsync, ct);
    }

    public async Task<string> EnviarArquivoAsync(
        string html, string formId, string campoArquivo, string nomeArquivo, string contentType, byte[] conteudo,
        IReadOnlyDictionary<string, string> parametrosUrl, string? viewState, string operacao, CancellationToken ct)
    {
        var r = await sessao.EnviarArquivoAsync(
            html, formId, campoArquivo, nomeArquivo, contentType, conteudo, parametrosUrl, viewState, operacao, ct);
        return r.EhTexto ? r.Texto : string.Empty;
    }
}

public sealed class TransporteSernit(ISernitWebSessao sessao) : ITransporteTelaCriacao
{
    public Task<string> AbrirTelaAsync(string caminho, CancellationToken ct) => sessao.AbrirTelaAsync(caminho, ct);

    public async Task<string> SubmeterLeituraAsync(
        string html, string formId, IReadOnlyDictionary<string, string> extras, string? viewState, CancellationToken ct)
    {
        var r = await sessao.SubmeterFormAsync(html, formId, extras, viewState, ct);
        return await TelaCriacaoRedirect.SeguirAsync(r.Location, r.Texto, sessao.AbrirTelaAsync, ct);
    }

    public async Task<string> SubmeterEscritaAsync(
        string html, string formId, IReadOnlyDictionary<string, string> extras, string? viewState,
        string operacao, CancellationToken ct)
    {
        var r = await sessao.SubmeterEscritaAsync(html, formId, extras, viewState, operacao, ct);
        return await TelaCriacaoRedirect.SeguirAsync(r.Location, r.Texto, sessao.AbrirTelaAsync, ct);
    }

    public async Task<string> EnviarArquivoAsync(
        string html, string formId, string campoArquivo, string nomeArquivo, string contentType, byte[] conteudo,
        IReadOnlyDictionary<string, string> parametrosUrl, string? viewState, string operacao, CancellationToken ct) =>
        (await sessao.EnviarArquivoAsync(
            html, formId, campoArquivo, nomeArquivo, contentType, conteudo, parametrosUrl, viewState, operacao, ct)).Texto;
}

/// <summary>
/// Redirect por CABEÇALHO depois de um POST. O SERNIT (atrás de proxy) responde o submit da aba
/// Editar com 302 para <c>http://</c>, e o HttpClient não segue redirect que rebaixa https→http: o
/// corpo chega VAZIO e a aba "não abre" (medido 08/10/2026, mesma armadilha que o
/// <c>SernitNovaSolicitacaoService.AbrirEditarAsync</c> já tratava). Quem segue é o
/// <c>AbrirTelaAsync</c> da sessão, que sobe para https. Só segue quando a resposta não trouxe página.
/// </summary>
internal static class TelaCriacaoRedirect
{
    public static async Task<string> SeguirAsync(
        string? location, string texto, Func<string, CancellationToken, Task<string>> abrir, CancellationToken ct) =>
        !string.IsNullOrWhiteSpace(location) && !texto.Contains("<form", StringComparison.OrdinalIgnoreCase)
            ? await abrir(location, ct)
            : texto;
}

/// <summary>
/// O que muda entre a tela de criação do SER-RJ e a do SERNIT (medido em 08/10/2026 na aba real
/// do SERNIT, <c>Automais.SERNIT/docs/APRENDIZADOS.md</c> §5.6):
/// <list type="bullet">
/// <item>o SERNIT não tem o combo "É ambulatório estadual?" (<see cref="TemRamo"/>);</item>
/// <item>o CNS do paciente é <c>form0:numeroCNS</c> no SERNIT e <c>form0:numeroCADSUS</c> no SER;</item>
/// <item>os campos dinâmicos e o painel do paciente têm leitores próprios (marcação diferente);</item>
/// <item>no SERNIT o CPF é obrigatório para gravar (<see cref="CpfObrigatorio"/>);</item>
/// <item>a pesquisa de paciente do SERNIT só aceita CNS — CPF volta "CNS INVÁLIDO"
///   (<see cref="PesquisaPorCpf"/>);</item>
/// <item>o SERNIT NÃO consulta o CADSUS: só acha quem já está na base dele. Paciente novo volta com
///   o painel vazio e aberto, para digitar — é o que <see cref="CadastraPacienteNaTela"/> diz
///   (medido 08/10/2026: 6 de 6 pacientes de Maricá sem pedido no SERNIT vieram assim).</item>
/// </list>
/// Botões (Pesquisar, Anexar Arquivo, Gravar) NÃO estão aqui: o motor os acha pelo rótulo, seja
/// <c>&lt;a&gt;</c> (SER) seja <c>&lt;input value&gt;</c> (SERNIT).
/// </summary>
public sealed record PerfilTelaCriacao(
    string Sistema,
    string CaminhoTela,
    bool TemRamo,
    string CampoCnsCpf,
    bool CpfObrigatorio,
    bool PesquisaPorCpf,
    bool CadastraPacienteNaTela,
    Func<IHtmlDocument, string, List<SerOpcaoDto>> Combo,
    Func<string, List<SerCampoPacienteDto>> Paciente,
    Func<string, List<SerCampoDinamicoDto>> CamposDinamicos)
{
    public static readonly PerfilTelaCriacao Ser = new(
        "SER",
        SerNovaSolicitacaoService.CaminhoTela,
        TemRamo: true,
        CampoCnsCpf: "form0:numeroCADSUS",
        CpfObrigatorio: false,
        PesquisaPorCpf: true,
        CadastraPacienteNaTela: false,
        SerNovaSolicitacaoService.Combo,
        SerNovaSolicitacaoService.CamposDoPaciente,
        SerNovaSolicitacaoService.CamposDinamicos);

    public static readonly PerfilTelaCriacao Sernit = new(
        "SERNIT",
        SernitNovaSolicitacaoService.CaminhoTela,
        TemRamo: false,
        CampoCnsCpf: "form0:numeroCNS",
        CpfObrigatorio: true,
        PesquisaPorCpf: false,
        CadastraPacienteNaTela: true,
        (doc, nome) => [.. SernitNovaSolicitacaoService.Combo(doc, nome).Select(Opcao)],
        html => [.. SernitNovaSolicitacaoService.CamposDoPaciente(html).Select(c =>
            new SerCampoPacienteDto(c.Campo, c.Rotulo, c.Valor, c.Tipo, c.Obrigatorio, c.Editavel,
                c.Opcoes is null ? null : [.. c.Opcoes.Select(Opcao)]))],
        html => [.. SernitNovaSolicitacaoService.CamposDinamicos(html).Select(c =>
            new SerCampoDinamicoDto(c.Numero, c.Campo, c.Rotulo, c.Tipo, c.Obrigatorio,
                c.Opcoes is null ? null : [.. c.Opcoes.Select(Opcao)]))]);

    private static SerOpcaoDto Opcao(SernitOpcaoDto o) => new(o.Valor, o.Rotulo);
}
