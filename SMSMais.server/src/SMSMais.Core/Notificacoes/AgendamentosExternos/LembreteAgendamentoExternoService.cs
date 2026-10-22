using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Notificacoes.AgendamentosExternos;

/// <summary>
/// Lembrete X dias antes dos agendamentos do SER, do SERNIT e do ESUS SG (21/10/2026) — o mesmo
/// lembrete do SISREG (<see cref="Confirmacoes.LembreteAgendamentoService"/>), com a chave e os dias
/// de cada regulação (tela de Configuração dela).
///
/// <para>As mesmas regras de quem recebe: quem <b>confirmou</b> recebe o lembrete objetivo
/// (<c>agendamento_proximo</c>); quem <b>não respondeu</b> recebe a primeira mensagem de novo; quem
/// avisou que <b>não vai</b> não recebe nada; quem foi avisado há menos de
/// <see cref="ComunicacaoPacienteOptions.LembreteIntervaloMinimoDias"/> dias também não. E não sai
/// enquanto o aviso do agendamento ainda está na fila — seriam duas mensagens sobre a mesma coisa.</para>
///
/// <para>Só ENFILEIRA. Quem envia é o <see cref="EnviadorComunicacaoService"/>, com a janela, a vazão
/// e as travas de LGPD de sempre.</para>
/// </summary>
public interface ILembreteAgendamentoExternoService
{
    /// <summary>Enfileira os lembretes devidos agora. Devolve quantos entraram na fila. Salva.</summary>
    Task<int> EnfileirarDevidosAsync(CancellationToken ct = default);
}

public sealed class LembreteAgendamentoExternoService(
    SmsMaisDbContext db,
    IAvisoRegulacaoConfiguracaoService avisosRegulacao,
    IComunicacaoPacienteService comunicacoes,
    IOptions<ComunicacaoPacienteOptions> options,
    ILogger<LembreteAgendamentoExternoService> logger) : ILembreteAgendamentoExternoService
{
    /// <summary>Teto por passagem: o lembrete é diário e previsível — rajada aqui é sinal de erro.</summary>
    private const int MaximoPorPassagem = 500;

    /// <summary>O aviso do agendamento que ainda não saiu (o lembrete espera por ele).</summary>
    private static readonly StatusComunicacao[] AvisoAindaNaFila =
    [
        StatusComunicacao.Pendente,
        StatusComunicacao.AguardandoTelefoneVerificado,
        StatusComunicacao.AguardandoCorrecaoContato,
    ];

    public async Task<int> EnfileirarDevidosAsync(CancellationToken ct = default)
    {
        if (!options.Value.EnviarLembreteAgendamento) return 0;

        var ligadas = (await avisosRegulacao.RegrasExternasAsync(ct)).Values
            .Where(r => r.LembreteHabilitado)
            .ToList();
        if (ligadas.Count == 0) return 0;

        var agora = DateTime.UtcNow;
        var hoje = Common.Tempo.FusoBrasilia.HojeEmBrasilia();
        var silencio = agora.AddDays(-Math.Max(0, options.Value.LembreteIntervaloMinimoDias));

        var enfileirados = 0;
        foreach (var regra in ligadas)
        {
            // O dia filtra no banco; o instante exato (com a hora, quando veio) decide em memória.
            var ultimoDia = hoje.AddDays(regra.LembreteDiasAntes);
            var limite = agora.AddDays(regra.LembreteDiasAntes);
            var candidatos = await db.AgendamentosExternos
                .Where(a => a.Sistema == regra.Sistema
                    && a.Situacao == SituacaoAgendamentoExterno.Ativo
                    && a.Data >= hoje && a.Data <= ultimoDia
                    && a.StatusConfirmacao != StatusConfirmacaoAgendamento.Cancelada
                    && !db.ComunicacoesPaciente.Any(c => c.AgendamentoExternoId == a.Id
                        && c.Finalidade == FinalidadeComunicacao.LembreteAgendamento)
                    // Falou com essa pessoa faz pouco tempo? Então não fala de novo.
                    && !db.ComunicacoesPaciente.Any(c => c.AgendamentoExternoId == a.Id
                        && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                        && c.EnviadoEm != null && c.EnviadoEm > silencio)
                    && !db.ComunicacoesPaciente.Any(c => c.AgendamentoExternoId == a.Id
                        && c.Finalidade == FinalidadeComunicacao.ConfirmacaoAgendamento
                        && c.EnviadoEm == null && AvisoAindaNaFila.Contains(c.Status)))
                .OrderBy(a => a.Data)
                .Take(MaximoPorPassagem)
                .ToListAsync(ct);

            foreach (var a in candidatos)
            {
                var instante = ComunicacaoPacienteService.InstanteDoAgendamento(a);
                if (instante <= agora || instante > limite) continue;
                await comunicacoes.EnfileirarExternoAsync(a, FinalidadeComunicacao.LembreteAgendamento, null, ct);
                enfileirados++;
            }
        }

        if (enfileirados > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Lembrete dos agendamentos do SER/SERNIT/ESUS SG: {Qtd} enfileirado(s).", enfileirados);
        }
        return enfileirados;
    }
}
