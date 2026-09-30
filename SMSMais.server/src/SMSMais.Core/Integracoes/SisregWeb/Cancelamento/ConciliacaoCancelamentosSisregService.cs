using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.SisregWeb.Indicadores;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Sisreg;

namespace SMSMais.Core.Integracoes.SisregWeb.Cancelamento;

/// <summary>Um cancelamento como a tela do SISREG o mostra.</summary>
/// <param name="Operador">Login de quem cancelou no SISREG — a trilha de autoria.</param>
/// <param name="Justificativa">O motivo registrado lá. <b>Interno</b>: nunca vai ao paciente.</param>
public sealed record CancelamentoLidoSisreg(
    string Codigo, DateTime? CanceladoEm, string? Justificativa, string? Operador);

/// <summary>Resultado de uma passada da conciliação, para o log e para a tela de execuções.</summary>
public sealed record ConciliacaoCancelamentosDto(
    int Lidos, int Conciliados, int JaConheciamos, int ForaDaNossaBase, int Requisicoes, string? Aviso);

public interface IConciliacaoCancelamentosSisregService
{
    /// <summary>Lê os cancelamentos de um dia e aplica na nossa base o que ainda não sabíamos.</summary>
    Task<ConciliacaoCancelamentosDto> ConciliarDiaAsync(DateOnly dia, CancellationToken ct = default);
}

/// <summary>
/// Traz para a nossa base os cancelamentos feitos NO SISREG por outra pessoa — unidade executante,
/// solicitante ou regulação.
///
/// <para><b>Por que é preciso.</b> O botão de cancelar do SMSMais já cobre o que passa por nós. Mas
/// a maior parte não passa: medido em 20/09/2026, foram <b>4.795 cancelamentos em 3 meses</b>, e
/// dos que existiam na nossa base <b>628 continuavam de pé aqui</b> — vaga bloqueada para a rede,
/// paciente sendo lembrado de um agendamento que não existe mais. Sessenta e cinco mensagens
/// chegaram a sair DEPOIS de o SISREG ter cancelado.</para>
///
/// <para><b>Por que não deduzir pelo TXT.</b> A varredura noturna já percebe o sumiço e registra
/// como "Ausente" (<c>sisreg_alteracao_agenda</c>), mas é dedução: chega com mediana de 7,6 h (p90
/// de 5 dias), não sabe o motivo, não sabe quem cancelou, e tem pontos cegos por construção (pula
/// janela no passado, descarta a unidade quando mais de 20% some). Aqui o SISREG <b>afirma</b>,
/// em minutos, com motivo e operador. A dedução continua valendo como conferidor independente: o
/// que sumiu do TXT e NÃO apareceu aqui é o caso que merece um humano.</para>
///
/// <para><b>Leitura, não ação assinada.</b> Usa a credencial de sincronismo (a mesma da varredura),
/// porque não há operador humano por trás. Escrever no SISREG continua exigindo o login de quem
/// clica — o SISREG carimba a autoria e ela não pode sair toda no mesmo nome.</para>
///
/// <para><b>Nunca conclui de leitura incompleta.</b> A tela declara quantas linhas existem; se o
/// lido não bater com o declarado, a passada é descartada. Numa listagem de cancelamentos,
/// "faltou uma linha" quer dizer "um cancelamento se perdeu" — e foi exatamente o que aconteceu na
/// primeira versão do coletor do laboratório, em silêncio.</para>
/// </summary>
public sealed class ConciliacaoCancelamentosSisregService(
    ISisregWebSessao sessao,
    SmsMaisDbContext db,
    IComunicacaoPacienteService comunicacoes,
    IArmazemIndicadoresSisreg indicadores,
    ILogger<ConciliacaoCancelamentosSisregService> logger) : IConciliacaoCancelamentosSisregService
{
    private const string Tela = "/cgi-bin/cons_marcacao_cancelada";

    /// <summary>Teto de sanidade — o número real de páginas vem do rodapé da própria tela.</summary>
    private const int TetoPaginas = 200;

    /// <summary>Canal gravado na solicitação: distingue o que veio de fora do que nós cancelamos.</summary>
    public const string Canal = "sisreg-conciliacao";

    public async Task<ConciliacaoCancelamentosDto> ConciliarDiaAsync(
        DateOnly dia, CancellationToken ct = default)
    {
        var (linhas, brutas, declaradas, requisicoes, aviso) = await LerDiaAsync(dia, ct);
        if (aviso is not null)
        {
            logger.LogWarning("SISREG_CONCILIACAO_INCOMPLETA: {Aviso}", aviso);
            return new(linhas.Count, 0, 0, 0, requisicoes, aviso);
        }

        await GravarParaIndicadoresAsync(dia, brutas, declaradas, ct);
        if (linhas.Count == 0) return new(0, 0, 0, 0, requisicoes, null);

        var codigos = linhas.Select(l => l.Codigo).ToList();
        var nossas = await db.Solicitacoes
            .Include(s => s.ExameImagem)
            .Where(s => s.CodigoSolicitacao != null
                && codigos.Contains(s.CodigoSolicitacao)
                && s.ExcluidoEm == null)
            .ToListAsync(ct);

        var porCodigo = nossas.ToDictionary(s => s.CodigoSolicitacao!, StringComparer.Ordinal);
        var agora = DateTime.UtcNow;
        int conciliados = 0, jaConheciamos = 0;

        foreach (var linha in linhas)
        {
            if (!porCodigo.TryGetValue(linha.Codigo, out var s)) continue;
            if (s.Status == StatusSolicitacao.Cancelada) { jaConheciamos++; continue; }

            // O instante é o do SISREG, não "agora": a trilha tem de contar quando aconteceu, não
            // quando descobrimos.
            var quando = linha.CanceladoEm ?? agora;

            s.Status = StatusSolicitacao.Cancelada;
            s.CanceladoEm = quando;
            s.MotivoCancelamento = Motivo(linha);
            s.StatusConfirmacao = StatusConfirmacaoAgendamento.Cancelada;
            s.ConfirmacaoCanceladaEm = quando;
            s.ConfirmadoCanal = Canal;
            s.AtualizadoEm = agora;

            if (s.ExameImagem is { } exame && exame.Status is StatusSolicitacaoExame.Solicitada
                    or StatusSolicitacaoExame.Enviada or StatusSolicitacaoExame.Recebida)
            {
                exame.Status = StatusSolicitacaoExame.Cancelada;
                exame.AtualizadoEm = agora;
            }

            // A dedução do TXT sobre esta solicitação agora está explicada — sai da fila humana.
            await db.SisregAlteracoesAgenda
                .Where(a => a.SolicitacaoId == s.Id
                    && a.Tipo == TipoAlteracaoAgenda.Ausente
                    && a.TratadaEm == null)
                .ExecuteUpdateAsync(u => u.SetProperty(a => a.TratadaEm, agora), ct);

            await comunicacoes.RevogarAcessosAsync(s.Id, agora, ct);

            // Só avisa o que ainda ia acontecer: cancelamento de data passada não é notícia, é
            // arrumação de base — e mandar mensagem sobre isso confunde quem já foi (ou não foi).
            if (s.DataAgendada is { } data && data > agora)
                await comunicacoes.EnfileirarAsync(s, FinalidadeComunicacao.CancelamentoAgendamento, ct);

            conciliados++;
        }

        if (conciliados > 0) await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "SISREG_CONCILIACAO {Dia}: {Lidos} cancelamento(s) na rede, {Conciliados} novo(s) para "
            + "nós, {Ja} já sabíamos, {Fora} não são da nossa base ({Req} requisição(ões)).",
            dia, linhas.Count, conciliados, jaConheciamos, linhas.Count - porCodigo.Count, requisicoes);

        return new(linhas.Count, conciliados, jaConheciamos, linhas.Count - porCodigo.Count,
            requisicoes, null);
    }

    /// <summary>
    /// Os Indicadores de Regulação (motivos das canceladas) vêm DAQUI daqui para frente: a leitura
    /// completa do dia grava todas as linhas (upsert — reler o dia não duplica) e, para dia já
    /// encerrado, o total DECLARADO pela tela vira a janela do dia (escopo "dia"). Falha aqui não pode
    /// derrubar a conciliação, que é o que protege o paciente.
    /// </summary>
    private async Task GravarParaIndicadoresAsync(
        DateOnly dia, IReadOnlyList<MarcacaoCanceladaLidaSisreg> brutas, int? declaradas, CancellationToken ct)
    {
        try
        {
            if (brutas.Count > 0) await indicadores.GravarCanceladasAsync(brutas, ct);

            var hoje = DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
            if (dia < hoje)
            {
                await indicadores.RegistrarJanelaConcluidaAsync(
                    ColetorIndicadorSisreg.Canceladas, dia, dia, PlanoColetaIndicadores.EscopoDia,
                    declaradas ?? brutas.Count, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "SISREG_CONCILIACAO: {Dia} conciliado, mas as linhas não foram gravadas para os indicadores.", dia);
        }
    }

    /// <summary>
    /// O motivo guardado na trilha: a justificativa do SISREG e o operador que cancelou.
    /// <b>Interno.</b> A mensagem ao paciente diz que foi cancelado e nada mais.
    /// </summary>
    private static string Motivo(CancelamentoLidoSisreg l)
    {
        var just = string.IsNullOrWhiteSpace(l.Justificativa) ? "sem justificativa" : l.Justificativa.Trim();
        var texto = $"Cancelado no SISREG: {just}"
                    + (string.IsNullOrWhiteSpace(l.Operador) ? "" : $" (operador {l.Operador.Trim()})");
        return texto.Length <= 500 ? texto : texto[..500];
    }

    private async Task<(List<CancelamentoLidoSisreg> Linhas, List<MarcacaoCanceladaLidaSisreg> Brutas, int? Declaradas,
        int Requisicoes, string? Aviso)> LerDiaAsync(DateOnly dia, CancellationToken ct)
    {
        var data = dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var vistos = new Dictionary<string, CancelamentoLidoSisreg>(StringComparer.Ordinal);
        var todas = new List<MarcacaoCanceladaLidaSisreg>();
        var brutas = 0;
        var requisicoes = 0;
        int? declaradas = null, paginas = null;

        for (var pagina = 0; pagina < TetoPaginas; pagina++)
        {
            var html = await sessao.PostFormAsync(Tela, new Dictionary<string, string>
            {
                ["etapa"] = "LISTAR_MARCACOES",
                // Pelo instante do CANCELAMENTO — é o que faz "o dia de hoje" significar "o que
                // foi cancelado hoje", e não "o que foi marcado ou executado hoje".
                ["tp_periodo"] = "C",
                ["dt_inicial"] = data,
                ["dt_final"] = data,
                ["co_cnes_ups"] = "",   // TODAS as unidades executantes que o operador enxerga
                ["pagina"] = pagina.ToString(CultureInfo.InvariantCulture),
            }, ct);
            requisicoes++;

            if (pagina == 0) (declaradas, paginas) = IndicadoresSisregHtmlParser.TotalCanceladas(html);

            var lidas = IndicadoresSisregHtmlParser.Canceladas(html);
            brutas += lidas.Count;
            todas.AddRange(lidas);
            foreach (var l in lidas)
                vistos.TryAdd(l.Codigo, new CancelamentoLidoSisreg(l.Codigo, l.CanceladoEm, l.Justificativa, l.Operador));

            if (paginas is { } p && pagina + 1 >= p) break;
            if (paginas is null && lidas.Count == 0) break;
        }

        // A tela DIZ quantas linhas existem. Fechar sem bater com isso é concluir de leitura
        // incompleta — num inventário de cancelamentos, "faltou" significa "um se perdeu".
        var aviso = declaradas is { } n && brutas != n
            ? $"a tela declarou {n} linha(s) em {paginas} página(s) e foram lidas {brutas}"
            : null;

        return (vistos.Values.ToList(), todas, declaradas, requisicoes, aviso);
    }
}
