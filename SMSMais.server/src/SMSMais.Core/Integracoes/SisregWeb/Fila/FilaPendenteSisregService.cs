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

/// <summary>O retrato da fila que já está no banco. Não fala com o SISREG.</summary>
/// <param name="UltimaLeitura">Último momento em que alguém foi visto na fila; nulo = nunca lida.</param>
public sealed record ResumoFilaDto(int PessoasNaFila, DateTime? UltimaLeitura);

/// <summary>
/// O SISREG respondeu, mas não com a listagem que se pediu — ou com uma listagem que não dá para
/// levar a sério. Quem chama deve tentar de novo; nada foi gravado.
/// </summary>
public sealed class LeituraDaFilaInvalidaException(string mensagem) : Exception(mensagem);

public interface IFilaPendenteSisregService
{
    /// <summary>
    /// Lê uma janela de <b>no máximo 31 dias</b> por data de solicitação. Uma requisição por
    /// situação lida (hoje duas: pendente e reenviada).
    /// </summary>
    /// <exception cref="LeituraDaFilaInvalidaException">A resposta não é a listagem, ou a pendente
    /// voltou zerada numa janela que sabidamente tem gente. Nada é gravado.</exception>
    Task<LeituraFilaDto> LerJanelaAsync(DateOnly inicio, DateOnly fim, CancellationToken ct = default);

    /// <summary>A janela recente — o que o diário roda.</summary>
    Task<LeituraFilaDto> SincronizarRecenteAsync(CancellationToken ct = default);

    Task<ResumoFilaDto> ResumoAsync(CancellationToken ct = default);

    /// <summary>
    /// Tira da fila quem já tem agendamento importado, e corrige quem saiu rotulado "sem agendar"
    /// mas aparece agendado depois. <b>Não fala com o SISREG</b> — é o que permite ler a fila só
    /// "daqui para frente" sem ela inchar com quem já foi atendido.
    /// </summary>
    /// <returns>Quantas linhas mudaram (fechadas + corrigidas).</returns>
    Task<int> FecharAgendadosAsync(CancellationToken ct = default);
}

/// <summary>
/// Lê a <b>fila de espera</b> do SISREG: quem pediu e ainda não foi agendado.
///
/// <para><b>Somente leitura</b> (ADR-0012). O <c>gerenciador_solicitacao</c> não escreve nada; e,
/// ao contrário do <c>expo_solicitacoes</c>, <b>não sofre a trava 07:30–15:00</b>.</para>
///
/// <para><b>1 requisição por situação e janela.</b> <c>qtd_itens_pag=0</c> significa "todos" e é
/// honrado pelo servidor — 15.502 registros em 13,4 MB numa requisição só, medido pelo laboratório
/// em 05/09/2026. Não há teto silencioso aqui (ao contrário do <c>expo</c>, que corta em 700 sem
/// avisar): 3.669 em 7 dias × 31/7 ≈ 16,2k contra 15,5k medidos em 31 dias — a aritmética fecha.</para>
///
/// <para><b>Em Maricá a fila mora na situação 1</b> (Solicitação/Pendente/Regulação), não na 2
/// ("Fila de Espera"), que volta vazia em toda janela testada. Sondar só a 2 — o nome óbvio —
/// daria a conclusão errada de que não há fila.</para>
///
/// <para><b>E também na 5 (Reenviada).</b> Conferido em 10/09/2026 contra a tela do regulador
/// (Autorizar → Ambulatorial): a fila que ele trabalha é a da situação 1 — 356 × 355 e 544 × 545
/// nas duas janelas comparadas, códigos 100/100 — <b>mais</b> as reenviadas (<c>SOL/REE/REG</c>,
/// ~1%), que ficavam de fora. A situação 5 respondeu com 23 fichas em 31 dias.</para>
///
/// <para><b>Quem sai, sai sobretudo por agendamento.</b> Das 310 saídas da primeira leitura
/// (11/09/2026), 290 (94%) apareceram agendadas no nosso banco horas depois — a varredura das
/// agendas traz o agendamento com o mesmo código. Por isso o fechamento pela agenda
/// (<see cref="FecharAgendadosAsync"/>) dispensa reler o passado para a maior parte das saídas.</para>
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

    /// <summary>Janela do sincronismo diário. 31 dias porque custa o mesmo que 7.</summary>
    private const int DiasDaJanelaRecente = 31;

    /// <summary>
    /// Folga exigida no orçamento anti-robô para começar. A leitura gasta 2, mas entrar com o
    /// orçamento no fim é tomar a frente de um operador humano que está a poucas requisições do
    /// CAPTCHA — e CAPTCHA pausa a credencial por 24 horas.
    /// </summary>
    private const int OrcamentoMinimo = 20;

    /// <summary>
    /// Como o SISREG diz "a busca rodou e não achou ninguém" — conferido na captura de jul/2024.
    /// Página sem linha e sem esta frase não é listagem vazia: é outra coisa (sessão caída, erro).
    /// </summary>
    private const string MarcaDeListagemVazia = "Nenhum registro encontrado";

    /// <summary>
    /// A pendente voltar zerada numa janela onde há pelo menos isto de gente aberta não é fila
    /// esvaziada, é leitura quebrada. Em 12/09/2026 a sessão caiu às 05:30 e a pendente dos últimos
    /// 31 dias voltou com zero (contra 13.486 na véspera). Janela antiga e rala continua podendo
    /// vir vazia de verdade — por isso o limiar, e não "zero é sempre suspeito".
    /// </summary>
    private const int LimiarDeLeituraSuspeita = 50;

    /// <summary>1 = Solicitação/Pendente/Regulação — onde a fila de Maricá mora.</summary>
    private const string SituacaoPendenteRegulacao = "1";

    /// <summary>5 = Reenviada: devolvida e mandada de novo, e de volta na mesa do regulador.</summary>
    private const string SituacaoReenviada = "5";

    /// <summary>A pendente vem primeiro: é ela que decide se a leitura conclui saídas.</summary>
    private static readonly string[] SituacoesDaFila = [SituacaoPendenteRegulacao, SituacaoReenviada];

    public Task<LeituraFilaDto> SincronizarRecenteAsync(CancellationToken ct = default)
    {
        var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        return LerJanelaAsync(hoje.AddDays(-(DiasDaJanelaRecente - 1)), hoje, ct);
    }

    public async Task<ResumoFilaDto> ResumoAsync(CancellationToken ct = default)
    {
        var abertas = await db.SisregFilaPendentes.CountAsync(f => f.SaiuEm == null, ct);
        var ultima = await db.SisregFilaPendentes.MaxAsync(f => (DateTime?)f.UltimoVistoEm, ct);
        return new ResumoFilaDto(abertas, ultima);
    }

    public async Task<int> FecharAgendadosAsync(CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;

        // Aberta na fila e com agendamento importado DEPOIS da última vez que a fila a viu: saiu
        // por agendamento. Se a fila a viu depois do agendamento (cancelado, voltou para a fila),
        // a evidência mais nova vence e ela continua esperando.
        var fechadas = await db.SisregFilaPendentes
            .Where(f => f.SaiuEm == null
                && db.Solicitacoes.Any(s => s.CodigoSolicitacao == f.CodigoSolicitacao
                    && s.ExcluidoEm == null
                    && s.CanceladoEm == null
                    && s.DataAgendada != null
                    && (s.AtualizadoEm ?? s.CriadoEm) >= f.UltimoVistoEm))
            .ExecuteUpdateAsync(set => set
                .SetProperty(f => f.SaiuEm, (DateTime?)agora)
                .SetProperty(f => f.SaiuPara, (SaidaDaFilaSisreg?)SaidaDaFilaSisreg.Agendada)
                .SetProperty(f => f.AtualizadoEm, (DateTime?)agora), ct);

        // Saiu rotulada "sem agendar" porque a fila é lida antes de a agenda ser importada: das 304
        // da primeira leitura, 284 apareceram agendadas horas depois. O rótulo se corrige sozinho.
        var corrigidas = await db.SisregFilaPendentes
            .Where(f => f.SaiuPara == SaidaDaFilaSisreg.SaiuSemAgendar
                && db.Solicitacoes.Any(s => s.CodigoSolicitacao == f.CodigoSolicitacao
                    && s.ExcluidoEm == null
                    && s.DataAgendada != null))
            .ExecuteUpdateAsync(set => set
                .SetProperty(f => f.SaiuPara, (SaidaDaFilaSisreg?)SaidaDaFilaSisreg.Agendada)
                .SetProperty(f => f.AtualizadoEm, (DateTime?)agora), ct);

        if (fechadas + corrigidas > 0)
        {
            logger.LogInformation(
                "SISREG_FILA_AGENDADOS: {Fechadas} saíram da fila por agendamento; {Corrigidas} saídas "
                + "corrigidas de 'sem agendar' para 'agendada'.",
                fechadas, corrigidas);
        }

        return fechadas + corrigidas;
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

        // Uma requisição por situação. A mesma solicitação não aparece nas duas, mas o código é a
        // chave da tabela: se aparecer, fica a primeira e o upsert não tenta gravar duas vezes.
        var linhas = new List<LinhaFilaPendente>();
        var codigos = new HashSet<string>(StringComparer.Ordinal);
        var pendentes = 0;

        foreach (var situacao in SituacoesDaFila)
        {
            var html = await sessao.GetAsync(Caminho, Consulta(inicio, fim, situacao), ct);
            var lidas = FilaPendenteHtmlParser.Ler(html);

            if (lidas.Count == 0 && !html.Contains(MarcaDeListagemVazia, StringComparison.OrdinalIgnoreCase))
            {
                throw new LeituraDaFilaInvalidaException(
                    $"O SISREG não devolveu a listagem da situação {situacao} para {inicio:dd/MM/yyyy}–"
                    + $"{fim:dd/MM/yyyy} (nem linhas, nem \"{MarcaDeListagemVazia}\"). Provável sessão caída.");
            }

            if (situacao == SituacaoPendenteRegulacao) pendentes = lidas.Count;

            foreach (var l in lidas)
            {
                if (codigos.Add(l.CodigoSolicitacao)) linhas.Add(l);
            }
        }

        if (pendentes == 0)
        {
            var abertasNaJanela = await db.SisregFilaPendentes.CountAsync(f => f.SaiuEm == null
                && f.DataSolicitacao != null
                && f.DataSolicitacao >= inicio
                && f.DataSolicitacao <= fim, ct);

            if (abertasNaJanela >= LimiarDeLeituraSuspeita)
            {
                throw new LeituraDaFilaInvalidaException(
                    $"A situação pendente voltou zerada para {inicio:dd/MM/yyyy}–{fim:dd/MM/yyyy}, onde "
                    + $"há {abertasNaJanela} pessoas abertas. Leitura descartada; será repetida.");
            }
        }

        logger.LogInformation(
            "SISREG_FILA: janela {Ini}..{Fim} devolveu {Qtd} pessoa(s) na fila ({Pend} pendentes) em {Req} requisições.",
            inicio, fim, linhas.Count, pendentes, SituacoesDaFila.Length);

        var (novas, atualizadas) = await GravarAsync(linhas, ct);
        var (saidas, agendadas) = await MarcarSaidasAsync(inicio, fim, linhas, pendentes, ct);

        return new LeituraFilaDto(
            inicio, fim, linhas.Count, novas, atualizadas, saidas, agendadas, SituacoesDaFila.Length);
    }

    /// <summary>Os campos exatamente como o formulário os envia (<c>METHOD=GET</c>).</summary>
    private static Dictionary<string, string> Consulta(DateOnly inicio, DateOnly fim, string situacao) => new()
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
        ["cmb_situacao"] = situacao,
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
    /// <para><b>Leitura vazia não conclui nada — e quem decide é a situação 1.</b> Zero linhas é
    /// indistinguível de sessão caída ou SISREG fora do ar. As reenviadas são ~1% da fila: se a
    /// pendente viesse vazia e só a reenviada trouxesse gente, concluir saídas marcaria como
    /// atendidos os 99% que simplesmente não foram lidos.</para>
    /// </summary>
    private async Task<(int Saidas, int Agendadas)> MarcarSaidasAsync(
        DateOnly inicio, DateOnly fim, IReadOnlyList<LinhaFilaPendente> linhas, int pendentes,
        CancellationToken ct)
    {
        if (pendentes == 0)
        {
            logger.LogWarning(
                "SISREG_FILA_VAZIA: janela {Ini}..{Fim} não devolveu pendente nenhum — nada é marcado "
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

        // Para onde foram, SEM gastar requisição: o código é a mesma chave dos dois lados. O que
        // ainda não aparece agendado sai como "sem agendar" e é corrigido por FecharAgendadosAsync
        // quando a agenda for importada.
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
