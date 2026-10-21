using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.GuiasAgendamento;

/// <summary>
/// A fila de guias de UM sistema — o mesmo quadro para SISREG, SER, SERNIT e ESUS (ADR-0076): o ritmo
/// configurado, quanto falta e, se ela não está andando, por quê.
/// </summary>
/// <param name="JanelaInicio">"HH:mm" em Brasília.</param>
/// <param name="JanelaFim">"HH:mm" em Brasília. Igual ao início = o dia inteiro.</param>
/// <param name="NaFila">Guias pendentes de agendamentos de hoje em diante.</param>
/// <param name="ObtidasHoje">Guias que chegaram hoje (Brasília).</param>
/// <param name="Falharam">Guias que esgotaram as tentativas, de agendamentos de hoje em diante.</param>
/// <param name="Impedimento">Por que a fila não está buscando agora (pausa, janela, varredura,
/// orçamento…), ou nulo quando está.</param>
public sealed record FilaGuiasDto(
    SistemaRegulacao Sistema,
    bool Habilitada,
    int PorHora,
    string JanelaInicio,
    string JanelaFim,
    int NaFila,
    int ObtidasHoje,
    int Falharam,
    DateTime? UltimaObtidaEm,
    DateTime? PausadaAte,
    string? Impedimento,
    DateTime? AtualizadoEm);

/// <summary>Salvar também tira a pausa (ex.: depois de alguém resolver o CAPTCHA do SISREG).</summary>
public sealed record SalvarFilaGuiasRequest(bool Habilitada, int PorHora, string JanelaInicio, string JanelaFim);

public interface IFilaGuiasConfiguracaoService
{
    /// <summary>A configuração em vigor (a gravada, ou o padrão do sistema).</summary>
    Task<GuiaFilaConfiguracao> ObterAsync(SistemaRegulacao sistema, CancellationToken ct);

    /// <summary>Configuração + contadores + impedimento, para a tela do sistema.</summary>
    Task<FilaGuiasDto> ResumoAsync(SistemaRegulacao sistema, CancellationToken ct);

    Task<FilaGuiasDto> SalvarAsync(SistemaRegulacao sistema, SalvarFilaGuiasRequest req, CancellationToken ct);

    /// <summary>Por que a fila do sistema não busca agora, ou <c>null</c> — a mesma régua do processo.</summary>
    Task<string?> ImpedimentoAsync(GuiaFilaConfiguracao config, CancellationToken ct);
}

public sealed class FilaGuiasConfiguracaoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IEnumerable<IBaixadorGuia> baixadores) : IFilaGuiasConfiguracaoService
{
    /// <summary>Teto do ritmo: uma guia por segundo já é mais do que qualquer sistema aguenta.</summary>
    internal const int MaximoPorHora = 3600;

    private static readonly SistemaRegulacao[] ComFila =
        [SistemaRegulacao.Sisreg, SistemaRegulacao.Ser, SistemaRegulacao.Sernit, SistemaRegulacao.EsusSg];

    public async Task<GuiaFilaConfiguracao> ObterAsync(SistemaRegulacao sistema, CancellationToken ct) =>
        await db.GuiaFilaConfiguracoes.AsNoTracking().FirstOrDefaultAsync(c => c.Sistema == sistema, ct)
        ?? GuiaFilaConfiguracao.Padrao(sistema);

    public async Task<string?> ImpedimentoAsync(GuiaFilaConfiguracao c, CancellationToken ct)
    {
        if (!c.Habilitada) return "A busca de guias está desligada.";
        if (c.PausadaAte is { } ate && ate > DateTime.UtcNow)
            return c.MotivoPausa ?? "A fila está pausada.";
        if (!JanelaDoDia.Contem(JanelaDoDia.AgoraEmBrasilia(), c.JanelaInicio, c.JanelaFim))
            return $"Fora da janela ({Hora(c.JanelaInicio)}–{Hora(c.JanelaFim)}).";
        if (await FilaGuiasScheduler.ChaveMestraAsync(db, c.Sistema, ct) is { } chaveMestra) return chaveMestra;
        var baixador = baixadores.FirstOrDefault(b => b.Sistema == c.Sistema);
        return baixador is null ? "Este sistema ainda não tem busca de guia." : await baixador.ImpedimentoAsync(ct);
    }

    public async Task<FilaGuiasDto> ResumoAsync(SistemaRegulacao sistema, CancellationToken ct)
    {
        var c = await ObterAsync(sistema, ct);
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var inicioDoDia = FusoBrasilia.InicioDoDiaAtualEmUtc();
        var guias = db.GuiasAgendamento.AsNoTracking().Where(g => g.Sistema == sistema);

        var naFila = await guias.CountAsync(
            g => g.Situacao == SituacaoGuiaAgendamento.Pendente && g.DataAtendimento >= hoje, ct);
        var obtidasHoje = await guias.CountAsync(
            g => g.Situacao == SituacaoGuiaAgendamento.Obtida && g.ObtidaEm >= inicioDoDia, ct);
        var falharam = await guias.CountAsync(
            g => g.Situacao == SituacaoGuiaAgendamento.Falhou && g.DataAtendimento >= hoje, ct);
        var ultima = await guias.Where(g => g.Situacao == SituacaoGuiaAgendamento.Obtida)
            .MaxAsync(g => g.ObtidaEm, ct);

        return new FilaGuiasDto(
            sistema, c.Habilitada, c.PorHora, Hora(c.JanelaInicio), Hora(c.JanelaFim),
            naFila, obtidasHoje, falharam, ultima,
            c.PausadaAte > DateTime.UtcNow ? c.PausadaAte : null,
            await ImpedimentoAsync(c, ct), c.AtualizadoEm);
    }

    public async Task<FilaGuiasDto> SalvarAsync(SistemaRegulacao sistema, SalvarFilaGuiasRequest req, CancellationToken ct)
    {
        if (!ComFila.Contains(sistema))
        {
            throw new ValidacaoException("guias.sistema_invalido", "Este sistema não tem fila de guias.");
        }
        if (req.PorHora is < 1 or > MaximoPorHora)
        {
            throw new ValidacaoException("guias.por_hora_invalido", $"Informe de 1 a {MaximoPorHora} guias por hora.");
        }
        var inicio = LerHora(req.JanelaInicio, "início");
        var fim = LerHora(req.JanelaFim, "fim");

        var c = await db.GuiaFilaConfiguracoes.FirstOrDefaultAsync(x => x.Sistema == sistema, ct);
        if (c is null)
        {
            c = GuiaFilaConfiguracao.Padrao(sistema);
            db.GuiaFilaConfiguracoes.Add(c);
        }
        c.Habilitada = req.Habilitada;
        c.PorHora = req.PorHora;
        c.JanelaInicio = inicio;
        c.JanelaFim = fim;
        c.PausadaAte = null;
        c.MotivoPausa = null;
        c.AtualizadoEm = DateTime.UtcNow;
        c.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
        return await ResumoAsync(sistema, ct);
    }

    private static string Hora(TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static TimeOnly LerHora(string? texto, string qual) =>
        TimeOnly.TryParseExact(texto?.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t
            : throw new ValidacaoException("guias.janela_invalida", $"Hora de {qual} da janela inválida (use HH:mm).");
}
