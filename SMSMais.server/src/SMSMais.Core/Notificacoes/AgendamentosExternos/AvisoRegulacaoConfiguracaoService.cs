using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.Confirmacoes;
using SMSMais.Core.Notificacoes.Confirmacoes.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Notificacoes.AgendamentosExternos;

/// <summary>
/// O bloco "Avisos ao paciente" da tela de Configuração de uma regulação.
/// </summary>
/// <param name="Sistema">"sisreg", "ser", "sernit" ou "esussg".</param>
/// <param name="AvisoAgendamentoHabilitado">Só leitura: o aviso de agendamento é ligado em outro
/// lugar — Mensageria → Regras para SER/SERNIT/ESUS; unidade a unidade (aba SISREG) para o SISREG.</param>
/// <param name="ConciliacaoCancelamentoHabilitada">Só leitura e só no SISREG: o aviso de
/// cancelamento dele depende do motor que traz os cancelamentos feitos lá (Mensageria → Regras).</param>
public sealed record AvisosPacienteRegulacaoDto(
    string Sistema,
    bool LembreteHabilitado,
    int LembreteDiasAntes,
    bool AvisoCancelamentoHabilitado,
    DateTime? AvisoCancelamentoLigadoEm,
    bool AvisoAgendamentoHabilitado,
    bool? ConciliacaoCancelamentoHabilitada,
    DateTime? AtualizadoEm);

public sealed record SalvarAvisosPacienteRegulacaoRequest(
    bool LembreteHabilitado,
    int LembreteDiasAntes,
    bool AvisoCancelamentoHabilitado);

/// <summary>As regras de uma regulação externa como os motores precisam (sem linha = desligado).</summary>
public sealed record AvisoRegulacaoRegras(
    SistemaRegulacao Sistema,
    bool LembreteHabilitado,
    int LembreteDiasAntes,
    bool AvisoCancelamentoHabilitado,
    DateTime? AvisoCancelamentoLigadoEm)
{
    public static AvisoRegulacaoRegras Desligado(SistemaRegulacao sistema) => new(sistema, false, 2, false, null);
}

public interface IAvisoRegulacaoConfiguracaoService
{
    Task<AvisosPacienteRegulacaoDto> ObterAsync(SistemaRegulacao sistema, CancellationToken ct = default);

    Task<AvisosPacienteRegulacaoDto> SalvarAsync(
        SistemaRegulacao sistema, SalvarAvisosPacienteRegulacaoRequest request, CancellationToken ct = default);

    /// <summary>As regras do SER, do SERNIT e do ESUS SG — as três sempre presentes no dicionário.</summary>
    Task<IReadOnlyDictionary<SistemaRegulacao, AvisoRegulacaoRegras>> RegrasExternasAsync(CancellationToken ct = default);
}

/// <summary>
/// Lembrete e aviso de cancelamento POR REGULAÇÃO (21/10/2026). SER, SERNIT e ESUS SG gravam em
/// <see cref="AvisoRegulacaoConfiguracao"/>; o SISREG grava nas mesmas chaves de sempre
/// (<see cref="Data.Entities.Notificacoes.ConfirmacaoConfiguracao"/>), que a Mensageria também mostra
/// — um valor só, visto de dois lugares.
/// </summary>
public sealed class AvisoRegulacaoConfiguracaoService(
    SmsMaisDbContext db,
    IConfirmacaoConfiguracaoService confirmacoes,
    IUsuarioAtualAccessor usuarioAtual) : IAvisoRegulacaoConfiguracaoService
{
    internal static readonly SistemaRegulacao[] Externos =
        [SistemaRegulacao.Ser, SistemaRegulacao.Sernit, SistemaRegulacao.EsusSg];

    public async Task<AvisosPacienteRegulacaoDto> ObterAsync(SistemaRegulacao sistema, CancellationToken ct = default)
    {
        var geral = await confirmacoes.ObterAsync(ct);
        if (sistema == SistemaRegulacao.Sisreg) return DoSisreg(geral);

        Validar(sistema);
        var linha = await db.AvisosRegulacaoConfiguracao.AsNoTracking().FirstOrDefaultAsync(x => x.Sistema == sistema, ct);
        return DoExterno(sistema, linha, geral);
    }

    public async Task<AvisosPacienteRegulacaoDto> SalvarAsync(
        SistemaRegulacao sistema, SalvarAvisosPacienteRegulacaoRequest request, CancellationToken ct = default)
    {
        if (request.LembreteDiasAntes is < 1 or > 30)
            throw new ValidacaoException("confirmacao.lembrete_dias_invalido",
                "O lembrete deve sair de 1 a 30 dias antes do agendamento.");

        if (sistema == SistemaRegulacao.Sisreg)
        {
            // As outras chaves do SISREG (janela, conciliação, régua…) seguem como estão: o pedido
            // leva de volta exatamente o que está gravado.
            var g = await confirmacoes.ObterAsync(ct);
            var salvo = await confirmacoes.SalvarAsync(new SalvarConfirmacaoConfiguracaoRequest(
                g.HoraInicioEnvio, g.HoraFimEnvio, g.MaximoPorPassagem, g.SomenteSisreg,
                request.LembreteDiasAntes, request.LembreteHabilitado,
                g.ConciliacaoCancelamentoHabilitada, request.AvisoCancelamentoHabilitado,
                g.ConciliacaoIntervaloMinutos, g.ConciliacaoHoraInicio, g.ConciliacaoHoraFim,
                g.ConciliacaoHoraFechamento, g.ReforcoConfirmacaoHabilitado, g.OrientacaoPostoHabilitada,
                g.AvisoAgendamentoExternoHabilitado), ct);
            return DoSisreg(salvo);
        }

        Validar(sistema);
        var agora = DateTime.UtcNow;
        var linha = await db.AvisosRegulacaoConfiguracao.FirstOrDefaultAsync(x => x.Sistema == sistema, ct);
        if (linha is null)
        {
            linha = new AvisoRegulacaoConfiguracao { Sistema = sistema, CriadoEm = agora };
            db.AvisosRegulacaoConfiguracao.Add(linha);
        }

        // Ligar vale daqui para frente: o instante da virada é o corte do que pode ser avisado.
        if (request.AvisoCancelamentoHabilitado && !linha.AvisoCancelamentoHabilitado)
            linha.AvisoCancelamentoLigadoEm = agora;
        linha.AvisoCancelamentoHabilitado = request.AvisoCancelamentoHabilitado;
        linha.LembreteHabilitado = request.LembreteHabilitado;
        linha.LembreteDiasAntes = request.LembreteDiasAntes;
        linha.AtualizadoEm = agora;
        linha.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);

        return DoExterno(sistema, linha, await confirmacoes.ObterAsync(ct));
    }

    public async Task<IReadOnlyDictionary<SistemaRegulacao, AvisoRegulacaoRegras>> RegrasExternasAsync(
        CancellationToken ct = default)
    {
        var linhas = await db.AvisosRegulacaoConfiguracao.AsNoTracking().ToListAsync(ct);
        return Externos.ToDictionary(s => s, s => linhas.FirstOrDefault(l => l.Sistema == s) is { } l
            ? new AvisoRegulacaoRegras(s, l.LembreteHabilitado, l.LembreteDiasAntes,
                l.AvisoCancelamentoHabilitado, l.AvisoCancelamentoLigadoEm)
            : AvisoRegulacaoRegras.Desligado(s));
    }

    private static void Validar(SistemaRegulacao sistema)
    {
        if (!Externos.Contains(sistema))
            throw new ValidacaoException("regulacao.sistema_sem_avisos",
                "Esta regulação não tem avisos ao paciente configuráveis.");
    }

    private static AvisosPacienteRegulacaoDto DoSisreg(ConfirmacaoConfiguracaoDto g) => new(
        "sisreg", g.LembreteHabilitado, g.LembreteDiasAntes, g.AvisoCancelamentoHabilitado,
        g.AvisoCancelamentoLigadoEm, AvisoAgendamentoHabilitado: true, g.ConciliacaoCancelamentoHabilitada,
        g.AtualizadoEm);

    private static AvisosPacienteRegulacaoDto DoExterno(
        SistemaRegulacao sistema, AvisoRegulacaoConfiguracao? l, ConfirmacaoConfiguracaoDto geral) => new(
        Chave(sistema),
        l?.LembreteHabilitado ?? false,
        l?.LembreteDiasAntes ?? 2,
        l?.AvisoCancelamentoHabilitado ?? false,
        l?.AvisoCancelamentoLigadoEm,
        geral.AvisoAgendamentoExternoHabilitado,
        ConciliacaoCancelamentoHabilitada: null,
        l?.AtualizadoEm);

    private static string Chave(SistemaRegulacao s) => s switch
    {
        SistemaRegulacao.Ser => "ser",
        SistemaRegulacao.Sernit => "sernit",
        SistemaRegulacao.EsusSg => "esussg",
        _ => s.ToString().ToLowerInvariant(),
    };
}
