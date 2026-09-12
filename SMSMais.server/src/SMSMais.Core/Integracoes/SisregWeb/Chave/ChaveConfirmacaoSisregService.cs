using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.SisregWeb.Chave;

/// <summary>A chave de confirmação como o SISREG a mostrou agora.</summary>
/// <param name="LidaEm">Instante da leitura (UTC) — a tela mostra "lida no SISREG às …".</param>
public sealed record ChaveConfirmacaoSisregDto(string CodigoSolicitacao, string Chave, DateTime LidaEm);

public interface IChaveConfirmacaoSisregService
{
    /// <summary>
    /// Lê no SISREG a chave de confirmação da solicitação. Aceita o id do exame (satélite de
    /// imagem) ou o da própria solicitação — as duas telas de detalhe têm ids diferentes na mão.
    /// </summary>
    Task<ChaveConfirmacaoSisregDto> RevelarAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Revela a <b>chave de confirmação</b> de uma solicitação, lida na hora no SISREG com o operador
/// da integração.
///
/// <para><b>Somente leitura.</b> Abre a ficha; não confirma, não registra falta. A baixa no SISREG
/// (que usa a chave) vai sair com o login SISREG de quem a faz, e não com este operador — o SISREG
/// registra quem confirmou.</para>
///
/// <para><b>Nada é gravado.</b> A chave é a prova de que o paciente trouxe o comprovante; guardá-la
/// em <c>solicitacao.chave_confirmacao</c> a exporia a todo mundo que abre o pedido (o DTO de exame
/// devolve esse campo). Cada revelação vai para a auditoria — quem, quando, qual solicitação —
/// <b>sem</b> o valor, porque a busca da auditoria é por texto.</para>
///
/// <para><b>Custo: 1 requisição</b> (2 se a primeira tela não mostrar a chave). Sai da reserva do
/// operador no orçamento anti-robô, não do teto dos motores: é um humano clicando.</para>
/// </summary>
public sealed class ChaveConfirmacaoSisregService(
    SmsMaisDbContext db,
    ISisregWebSessao sessao,
    SisregOrcamentoRequisicoes orcamento,
    IOptions<SisregOrcamentoOpcoes> orcamentoOpcoes,
    IAuditoriaService auditoria,
    ILogger<ChaveConfirmacaoSisregService> logger) : IChaveConfirmacaoSisregService
{
    /// <summary>Folga mínima no teto cheio. Uma revelação gasta no máximo 2; abaixo disto o
    /// próximo clique pode ser o que dispara o CAPTCHA — e ele trava o operador por 24 horas.</summary>
    private const int OrcamentoMinimo = 5;

    /// <summary>O sentinela de pedido extra-SUS: não existe no SISREG.</summary>
    private const string CodigoSemSisreg = "0000";

    public async Task<ChaveConfirmacaoSisregDto> RevelarAsync(Guid id, CancellationToken ct = default)
    {
        var solicitacaoId = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == id && e.ExcluidoEm == null)
            .Select(e => (Guid?)e.SolicitacaoId)
            .FirstOrDefaultAsync(ct) ?? id;

        var codigo = await db.Solicitacoes.AsNoTracking()
            .Where(s => s.Id == solicitacaoId && s.ExcluidoEm == null)
            .Select(s => new { s.CodigoSolicitacao })
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException("solicitacao", id.ToString());

        var co = codigo.CodigoSolicitacao?.Trim();
        if (string.IsNullOrEmpty(co) || co == CodigoSemSisreg || !co.All(char.IsDigit))
        {
            throw new ValidacaoException(
                "sisreg_chave.sem_codigo",
                "Esta solicitação não tem número do SISREG — não há chave para consultar.");
        }

        var restante = orcamento.Restante(orcamentoOpcoes.Value.TetoPorHora);
        if (restante < OrcamentoMinimo)
        {
            var espera = orcamento.EsperaAteLiberar();
            throw new ConflitoException(
                "sisreg_chave.orcamento_curto",
                "O limite de consultas ao SISREG desta hora está no fim. Tente de novo em "
                + $"{Math.Max(1, (int)Math.Ceiling(espera?.TotalMinutes ?? 1))} minuto(s).");
        }

        var (chave, tela) = await LerAsync(co, ct);
        if (chave is null)
        {
            // Sem valor e sem HTML no log: a ficha tem nome, CNS e endereço do paciente.
            logger.LogWarning(
                "SISREG_CHAVE: nenhuma tela mostrou a chave da solicitação {Codigo}.", co);
            throw new ConflitoException(
                "sisreg_chave.nao_encontrada",
                "O SISREG não mostrou a chave de confirmação desta solicitação. Ela só existe depois "
                + "que a solicitação é autorizada e agendada.");
        }

        logger.LogInformation("SISREG_CHAVE: solicitação {Codigo} revelada pela tela {Tela}.", co, tela);
        await auditoria.RegistrarAsync(
            "Solicitacao", solicitacaoId.ToString(), "ChaveConfirmacaoRevelada",
            null, $"SISREG {co} (tela {tela})", ct);

        return new ChaveConfirmacaoSisregDto(co, chave, DateTime.UtcNow);
    }

    /// <summary>
    /// A ficha do <c>cons_marcados_reg</c> é onde a chave foi vista (capturas do laboratório). Se
    /// ela não vier — o perfil do operador pode não alcançar essa tela —, tenta a ficha do
    /// <c>gerenciador_solicitacao</c>, a mesma que a leitura da fila já usa.
    /// </summary>
    private async Task<(string? Chave, string Tela)> LerAsync(string codigo, CancellationToken ct)
    {
        var html = await sessao.PostFormAsync("/cgi-bin/cons_marcados_reg", new Dictionary<string, string>
        {
            ["etapa"] = "EXIBIR_FICHA",
            ["co_solicitacao"] = codigo,
        }, ct);
        if (FichaChaveHtmlParser.Ler(html) is { } chave) return (chave, "cons_marcados_reg");

        html = await sessao.GetAsync("/cgi-bin/gerenciador_solicitacao", new Dictionary<string, string>
        {
            ["etapa"] = "VISUALIZAR_FICHA",
            ["co_seq_solicitacao"] = codigo,
        }, ct);
        return (FichaChaveHtmlParser.Ler(html), "gerenciador_solicitacao");
    }
}
