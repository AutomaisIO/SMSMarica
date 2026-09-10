using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Tempo;
using SMSMais.Data;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Fila;

/// <summary>Resultado de uma janela lida.</summary>
/// <param name="Saidas">Quem estava aberto nesta janela e não veio na leitura — saiu da fila.</param>
public sealed record LeituraFilaDto(
    DateOnly Inicio,
    DateOnly Fim,
    int Lidas,
    int Novas,
    int Atualizadas,
    int Saidas,
    int SaidasAgendadas,
    int Requisicoes);

public interface IFilaPendenteSisregService
{
    /// <summary>Lê uma janela de <b>no máximo 31 dias</b> por data de solicitação. 1 requisição.</summary>
    Task<LeituraFilaDto> LerJanelaAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>A janela recente — o que o diário roda.</summary>
    Task<LeituraFilaDto> SincronizarRecenteAsync(CancellationToken ct = default);
}

/// <summary>
/// Lê a <b>fila de espera</b> do SISREG: quem pediu e ainda não foi agendado.
///
/// <para><b>Somente leitura</b> (ADR-0012). O <c>gerenciador_solicitacao</c> não escreve nada; e,
/// ao contrário do <c>expo_solicitacoes</c>, <b>não sofre a trava 07:30–15:00</b>.</para>
///
/// <para><b>1 requisição por janela.</b> <c>qtd_itens_pag=0</c> significa "todos" e é honrado pelo
/// servidor — 15.502 registros em 13,4 MB numa requisição só, medido pelo laboratório em
/// 05/09/2026. Não há teto silencioso aqui (ao contrário do <c>expo</c>, que corta em 700 sem
/// avisar): 3.669 em 7 dias × 31/7 ≈ 16,2k contra 15,5k medidos em 31 dias — a aritmética fecha.</para>
///
/// <para><b>Em Maricá a fila mora na situação 1</b> (Solicitação/Pendente/Regulação), não na 2
/// ("Fila de Espera"), que volta vazia em toda janela testada. Sondar só a 2 — o nome óbvio —
/// daria a conclusão errada de que não há fila.</para>
/// </summary>
public sealed class FilaPendenteSisregService(
    SmsMaisDbContext db,
    ISisregWebSessao sessao,
    SisregOrcamentoRequisicoes orcamento,
    IOptions<SisregOrcamentoOpcoes> orcamentoOpcoes,
    ILogger<FilaPendenteSisregService> logger) : IFilaPendenteSisregService
{
    private const string Caminho = "/cgi-bin/gerenciador_solicitacao";

    /// <summary>O <c>validaFormulario()</c> do SISREG recusa janela maior que isto.</summary>
    public const int MaxDiasPorJanela = 31;

    /// <summary>Janela do sincronismo diário. 31 dias porque custa o mesmo que 7: uma requisição.</summary>
    private const int DiasDaJanelaRecente = 31;

    /// <summary>
    /// Folga exigida no orçamento anti-robô para começar. A leitura gasta 1, mas entrar com o
    /// orçamento no fim é tomar a frente de um operador humano que está a poucas requisições do
    /// CAPTCHA — e CAPTCHA pausa a credencial por 24 horas.
    /// </summary>
    private const int OrcamentoMinimo = 20;

    public Task<LeituraFilaDto> SincronizarRecenteAsync(CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        return LerJanelaAsync(hoje.AddDays(-(DiasDaJanelaRecente - 1)), hoje, ct);
    }

    public async Task<LeituraFilaDto> LerJanelaAsync(
        DateOnly inicio, DateOnly fim, CancellationToken ct = default)
    {
        if (inicio > fim)
        {
            throw new Common.Excecoes.ValidacaoException(
                "fila.periodo_invalido", "A data inicial não pode ser depois da data final.");
        }

        var dias = fim.DayNumber - inicio.DayNumber + 1;
        if (dias > MaxDiasPorJanela)
        {
            throw new Common.Excecoes.ValidacaoException(
                "fila.periodo_longo",
                $"A janela não pode passar de {MaxDiasPorJanela} dias — o SISREG recusa. "
                + "Leia em partes.");
        }

        var restante = orcamento.Restante(orcamentoOpcoes.Value.TetoAutomatico);
        if (restante < OrcamentoMinimo)
        {
            throw new Common.Excecoes.ConflitoException(
                "fila.orcamento_curto",
                $"Só restam {restante} requisições no orçamento desta hora. A leitura da fila fica "
                + "para depois, para não tomar a frente de quem está atendendo.");
        }

        var html = await sessao.GetAsync(Caminho, Consulta(inicio, fim), ct);
        var linhas = FilaPendenteHtmlParser.Ler(html);

        logger.LogInformation(
            "SISREG_FILA: janela {Ini}..{Fim} devolveu {Qtd} pendente(s) em 1 requisição.",
            inicio, fim, linhas.Count);

        var (novas, atualizadas) = await GravarAsync(linhas, ct);
        var (saidas, agendadas) = await MarcarSaidasAsync(inicio, fim, linhas, ct);

        return new LeituraFilaDto(inicio, fim, linhas.Count, novas, atualizadas, saidas, agendadas, 1);
    }

    /// <summary>Os campos exatamente como o formulário os envia (<c>METHOD=GET</c>).</summary>
    private static Dictionary<string, string> Consulta(DateOnly inicio, DateOnly fim) => new()
    {
        ["etapa"] = "LISTAR_SOLICITACOES",
        ["co_solicitacao"] = "",
        ["cns_paciente"] = "",
        ["no_usuario"] = "",
        ["cnes_solicitante"] = "",
        ["cnes_executante"] = "",
        ["co_proc_unificado"] = "",
        ["co_pa_interno"] = "",
        ["ds_procedimento"] = "",
        // S = por data de SOLICITACAO. É o eixo certo: a fila é "quem pediu e ainda espera".
        ["tipo_periodo"] = "S",
        ["dt_inicial"] = inicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        ["dt_final"] = fim.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        ["cmb_situacao"] = "1",
        // 0 = TODOS. É o que derruba o custo de N páginas para 1 requisição.
        ["qtd_itens_pag"] = "0",
        ["co_seq_solicitacao"] = "",
        ["ordenacao"] = "2",
        ["pagina"] = "0",
    };

    /// <summary>
    /// Upsert por <c>codigo_solicitacao</c>. A mesma pessoa relida amanhã tem de ATUALIZAR a
    /// linha; sem isso uma fila de 15 mil viraria meio milhão de linhas em um mês.
    /// </summary>
    private async Task<(int Novas, int Atualizadas)> GravarAsync(
        IReadOnlyList<LinhaFilaPendente> linhas, CancellationToken ct)
    {
        if (linhas.Count == 0) return (0, 0);

        var agora = DateTime.UtcNow;
        var codigos = linhas.Select(l => l.CodigoSolicitacao).ToList();

        var existentes = await db.SisregFilaPendentes
            .Where(f => codigos.Contains(f.CodigoSolicitacao))
            .ToDictionaryAsync(f => f.CodigoSolicitacao, StringComparer.Ordinal, ct);

        var novas = 0;
        var atualizadas = 0;

        foreach (var l in linhas)
        {
            if (existentes.TryGetValue(l.CodigoSolicitacao, out var f))
            {
                // Reapareceu depois de ter saído: o SISREG devolveu para a fila (devolvida,
                // reenviada). Limpar a saída é o que impede a pessoa de ficar como "atendida"
                // enquanto ainda espera.
                if (f.SaiuEm is not null)
                {
                    f.SaiuEm = null;
                    f.SaiuPara = null;
                }
                atualizadas++;
            }
            else
            {
                f = new SisregFilaPendente
                {
                    Id = Guid.CreateVersion7(),
                    CodigoSolicitacao = l.CodigoSolicitacao,
                    PrimeiroVistoEm = agora,
                    CriadoEm = agora,
                };
                db.SisregFilaPendentes.Add(f);
                novas++;
            }

            f.DataSolicitacao = l.DataSolicitacao;
            f.Risco = l.Risco;
            f.PacienteNome = l.PacienteNome;
            f.Cns = l.Cns;
            f.NomeMae = l.NomeMae;
            f.DataNascimento = l.DataNascimento;
            f.IdadeAnos = l.IdadeAnos;
            f.Telefone = l.Telefone;
            f.Municipio = l.Municipio;
            f.ProcedimentoNome = l.ProcedimentoNome;
            f.ProcedimentoCodigo = l.ProcedimentoCodigo;
            f.CidCodigo = l.CidCodigo;
            f.UnidadeSolicitante = l.UnidadeSolicitante;
            f.Situacao = l.Situacao;
            f.UltimoVistoEm = agora;
            f.AtualizadoEm = agora;
        }

        await db.SaveChangesAsync(ct);
        return (novas, atualizadas);
    }

    /// <summary>
    /// Quem estava aberto <b>dentro da janela lida</b> e não veio na leitura saiu da fila.
    ///
    /// <para><b>O recorte pela janela é o que impede o desastre.</b> Ler 31 dias e concluir "sumiu"
    /// sobre a base inteira marcaria como atendida toda pessoa que pediu fora dessa janela — e a
    /// maioria da fila pediu antes. É a mesma lição que o detector de ausentes da agenda pagou:
    /// ausência só significa alguma coisa onde a leitura de fato aconteceu.</para>
    ///
    /// <para><b>Leitura vazia não conclui nada.</b> Zero linhas é indistinguível de sessão caída ou
    /// SISREG fora do ar; marcar a janela inteira como "saiu" seria transformar uma falha de rede
    /// em "todo mundo foi atendido".</para>
    /// </summary>
    private async Task<(int Saidas, int Agendadas)> MarcarSaidasAsync(
        DateOnly inicio, DateOnly fim, IReadOnlyList<LinhaFilaPendente> linhas, CancellationToken ct)
    {
        if (linhas.Count == 0)
        {
            logger.LogWarning(
                "SISREG_FILA_VAZIA: janela {Ini}..{Fim} não devolveu linha nenhuma — nada é marcado "
                + "como saída. Leitura vazia é indistinguível de sessão caída.",
                inicio, fim);
            return (0, 0);
        }

        var vistos = linhas.Select(l => l.CodigoSolicitacao).ToHashSet(StringComparer.Ordinal);

        var abertas = await db.SisregFilaPendentes
            .Where(f => f.SaiuEm == null
                && f.DataSolicitacao != null
                && f.DataSolicitacao >= inicio
                && f.DataSolicitacao <= fim)
            .ToListAsync(ct);

        var sumiram = abertas.Where(f => !vistos.Contains(f.CodigoSolicitacao)).ToList();
        if (sumiram.Count == 0) return (0, 0);

        // Para onde foram, SEM gastar requisição: o código é a mesma chave dos dois lados.
        var codigos = sumiram.Select(f => f.CodigoSolicitacao).ToList();
        var agendadasNoBanco = await db.Solicitacoes
            .Where(s => s.CodigoSolicitacao != null
                && codigos.Contains(s.CodigoSolicitacao)
                && s.ExcluidoEm == null
                && s.DataAgendada != null)
            .Select(s => s.CodigoSolicitacao!)
            .ToListAsync(ct);

        var agendadas = agendadasNoBanco.ToHashSet(StringComparer.Ordinal);
        var agora = DateTime.UtcNow;

        foreach (var f in sumiram)
        {
            f.SaiuEm = agora;
            f.SaiuPara = agendadas.Contains(f.CodigoSolicitacao)
                ? SaidaDaFilaSisreg.Agendada
                : SaidaDaFilaSisreg.SaiuSemAgendar;
            f.AtualizadoEm = agora;
        }

        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "SISREG_FILA_SAIDAS: {Total} saíram da fila na janela {Ini}..{Fim} — {Ag} agendadas, "
            + "{Sem} sem agendamento no nosso banco.",
            sumiram.Count, inicio, fim, agendadas.Count, sumiram.Count - agendadas.Count);

        return (sumiram.Count, agendadas.Count);
    }
}
