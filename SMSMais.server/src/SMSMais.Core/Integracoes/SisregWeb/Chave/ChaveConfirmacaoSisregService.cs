using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Data;

namespace SMSMais.Core.Integracoes.SisregWeb.Chave;

/// <summary>A chave de confirmação da solicitação, como o SISREG a mostrou.</summary>
/// <param name="LidaEm">Instante (UTC) em que ela foi lida no SISREG — a tela mostra "lida no
/// SISREG às …". Quando vem do banco, é a data da primeira leitura, não a de agora.</param>
public sealed record ChaveConfirmacaoSisregDto(string CodigoSolicitacao, string Chave, DateTime LidaEm);

public interface IChaveConfirmacaoSisregService
{
    /// <summary>
    /// <b>O comando único</b> de chave: devolve a chave guardada na solicitação; se ainda não há,
    /// lê no SISREG, guarda e devolve. Todo caminho (painel, app do paciente, recepção) passa por
    /// aqui — a origem (banco ou SISREG) não muda o resultado.
    /// </summary>
    Task<ChaveConfirmacaoSisregDto> ObterAsync(Guid solicitacaoId, CancellationToken ct = default);

    /// <summary>
    /// <see cref="ObterAsync"/> a partir do id da tela: aceita o id do exame (satélite de imagem)
    /// ou o da própria solicitação — as duas telas de detalhe têm ids diferentes na mão.
    /// </summary>
    Task<ChaveConfirmacaoSisregDto> RevelarAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Revela a <b>chave de confirmação</b> de uma solicitação.
///
/// <para><b>Um comando só.</b> A primeira revelação lê no SISREG com o operador da integração e
/// guarda em <c>solicitacao.chave_confirmacao_sisreg</c>; as seguintes saem do banco sem gastar
/// requisição. É a mesma chave que a recepção digita ao autorizar (e que é criticada contra a
/// guardada) e a que o paciente vê no app no dia do atendimento.</para>
///
/// <para><b>Somente leitura no SISREG.</b> Abre a ficha; não confirma, não registra falta. A baixa
/// (que usa a chave) sai com o login SISREG de quem a faz — o SISREG registra quem confirmou.</para>
///
/// <para><b>Guardada, mas não exposta.</b> A coluna nunca entra no DTO da solicitação; só sai por
/// este comando, e cada revelação vai para a auditoria — quem, quando, qual solicitação — <b>sem</b>
/// o valor, porque a busca da auditoria é por texto.</para>
///
/// <para><b>Custo: 1 requisição na primeira vez</b> (2 se a primeira tela não mostrar a chave),
/// zero depois. Sai da reserva do operador no orçamento anti-robô, não do teto dos motores.</para>
/// </summary>
public sealed class ChaveConfirmacaoSisregService(
    SmsMaisDbContext db,
    ISisregWebSessao sessao,
    SisregOrcamentoRequisicoes orcamento,
    IOptions<SisregOrcamentoOpcoes> orcamentoOpcoes,
    IAuditoriaService auditoria,
    ILogger<ChaveConfirmacaoSisregService> logger) : IChaveConfirmacaoSisregService
{
    /// <summary>Folga mínima no teto cheio. Uma leitura gasta no máximo 2; abaixo disto o
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

        return await ObterAsync(solicitacaoId, ct);
    }

    public async Task<ChaveConfirmacaoSisregDto> ObterAsync(Guid solicitacaoId, CancellationToken ct = default)
    {
        var reg = await db.Solicitacoes
            .FirstOrDefaultAsync(s => s.Id == solicitacaoId && s.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("solicitacao", solicitacaoId.ToString());

        var co = reg.CodigoSolicitacao?.Trim();
        if (string.IsNullOrEmpty(co) || co == CodigoSemSisreg || !co.All(char.IsDigit))
        {
            throw new ValidacaoException(
                "sisreg_chave.sem_codigo",
                "Esta solicitação não tem número do SISREG — não há chave para consultar.");
        }

        // 1) Já está no banco: devolve sem ir ao SISREG.
        if (!string.IsNullOrWhiteSpace(reg.ChaveConfirmacaoSisreg))
        {
            await auditoria.RegistrarAsync(
                "Solicitacao", reg.Id.ToString(), "ChaveConfirmacaoRevelada",
                null, $"SISREG {co} (guardada)", ct);
            return new ChaveConfirmacaoSisregDto(
                co, reg.ChaveConfirmacaoSisreg, reg.ChaveSisregLidaEm ?? reg.AtualizadoEm ?? DateTime.UtcNow);
        }

        // 2) Não está: lê no SISREG, guarda e devolve.
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

        var agora = DateTime.UtcNow;
        reg.ChaveConfirmacaoSisreg = chave;
        reg.ChaveSisregLidaEm = agora;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("SISREG_CHAVE: solicitação {Codigo} lida pela tela {Tela} e guardada.", co, tela);
        await auditoria.RegistrarAsync(
            "Solicitacao", reg.Id.ToString(), "ChaveConfirmacaoRevelada",
            null, $"SISREG {co} (lida na tela {tela})", ct);

        return new ChaveConfirmacaoSisregDto(co, chave, agora);
    }

    /// <summary>
    /// A ficha do <c>cons_marcados_reg</c> é onde a chave foi vista (capturas do laboratório). Se
    /// ela não vier — o perfil do operador pode não alcançar essa tela —, tenta a ficha do
    /// <c>gerenciador_solicitacao</c> (menu Consulta Amb → Solicitações), que também a mostra.
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
