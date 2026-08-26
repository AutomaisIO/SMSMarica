using System.Globalization;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;
using SMSMais.Core.Integracoes.SisregWeb.Varredura;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Background;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Sigtap;

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao.AgendaPontual;

/// <summary>O que o botão "Importar" da tela de mapeamento manda.</summary>
public sealed record ImportarAgendaPontualRequest(
    string Cpf, string CodigoProcedimento, DateOnly DataInicio, DateOnly DataFim);

/// <summary>Resumo do que a importação pontual fez — vira o modal de resultado.</summary>
public sealed record ImportacaoAgendaPontualResultado(
    DateOnly Inicio,
    DateOnly Fim,
    int Requisicoes,
    int TotalEncontrados,
    int Importados,
    int JaExistiam,
    int Pendencias,
    string Mensagem);

public interface IImportacaoAgendaPontualService
{
    Task<ImportacaoAgendaPontualResultado> ImportarAsync(
        ImportarAgendaPontualRequest request, CancellationToken ct);
}

/// <summary>
/// Importação PONTUAL da agenda de UM profissional × procedimento, por consulta direta ao
/// <c>cons_agendas</c> (paginado), para o momento em que o operador não pode esperar a varredura
/// noturna — que usa o <c>expo_solicitacoes</c>, bloqueado pelo SISREG das 8h às 15h.
///
/// <para>É <b>síncrono</b> (o operador vê o resultado na hora) e escrito para custar pouco: escopo
/// de um par e de um intervalo curto (o dia). Cada agendamento vira <see cref="MarcacaoSisreg"/> e
/// entra pelo <b>mesmo</b> núcleo de importação da varredura (<see
/// cref="IImportacaoSisregService.ImportarMarcacoesAsync"/>): mesma resolução de paciente
/// (CNS→CPF), mesma criação de unidade por CNES, mesma idempotência por nº de solicitação e a
/// mesma pendência de 1ª classe quando falta SIGTAP.</para>
///
/// <para><b>SIGTAP:</b> o <c>cons_agendas</c> não informa SIGTAP nem <c>pa</c> por linha, só o NOME
/// do procedimento. Resolvemos o SIGTAP por <b>nome exato</b> contra o catálogo (<see
/// cref="IMapeadorSigtapSisreg.ResolverPorNomeExatoAsync"/>) — que é o eixo do procedimento. O que
/// não casar vira pendência de SIGTAP, exatamente como na varredura, e é coberto pela varredura
/// noturna do <c>expo</c> (que traz o SIGTAP na coluna 2) ou pelo mapeamento manual.</para>
/// </summary>
public sealed class ImportacaoAgendaPontualService(
    ISisregUnidadeAtual unidadeAtual,
    ISisregWebSessao sessao,
    IImportacaoSisregService importacao,
    IMapeadorSigtapSisreg mapeadorSigtap,
    VarreduraSisregEstadoVivo varreduraEstadoVivo,
    SisregImportacaoEstadoVivo importacaoEstadoVivo,
    ILogger<ImportacaoAgendaPontualService> logger) : IImportacaoAgendaPontualService
{
    private const string Caminho = "/cgi-bin/cons_agendas";

    /// <summary>Guarda contra varredura infinita por bug de paginação — um dia de um par jamais
    /// chega perto disto (50 por página).</summary>
    private const int MaxPaginas = 40;

    public async Task<ImportacaoAgendaPontualResultado> ImportarAsync(
        ImportarAgendaPontualRequest request, CancellationToken ct)
    {
        var cpf = new string([.. (request.Cpf ?? string.Empty).Where(char.IsDigit)]);
        var pa = (request.CodigoProcedimento ?? string.Empty).Trim();
        if (cpf.Length == 0)
            throw new ValidacaoException("agenda_pontual.cpf_obrigatorio", "Informe o profissional (CPF).");
        if (pa.Length == 0)
            throw new ValidacaoException("agenda_pontual.procedimento_obrigatorio", "Informe o procedimento.");

        if (request.DataInicio > request.DataFim)
            throw new ValidacaoException(
                "agenda_pontual.periodo_invalido", "A data inicial não pode ser depois da data final.");

        var dias = request.DataFim.DayNumber - request.DataInicio.DayNumber;
        if (dias > VarreduraSisregOpcoes.MaxDiasAFrente)
            throw new ValidacaoException(
                "agenda_pontual.periodo_longo",
                $"O período não pode passar de {VarreduraSisregOpcoes.MaxDiasAFrente} dias — o SISREG "
                + "recusa consulta com intervalo maior. Importe em partes para cobrir um período maior.");

        var unidade = await unidadeAtual.ObterObrigatoriaAsync(ct);
        var cnes = new string([.. (unidade.Cnes ?? string.Empty).Where(char.IsDigit)]);
        if (cnes.Length == 0)
            throw new ValidacaoException(
                "agenda_pontual.unidade_sem_cnes",
                $"A unidade '{unidade.Nome}' não tem CNES cadastrado — sem ele não dá para consultar a agenda.");

        // A varredura e a importação por arquivo falam com o SISREG pela MESMA sessão (uma por
        // operador). Rodar a importação pontual junto atropelaria a outra na mesma saída.
        if (varreduraEstadoVivo.ObterAtual() is not null)
            throw new ConflitoException(
                "agenda_pontual.varredura_em_andamento",
                "Há uma varredura da agenda em andamento. Espere ela terminar para importar pontualmente.");
        if (importacaoEstadoVivo.ObterAtual() is not null)
            throw new ConflitoException(
                "agenda_pontual.importacao_em_andamento",
                "Há uma importação de arquivo em andamento. Espere ela terminar para importar pontualmente.");

        var ctx = new ConsAgendasParser.Contexto(cnes, unidade.Nome, cpf, null);

        // Página 0 primeiro para saber o total; depois as demais. Dedup por nº de solicitação —
        // varrer por um código de GRUPO pode repetir a mesma solicitação entre páginas.
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var marcacoes = new List<MarcacaoSisreg>();
        var requisicoes = 0;

        var html = await ConsultarPaginaAsync(cnes, cpf, pa, request.DataInicio, request.DataFim, 0, ct);
        requisicoes++;

        if (ConsAgendasParser.SemResultado(html))
        {
            return new ImportacaoAgendaPontualResultado(
                request.DataInicio, request.DataFim, requisicoes, 0, 0, 0, 0,
                "Nenhum agendamento no período para esse profissional e procedimento.");
        }

        void Coletar(string pagina)
        {
            foreach (var m in ConsAgendasParser.Parse(pagina, ctx))
                if (vistos.Add(m.CodigoSolicitacao)) marcacoes.Add(m);
        }
        Coletar(html);

        var totalPaginas = Math.Min(ConsAgendasParser.TotalPaginas(html), MaxPaginas);
        for (var pagina = 1; pagina < totalPaginas; pagina++)
        {
            ct.ThrowIfCancellationRequested();
            html = await ConsultarPaginaAsync(cnes, cpf, pa, request.DataInicio, request.DataFim, pagina, ct);
            requisicoes++;
            Coletar(html);
        }

        // SIGTAP por NOME (o eixo). O que não resolver entra com SIGTAP nulo e o gate do
        // ImportarMarcacoesAsync o transforma em pendência acionável — não em lixo silencioso.
        var resolvidas = new List<MarcacaoSisreg>(marcacoes.Count);
        foreach (var m in marcacoes)
        {
            var sig = await mapeadorSigtap.ResolverPorNomeExatoAsync(m.ProcedimentoTexto, ct);
            resolvidas.Add(sig is null ? m : m with { CodigoSigtap = sig });
        }

        // Mesmo ponto de injeção da varredura. O execucaoId agrupa eventuais pendências desta
        // importação (a coluna não tem FK — não precisa de linha de execução).
        var execucaoId = Guid.CreateVersion7();
        var resultado = await importacao.ImportarMarcacoesAsync(execucaoId, resolvidas, ct);

        var importados = Math.Max(0, resultado.Validos - resultado.JaExistiam);
        logger.LogInformation(
            "SISREG_IMPORT_PONTUAL: unidade {Unidade} cpf {Cpf} pa {Pa} {Ini}..{Fim} — {Req} req, "
            + "{Total} agendamentos, {Novos} novos, {JaExistiam} já existiam, {Pend} pendências.",
            unidade.Nome, cpf, pa, request.DataInicio, request.DataFim, requisicoes,
            resolvidas.Count, importados, resultado.JaExistiam, resultado.Invalidos);

        var mensagem = MontarMensagem(resolvidas.Count, importados, resultado.JaExistiam, resultado.Invalidos);
        return new ImportacaoAgendaPontualResultado(
            request.DataInicio, request.DataFim, requisicoes, resolvidas.Count,
            importados, resultado.JaExistiam, resultado.Invalidos, mensagem);
    }

    private async Task<string> ConsultarPaginaAsync(
        string cnes, string cpf, string pa, DateOnly inicio, DateOnly fim, int pagina, CancellationToken ct) =>
        await sessao.PostFormAsync(Caminho, new Dictionary<string, string>
        {
            ["co_solicitacao"] = string.Empty,
            ["cns_paciente"] = string.Empty,
            // Cultura invariante: a "/" em "dd/MM/yyyy" é separador da cultura, não literal.
            ["dataInicial"] = inicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["dataFinal"] = fim.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["ups"] = cnes,
            ["cpf"] = cpf,
            ["pa"] = pa,
            ["cmbTipoOperacao"] = "Consulta",
            ["chkboxExibirProcedimentos"] = "on",
            ["chkboxExibirTelefones"] = "on",
            ["cmbOrdenacao"] = "1",
            ["cmbMaxResults"] = "50",
            ["etapa"] = "ListaConsulta", // LEITURA — nunca Confirma/Falta.
            ["pagina"] = pagina.ToString(CultureInfo.InvariantCulture),
            ["linhas"] = "0",
        }, ct);

    private static string MontarMensagem(int total, int importados, int jaExistiam, int pendencias)
    {
        if (total == 0) return "Nenhum agendamento no período.";
        var partes = new List<string> { $"{total} agendamento(s) encontrado(s)" };
        if (importados > 0) partes.Add($"{importados} importado(s)");
        if (jaExistiam > 0) partes.Add($"{jaExistiam} já existia(m)");
        if (pendencias > 0) partes.Add($"{pendencias} em pendência (procedimento sem SIGTAP mapeado)");
        return string.Join(" · ", partes) + ".";
    }
}
