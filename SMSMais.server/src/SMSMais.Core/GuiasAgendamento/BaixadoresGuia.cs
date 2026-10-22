using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Integracoes.EsusSgWeb;
using SMSMais.Core.Integracoes.EsusSgWeb.Varredura.Background;
using SMSMais.Core.Integracoes.SernitWeb.Varredura;
using SMSMais.Core.Integracoes.SernitWeb.Varredura.Background;
using SMSMais.Core.Integracoes.SerWeb.Varredura;
using SMSMais.Core.Integracoes.SerWeb.Varredura.Background;
using SMSMais.Core.Integracoes.SisregWeb;
using SMSMais.Core.Integracoes.SisregWeb.Chave;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;
using SisregEscalas = SMSMais.Core.Integracoes.SisregWeb.Escalas.Background;
using SisregFila = SMSMais.Core.Integracoes.SisregWeb.Fila.Background;
using SisregImportacao = SMSMais.Core.Integracoes.SisregWeb.Importacao.Background;
using SisregLote = SMSMais.Core.Integracoes.SisregWeb.MapeamentoLote.Background;
using SisregVarredura = SMSMais.Core.Integracoes.SisregWeb.Varredura.Background;

namespace SMSMais.Core.GuiasAgendamento;

/// <summary>A guia trazida do sistema de regulação, pronta para o acervo do paciente.</summary>
public sealed record GuiaBaixada(byte[] Pdf, string Titulo, string Descricao, string NomeArquivo);

/// <summary>
/// Quem sabe buscar a guia de UM sistema de regulação (ADR-0076). A fila é genérica: ela escolhe a
/// próxima guia, no ritmo e na janela configurados, e entrega ao baixador do sistema. Sistema novo é
/// mais um baixador registrado.
/// </summary>
public interface IBaixadorGuia
{
    SistemaRegulacao Sistema { get; }

    /// <summary>
    /// Por que a fila deste sistema não deve buscar AGORA — a varredura está usando a sessão, o
    /// sincronismo está desligado, o orçamento está curto… —, ou <c>null</c> quando pode. Não conta
    /// tentativa de ninguém: a fila só espera. Aparece na tela de configuração do sistema.
    /// </summary>
    Task<string?> ImpedimentoAsync(CancellationToken ct);

    Task<GuiaBaixada> BaixarAsync(GuiaAgendamento guia, CancellationToken ct);
}

/// <summary>Ainda não dá para buscar ESTA guia (ex.: o ESUS ainda não deu o id do agendamento). Não
/// conta como tentativa: a guia volta para a fila depois de <see cref="Espera"/>.</summary>
public sealed class GuiaAdiadaException(string motivo, TimeSpan espera) : Exception(motivo)
{
    public TimeSpan Espera { get; } = espera;
}

/// <summary>O sistema inteiro não aceita mais pedidos por um tempo (ex.: o SISREG pediu CAPTCHA). A
/// fila do sistema para por <see cref="Duracao"/>; a guia não conta tentativa.</summary>
public sealed class FilaGuiasPausadaException(string motivo, TimeSpan duracao) : Exception(motivo)
{
    public TimeSpan Duracao { get; } = duracao;
}

/// <summary>Título, descrição e nome de arquivo da guia — o mesmo padrão para todos os sistemas.</summary>
internal static class TextoGuia
{
    public static GuiaBaixada Montar(
        byte[] pdf, SistemaRegulacao sistema, string numero, string procedimento, DateOnly data, TimeOnly? hora, string? local)
    {
        var proc = procedimento.Length > 150 ? procedimento[..150] : procedimento;
        var quando = hora is { } h ? $"{data:dd/MM/yyyy} às {h:HH\\:mm}" : $"{data:dd/MM/yyyy}";
        var onde = string.IsNullOrWhiteSpace(local) ? null : $" em {local}";
        return new GuiaBaixada(
            pdf,
            $"Guia — {proc} — {data:dd/MM/yyyy}",
            $"Guia do agendamento de {quando}{onde}. {NomeSistema(sistema)}, pedido {numero}.",
            $"guia-{Sigla(sistema)}-{numero}-{data:yyyy-MM-dd}.pdf");
    }

    public static string NomeSistema(SistemaRegulacao s) => s switch
    {
        SistemaRegulacao.Sisreg => "SISREG (regulação municipal)",
        SistemaRegulacao.Ser => "SER (regulação estadual)",
        SistemaRegulacao.Sernit => "SER de Niterói",
        SistemaRegulacao.EsusSg => "ESUS de São Gonçalo",
        _ => s.ToString(),
    };

    private static string Sigla(SistemaRegulacao s) => s switch
    {
        SistemaRegulacao.Sisreg => "sisreg",
        SistemaRegulacao.Ser => "ser",
        SistemaRegulacao.Sernit => "sernit",
        SistemaRegulacao.EsusSg => "esus",
        _ => "regulacao",
    };
}

/// <summary>Base dos sistemas cuja âncora é o <see cref="AgendamentoExterno"/> (ADR-0072). A sessão
/// de cada um é única: com a varredura do sistema rodando, a fila espera — pesquisar no meio dela
/// desarruma a paginação.</summary>
public abstract class BaixadorGuiaExterno(SmsMaisDbContext db) : IBaixadorGuia
{
    public abstract SistemaRegulacao Sistema { get; }
    protected abstract bool VarreduraRodando { get; }

    public Task<string?> ImpedimentoAsync(CancellationToken ct) => Task.FromResult(
        VarreduraRodando ? "A varredura do sistema está rodando — a guia espera ela terminar." : null);

    public async Task<GuiaBaixada> BaixarAsync(GuiaAgendamento guia, CancellationToken ct)
    {
        var a = await db.AgendamentosExternos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == guia.AgendamentoExternoId, ct)
            ?? throw new InvalidOperationException($"Agendamento externo {guia.AgendamentoExternoId} não existe.");
        var pdf = await PdfAsync(a, ct);
        return TextoGuia.Montar(pdf, a.Sistema, a.NumeroExterno, a.Procedimento, a.Data, a.Hora, a.LocalNome);
    }

    protected abstract Task<byte[]> PdfAsync(AgendamentoExterno agendamento, CancellationToken ct);
}

/// <summary>SER: o PDF "Autorização de Consulta/Exame Resumida" — gasta uma via no SER.</summary>
public sealed class BaixadorGuiaSer(SmsMaisDbContext db, ISerLeitorService leitor, IVarreduraSerFila fila)
    : BaixadorGuiaExterno(db)
{
    public override SistemaRegulacao Sistema => SistemaRegulacao.Ser;
    protected override bool VarreduraRodando => fila.TemTrabalho;

    protected override Task<byte[]> PdfAsync(AgendamentoExterno agendamento, CancellationToken ct) =>
        leitor.EmitirGuiaAsync(agendamento.NumeroExterno, ct);
}

/// <summary>SERNIT: a mesma chave de autorização — gasta uma via no SERNIT.</summary>
public sealed class BaixadorGuiaSernit(SmsMaisDbContext db, ISernitLeitorService leitor, IVarreduraSernitFila fila)
    : BaixadorGuiaExterno(db)
{
    public override SistemaRegulacao Sistema => SistemaRegulacao.Sernit;
    protected override bool VarreduraRodando => fila.TemTrabalho;

    protected override Task<byte[]> PdfAsync(AgendamentoExterno agendamento, CancellationToken ct) =>
        leitor.EmitirGuiaAsync(agendamento.NumeroExterno, ct);
}

/// <summary>ESUS SG: os dados do comprovante viram PDF aqui. Não marca "impresso" no ESUS.</summary>
public sealed class BaixadorGuiaEsusSg(SmsMaisDbContext db, IEsusSgLeitorService leitor, IVarreduraEsusSgFila fila)
    : BaixadorGuiaExterno(db)
{
    private readonly SmsMaisDbContext _db = db;

    public override SistemaRegulacao Sistema => SistemaRegulacao.EsusSg;
    protected override bool VarreduraRodando => fila.TemTrabalho;

    protected override async Task<byte[]> PdfAsync(AgendamentoExterno agendamento, CancellationToken ct)
    {
        var espelho = await _db.EsusSgSolicitacoes.AsNoTracking()
            .Where(e => e.Id == agendamento.OrigemId)
            .Select(e => new { e.AgendamentoIdEsus, e.DataAgendada })
            .FirstOrDefaultAsync(ct);
        // Sem o eap_id (a varredura ainda não passou desde a coluna nova) não há o que pedir.
        if (espelho?.AgendamentoIdEsus is not { } eap || espelho.DataAgendada != agendamento.Data)
        {
            throw new GuiaAdiadaException(
                "O ESUS ainda não deu o id deste agendamento — volta depois da próxima varredura.",
                TimeSpan.FromHours(1));
        }
        var comprovante = await leitor.LerComprovanteExameAsync(eap, ct);
        return GuiaEsusSgPdf.Gerar(comprovante, FusoBrasilia.ParaExibicao(DateTime.UtcNow));
    }
}

/// <summary>
/// SISREG: a ficha da solicitação (a página que o "Imprimir" do SISREG imprime), lida pelo mesmo
/// comando da chave de confirmação, vira PDF aqui. Leva a chave — decisão do dono, 21/10/2026.
///
/// <para>A credencial é a dedicada do sincronismo, mas o <b>orçamento anti-robô</b> é dividido com
/// os outros motores do SISREG: a fila cede a qualquer um deles rodando, cede ao orçamento curto e,
/// se o SISREG pedir CAPTCHA, para por 24 horas (o CAPTCHA é do usuário, não da guia).</para>
/// </summary>
public sealed class BaixadorGuiaSisreg(
    SmsMaisDbContext db,
    IChaveConfirmacaoSisregService fichas,
    SisregOrcamentoRequisicoes orcamento,
    IOptions<SisregOrcamentoOpcoes> orcamentoOpcoes,
    SisregVarredura.VarreduraSisregEstadoVivo varredura,
    SisregImportacao.SisregImportacaoEstadoVivo importacao,
    SisregLote.MapeamentoLoteEstadoVivo lote,
    SisregEscalas.EscalasSincronizacaoEstadoVivo escalas,
    SisregFila.FilaPendenteEstadoVivo filaPendente) : IBaixadorGuia
{
    /// <summary>Folga que a fila deixa no teto automático: a guia é a menos urgente dos motores.</summary>
    internal const int MargemOrcamento = 40;

    public SistemaRegulacao Sistema => SistemaRegulacao.Sisreg;

    // A chave-mestra do SISREG é conferida pela fila (FilaGuiasScheduler.ChaveMestraAsync).
    public Task<string?> ImpedimentoAsync(CancellationToken ct) => Task.FromResult<string?>(
        varredura.ObterAtual() is not null || importacao.ObterAtual() is not null
            || lote.EmExecucao || escalas.EmExecucao || filaPendente.EmExecucao
            ? "Outro processo do SISREG está rodando — a guia espera ele terminar."
            : orcamento.Restante(orcamentoOpcoes.Value.TetoAutomatico) < MargemOrcamento
                ? "O limite de consultas ao SISREG desta hora está no fim."
                : null);

    public async Task<GuiaBaixada> BaixarAsync(GuiaAgendamento guia, CancellationToken ct)
    {
        var solicitacaoId = guia.SolicitacaoId
            ?? throw new InvalidOperationException($"Guia {guia.Id} do SISREG sem solicitação.");

        FichaSisregLida lida;
        try
        {
            lida = await fichas.LerFichaAsync(solicitacaoId, orcamentoOpcoes.Value.TetoAutomatico, ct);
        }
        catch (ConflitoException ex) when (ex.Codigo == ChaveConfirmacaoSisregService.CodigoOrcamentoCurto)
        {
            throw new GuiaAdiadaException(ex.Message, TimeSpan.FromMinutes(15));
        }
        catch (Exception ex) when (SisregWebSessao.EhCaptcha(ex))
        {
            throw new FilaGuiasPausadaException(
                "O SISREG pediu CAPTCHA — a fila de guias para por 24 horas (só uma pessoa entrando no SISREG libera).",
                TimeSpan.FromHours(24));
        }

        // A página pode voltar montada com o miolo vazio, ou de outra solicitação: sem a ficha
        // DESTA solicitação, não há guia — e nada vai para o acervo.
        var ficha = FichaSisregHtmlParser.Ler(lida.Html)
            ?? throw new InvalidOperationException($"O SISREG não devolveu a ficha da solicitação {lida.CodigoSolicitacao}.");
        if (ficha.CodigoSolicitacao is { } codigo && codigo != lida.CodigoSolicitacao)
        {
            throw new InvalidOperationException(
                $"A ficha que voltou é da solicitação {codigo}, não da {lida.CodigoSolicitacao}.");
        }

        var s = await db.Solicitacoes.AsNoTracking()
            .Where(x => x.Id == solicitacaoId)
            .Select(x => new
            {
                x.ProcedimentoTexto,
                TipoExame = x.ExameImagem != null && x.ExameImagem.TipoExame != null ? x.ExameImagem.TipoExame.Nome : null,
                x.DataAgendada,
                Unidade = x.UnidadeExecutante != null ? x.UnidadeExecutante.Nome : null,
            })
            .FirstAsync(ct);
        var pdf = GuiaSisregPdf.Gerar(ficha, FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        TimeOnly? hora = s.DataAgendada is { } d ? TimeOnly.FromDateTime(FusoBrasilia.ParaExibicao(d)) : null;
        return TextoGuia.Montar(
            pdf, SistemaRegulacao.Sisreg, lida.CodigoSolicitacao,
            s.TipoExame ?? s.ProcedimentoTexto ?? "Agendamento", guia.DataAtendimento, hora, s.Unidade);
    }
}
