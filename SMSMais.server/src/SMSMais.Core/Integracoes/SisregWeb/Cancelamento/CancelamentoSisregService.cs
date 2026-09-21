using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SMSMais.Core.Integracoes.SisregWeb.Cancelamento;

/// <summary>Como terminou a tentativa de cancelar no SISREG.</summary>
public enum ResultadoCancelamentoSisreg
{
    /// <summary>Cancelado, e a ficha relida confirma.</summary>
    Cancelado = 1,

    /// <summary>Já estava cancelado antes de tentarmos — nada foi enviado.</summary>
    JaEstavaCancelado = 2,

    /// <summary>Não deu. O cancelamento LOCAL continua valendo; alguém precisa fazer no navegador.</summary>
    Falhou = 3,
}

/// <param name="SituacaoAntes">"SOLICITAÇÃO / AUTORIZADA / REGULADOR", como a ficha mostrava.</param>
/// <param name="SituacaoDepois">O que a ficha passou a dizer — é esta a prova, não o HTTP 200.</param>
/// <param name="Detalhe">Mensagem do SISREG ou o motivo da desistência, para a trilha e para a tela.</param>
public sealed record CancelamentoSisregDto(
    ResultadoCancelamentoSisreg Resultado,
    string? SituacaoAntes,
    string? SituacaoDepois,
    string? Detalhe);

public interface ICancelamentoSisregService
{
    /// <summary>
    /// Cancela a marcação no SISREG. Nunca lança por falha do SISREG: devolve
    /// <see cref="ResultadoCancelamentoSisreg.Falhou"/> — quem chama já cancelou localmente e
    /// não pode ser desfeito por isso.
    /// </summary>
    Task<CancelamentoSisregDto> CancelarAsync(
        ISisregWebSessao sessao, string codigoSolicitacao, string cns, string justificativa,
        CancellationToken ct = default);
}

/// <summary>
/// Cancela no SISREG pela tela "Consulta de Autorização/Cancelamento" (<c>cons_verificar</c>),
/// reproduzindo exatamente o que o navegador faz. O formulário foi lido de uma captura REAL da
/// extensão — nenhuma requisição foi gasta para descobri-lo — e validado em produção em
/// 20/09/2026.
///
/// <para><b>Três passos, e o terceiro é o que vale:</b> lê a ficha (já está cancelada? então não
/// manda nada), lista para achar a linha, cancela, e <b>relê a ficha</b>. A confirmação nunca é o
/// HTTP 200 nem o <c>alert()</c> da resposta: a primeira versão desta rotina, no laboratório,
/// conferia "sumiu da listagem" e chegou a aprovar uma solicitação que ninguém havia tocado — a
/// listagem só mostra o que ainda é cancelável, então "sumiu" confunde "eu cancelei", "já estava
/// cancelado" e "a sessão veio cega".</para>
///
/// <para><b>Nunca adivinhar o índice.</b> O campo é <c>chk_N</c>, em que N é a POSIÇÃO da linha na
/// página e o valor é o CÓDIGO da solicitação. Não se sabe qual dos dois o CGI obedece, e errar
/// cancelaria o exame de outra pessoa. Por isso a linha é procurada no HTML e usa-se o nome e o
/// valor exatos daquele checkbox — aí as duas hipóteses dão no mesmo resultado.</para>
///
/// <para><b>Quem assina é a atendente</b>, não o robô: a sessão vem do
/// <c>ISisregSessaoOperadorStore</c>, autenticada com o login dela no SISREG. A credencial
/// cadastrada no sistema é de SINCRONISMO e não cancela nada — o SISREG carimba a coluna
/// "Operador" com quem fez, e essa trilha não pode sair toda no nome da mesma pessoa.</para>
/// </summary>
public sealed class CancelamentoSisregService(
    ILogger<CancelamentoSisregService> logger) : ICancelamentoSisregService
{
    private const string TelaCancelamento = "/cgi-bin/cons_verificar";
    private const string TelaFicha = "/cgi-bin/cons_marcados_reg";

    /// <summary>O SISREG recusa justificativa vazia e corta em 200 caracteres.</summary>
    private const int MaxJustificativa = 200;

    /// <summary>Páginas da listagem daquele CNS — teto de sanidade, não expectativa.</summary>
    private const int MaxPaginas = 12;

    public async Task<CancelamentoSisregDto> CancelarAsync(
        ISisregWebSessao sessao, string codigoSolicitacao, string cns, string justificativa,
        CancellationToken ct = default)
    {
        var codigo = (codigoSolicitacao ?? string.Empty).Trim();
        cns = (cns ?? string.Empty).Trim();
        justificativa = (justificativa ?? string.Empty).Trim();

        if (codigo.Length == 0 || cns.Length == 0)
            return new(ResultadoCancelamentoSisreg.Falhou, null, null,
                "Sem código de solicitação ou sem CNS do paciente — a busca no SISREG é por CNS.");
        if (justificativa.Length == 0)
            justificativa = "Cancelado pela unidade";
        if (justificativa.Length > MaxJustificativa)
            justificativa = justificativa[..MaxJustificativa];

        try
        {
            // 1. Em que estado está HOJE? Tentar cancelar o que já está cancelado gasta requisição
            //    do orçamento anti-robô e polui a ficha do paciente com justificativa repetida.
            var antes = await SituacaoAsync(sessao, codigo, ct);
            if (antes is not null && antes.Contains("CANCELAD", StringComparison.OrdinalIgnoreCase))
                return new(ResultadoCancelamentoSisreg.JaEstavaCancelado, antes, antes,
                    "Já estava cancelada no SISREG.");

            // 2. Achar a linha — nunca inventá-la.
            var alvo = await ProcurarLinhaAsync(sessao, cns, codigo, ct);
            if (alvo is null)
                return new(ResultadoCancelamentoSisreg.Falhou, antes, null,
                    "A solicitação não apareceu na listagem de canceláveis desse CNS.");

            // 3. Cancelar.
            var resposta = await sessao.PostFormAsync(TelaCancelamento, new Dictionary<string, string>
            {
                ["pg"] = "",
                ["cns"] = cns,
                [alvo.Value.Campo] = alvo.Value.Valor,
                ["etapa"] = "EXCLUIR_SOLICITACAO",
                ["ordem"] = "",
                ["total"] = "",
                ["co_solic"] = "",
                ["dt_final"] = "",
                ["nr_pagina"] = alvo.Value.Pagina.ToString(),
                ["dt_inicial"] = "",
                ["justificativa"] = justificativa,
                ["codigo_solicitacao"] = "",
            }, ct);

            // 4. A PROVA. A resposta traz todos os alert() de validação do JavaScript da página,
            //    então o primeiro deles não diz nada sobre o desfecho — foi o que me fez ler
            //    "Preencha a Data Inicial" num cancelamento que tinha dado certo.
            var depois = await SituacaoAsync(sessao, codigo, ct);
            var ok = depois is not null && depois.Contains("CANCELAD", StringComparison.OrdinalIgnoreCase);

            if (ok)
            {
                logger.LogInformation(
                    "SISREG_CANCELADO: solicitação {Codigo} — {Antes} → {Depois}.", codigo, antes, depois);
                return new(ResultadoCancelamentoSisreg.Cancelado, antes, depois, null);
            }

            logger.LogWarning(
                "SISREG_CANCELAMENTO_NAO_CONFIRMADO: solicitação {Codigo} continua {Depois} depois do "
                + "envio (alerta: {Alerta}). Tratando como NÃO cancelada.",
                codigo, depois ?? "(ilegível)", Alerta(resposta));

            return new(ResultadoCancelamentoSisreg.Falhou, antes, depois,
                depois is null
                    ? "O SISREG aceitou o envio, mas não consegui reler a ficha para confirmar."
                    : $"O SISREG aceitou o envio, mas a ficha continua {depois}.");
        }
        catch (Exception ex)
        {
            // Falha do SISREG não pode derrubar o cancelamento local: a atendente já disse ao
            // paciente que cancelou, e desfazer isso seria pior.
            logger.LogError(ex, "Falha ao cancelar a solicitação {Codigo} no SISREG.", codigo);
            return new(ResultadoCancelamentoSisreg.Falhou, null, null, ex.Message);
        }
    }

    /// <summary>
    /// A situação da ficha, aberta DIRETO pelo código — sem passar pela listagem.
    ///
    /// <para>Duas telas mostram a mesma ficha e o perfil do operador decide qual abre; tenta as
    /// duas. E tenta duas voltas: a ficha às vezes responde com a página montada e o miolo vazio,
    /// o mesmo sintoma de sessão disputada. Como aqui a resposta É a prova, insistir é barato
    /// perto de concluir errado.</para>
    /// </summary>
    private static async Task<string?> SituacaoAsync(
        ISisregWebSessao sessao, string codigo, CancellationToken ct)
    {
        for (var volta = 0; volta < 2; volta++)
        {
            var html = await sessao.PostFormAsync(TelaFicha, new Dictionary<string, string>
            {
                ["etapa"] = "EXIBIR_FICHA",
                ["co_solicitacao"] = codigo,
            }, ct);
            if (FichaCancelamentoHtmlParser.Situacao(html, codigo) is { } s) return s;

            html = await sessao.GetAsync(TelaFicha.Replace("cons_marcados_reg", "gerenciador_solicitacao"),
                new Dictionary<string, string>
                {
                    ["etapa"] = "VISUALIZAR_FICHA",
                    ["co_seq_solicitacao"] = codigo,
                }, ct);
            if (FichaCancelamentoHtmlParser.Situacao(html, codigo) is { } s2) return s2;
        }
        return null;
    }

    /// <summary>Varre as páginas daquele CNS até achar a linha do código.</summary>
    private static async Task<(string Campo, string Valor, int Pagina)?> ProcurarLinhaAsync(
        ISisregWebSessao sessao, string cns, string codigo, CancellationToken ct)
    {
        string? total = null, ordem = null;
        for (var pagina = 0; pagina < MaxPaginas; pagina++)
        {
            var html = await sessao.PostFormAsync(TelaCancelamento, new Dictionary<string, string>
            {
                ["pg"] = pagina.ToString(),
                ["cns"] = cns,
                ["etapa"] = "LISTAR",
                ["ordem"] = pagina == 0 ? "" : ordem ?? "1",
                ["total"] = pagina == 0 ? "" : total ?? "",
                ["co_solic"] = "",
                ["dt_final"] = "",
                ["nr_pagina"] = pagina == 0 ? "" : (pagina - 1).ToString(),
                ["dt_inicial"] = "",
                ["codigo_solicitacao"] = "",
            }, ct);

            if (FichaCancelamentoHtmlParser.Checkbox(html, codigo) is { } achado)
                return (achado.Campo, achado.Valor, pagina);

            (total, ordem) = FichaCancelamentoHtmlParser.TotalEOrdem(html);
            if (!html.Contains("chk_", StringComparison.OrdinalIgnoreCase)) break;
        }
        return null;
    }

    private static string? Alerta(string html)
    {
        var m = Regex.Match(html, @"alert\s*\(\s*['""](.{0,160}?)['""]\s*\)", RegexOptions.Singleline);
        return m.Success ? m.Groups[1].Value.Trim() : null;
    }
}
