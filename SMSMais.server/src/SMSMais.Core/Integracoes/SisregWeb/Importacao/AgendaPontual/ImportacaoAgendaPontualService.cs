using System.Globalization;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;
using SMSMais.Core.Integracoes.SisregWeb.Varredura;
using SMSMais.Core.Integracoes.SisregWeb.Varredura.Background;

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
/// (CNS→CPF), mesma criação de unidade por CNES e mesma idempotência/reconciliação por nº.</para>
///
/// <para><b>SIGTAP é IGNORADO.</b> O <c>cons_agendas</c> não informa SIGTAP nem <c>pa</c> por linha,
/// só o NOME do procedimento — e o eixo do catálogo é justamente o NOME do SISREG. A solicitação é
/// cadastrada local pelo nome (categoria por nome; o <c>TipoExame</c> nasce sozinho pelo nome se
/// ainda não existir), sem depender de SIGTAP. A correlação SIGTAP é trabalho de faturamento,
/// associada DEPOIS — nunca barra a importação.</para>
/// </summary>
public sealed class ImportacaoAgendaPontualService(
    ISisregUnidadeAtual unidadeAtual,
    ISisregWebSessao sessao,
    IImportacaoSisregService importacao,
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

        // SIGTAP é IGNORADO aqui: o cons_agendas não informa código, e o eixo do catálogo é o NOME
        // do SISREG (a correlação SIGTAP é do faturamento, à parte). O núcleo cadastra local pelo
        // nome — categoria por nome, TipoExame criado pelo nome se ainda não existe.
        //
        // Mesmo ponto de injeção da varredura. O execucaoId agrupa eventuais pendências desta
        // importação (a coluna não tem FK — não precisa de linha de execução).
        var execucaoId = Guid.CreateVersion7();
        var resultado = await importacao.ImportarMarcacoesAsync(execucaoId, marcacoes, ct);

        var importados = Math.Max(0, resultado.Validos - resultado.JaExistiam);
        logger.LogInformation(
            "SISREG_IMPORT_PONTUAL: unidade {Unidade} cpf {Cpf} pa {Pa} {Ini}..{Fim} — {Req} req, "
            + "{Total} agendamentos, {Novos} novos, {JaExistiam} já existiam, {Pend} pendências.",
            unidade.Nome, cpf, pa, request.DataInicio, request.DataFim, requisicoes,
            marcacoes.Count, importados, resultado.JaExistiam, resultado.Invalidos);

        var mensagem = MontarMensagem(marcacoes.Count, importados, resultado.JaExistiam, resultado.Invalidos);
        return new ImportacaoAgendaPontualResultado(
            request.DataInicio, request.DataFim, requisicoes, marcacoes.Count,
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
