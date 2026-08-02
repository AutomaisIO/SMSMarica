using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Integracoes.Dtos;
using SMSMarica.Core.Integracoes.Proxy;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;
using SMSMarica.Data.Entities.Pep;

namespace SMSMarica.Core.Integracoes.Pep.Divergencias;

/// <summary>Resumo de uma rodada de arbitragem (o que a tela e o log mostram).</summary>
public sealed record ResultadoVerificacaoDivergencias(
    int Analisadas,
    int OrigemCorreta,
    int HubCorreto,
    int AmbosNegados,
    int NaoConclusivas,
    bool InterrompidaPorIndisponibilidade,
    /// <summary>A rodada parou por ter estourado o teto de TEMPO, não por acabar a fila.</summary>
    bool InterrompidaPorTempo = false,
    /// <summary>Quantas ficaram sem análise nesta rodada (voltam na próxima).</summary>
    int Restantes = 0);

/// <summary>
/// Arbitra divergências de identidade contra a consulta oficial de CPF (Hub do Desenvolvedor,
/// com CADSUS/SISREG como fallback — a cadeia configurada em <c>proxy_motor</c>).
/// </summary>
public interface IVerificadorDivergenciasPep
{
    /// <param name="max">Teto de divergências por rodada.</param>
    /// <param name="teto">
    /// Teto de TEMPO da rodada. Limitar só a quantidade não protege: 50 divergências lentas
    /// seguram a rodada por dezenas de minutos (foi o que aconteceu em 02/08). O que se quer
    /// limitar é o tempo de parede, porque quem está do outro lado é um serviço externo pago
    /// e de latência imprevisível. Null = 2 minutos.
    /// </param>
    Task<ResultadoVerificacaoDivergencias> VerificarPendentesAsync(
        Guid? fonteId, int max, TimeSpan? teto = null, CancellationToken ct = default);
}

/// <summary>
/// O árbitro. A chave é a semântica da consulta de CPF: o serviço **só valida quando o par
/// CPF + data de nascimento confere** com a Receita. Então perguntar "este CPF nasceu nesta
/// data?" para cada um dos dois valores em disputa resolve o conflito sem opinião nossa:
///
/// <list type="bullet">
/// <item>a data da ORIGEM valida → <see cref="VeredictoDivergenciaIdentidade.OrigemCorreta"/> (o congelamento cai; o próximo run corrige o hub);</item>
/// <item>a data do HUB valida → <see cref="VeredictoDivergenciaIdentidade.HubCorreto"/> (congelamento PERMANENTE — a origem está errada);</item>
/// <item>nenhuma valida → <see cref="VeredictoDivergenciaIdentidade.AmbosNegados"/> (o CPF é suspeito: provável troca de titular — só olho humano resolve);</item>
/// <item>o motor não respondeu → <see cref="StatusDivergenciaIdentidade.NaoConclusiva"/>, tenta de novo na próxima rodada.</item>
/// </list>
///
/// <para><b>Economia deliberada</b>: cada consulta custa (o Hub é pago e tem saldo). Por isso
/// (a) a origem é consultada primeiro e, validando, a segunda consulta é dispensada — a Receita
/// casa data exata, então duas datas diferentes não podem validar as duas; (b) há teto por
/// rodada; (c) indisponibilidade em sequência aborta a rodada em vez de queimar a fila.</para>
/// </summary>
public sealed class VerificadorDivergenciasPep(
    SmsMaricaDbContext db,
    IConsultaCpfService consultaCpf,
    ILogger<VerificadorDivergenciasPep> logger) : IVerificadorDivergenciasPep
{
    /// <summary>Indisponibilidades seguidas que abortam a rodada (motor fora do ar / sem saldo).</summary>
    private const int LimiteIndisponibilidadeSeguida = 3;

    /// <summary>Respiro entre consultas — não martelar o serviço externo.</summary>
    private static readonly TimeSpan Intervalo = TimeSpan.FromMilliseconds(400);

    /// <summary>Teto de tempo padrão de uma rodada, quando o chamador não informa.</summary>
    public static readonly TimeSpan TetoTempoPadrao = TimeSpan.FromMinutes(2);

    public async Task<ResultadoVerificacaoDivergencias> VerificarPendentesAsync(
        Guid? fonteId, int max, TimeSpan? teto = null, CancellationToken ct = default)
    {
        var limite = Math.Clamp(max, 1, 500);
        var orcamento = teto is { } t && t > TimeSpan.Zero ? t : TetoTempoPadrao;
        var relogio = System.Diagnostics.Stopwatch.StartNew();

        var q = db.PepDivergenciasIdentidade.Where(d =>
            d.Status == StatusDivergenciaIdentidade.Pendente ||
            d.Status == StatusDivergenciaIdentidade.NaoConclusiva);
        if (fonteId is { } fid) q = q.Where(d => d.FonteId == fid);

        // Mais antigas primeiro: a fila anda de forma justa entre rodadas.
        var pendentes = await q.OrderBy(d => d.AtualizadoEm).Take(limite).ToListAsync(ct);
        if (pendentes.Count == 0)
            return new ResultadoVerificacaoDivergencias(0, 0, 0, 0, 0, false);

        int origem = 0, hub = 0, ambos = 0, inconclusivas = 0, seguidasIndisponivel = 0;
        var abortou = false;
        var estourouTempo = false;
        var analisadas = 0;

        foreach (var d in pendentes)
        {
            ct.ThrowIfCancellationRequested();

            // Teto de TEMPO, checado ANTES de gastar a próxima consulta. Limitar só a quantidade
            // não bastava: em 02/08, 35 divergências lentas seguraram a rodada por >11 min.
            if (relogio.Elapsed >= orcamento)
            {
                logger.LogInformation(
                    "Arbitragem interrompida pelo teto de tempo ({Teto}): {Feitas}/{Total} analisadas; o resto volta na próxima rodada.",
                    orcamento, analisadas, pendentes.Count);
                estourouTempo = true;
                break;
            }

            var (veredicto, motor, valorCorreto, nomeOficial, detalhe, indisponivel) =
                await ArbitrarAsync(d, ct);
            analisadas++;

            if (indisponivel)
            {
                seguidasIndisponivel++;
                d.Status = StatusDivergenciaIdentidade.NaoConclusiva;
                d.Detalhe = detalhe;
                d.VerificadoEm = DateTime.UtcNow;
                d.AtualizadoEm = DateTime.UtcNow;
                inconclusivas++;

                if (seguidasIndisponivel >= LimiteIndisponibilidadeSeguida)
                {
                    logger.LogWarning(
                        "Arbitragem de divergências abortada: {N} consultas seguidas indisponíveis. {Feitas}/{Total} analisadas.",
                        seguidasIndisponivel, origem + hub + ambos + inconclusivas, pendentes.Count);
                    abortou = true;
                    break;
                }
                continue;
            }

            seguidasIndisponivel = 0;
            d.Veredicto = veredicto;
            d.VeredictoMotor = motor;
            d.ValorCorreto = valorCorreto;
            d.NomeOficial = nomeOficial;
            d.Detalhe = detalhe;
            d.VerificadoEm = DateTime.UtcNow;
            d.AtualizadoEm = DateTime.UtcNow;
            d.Status = veredicto == VeredictoDivergenciaIdentidade.Inconclusivo
                ? StatusDivergenciaIdentidade.NaoConclusiva
                : StatusDivergenciaIdentidade.Verificada;

            switch (veredicto)
            {
                case VeredictoDivergenciaIdentidade.OrigemCorreta: origem++; break;
                case VeredictoDivergenciaIdentidade.HubCorreto: hub++; break;
                case VeredictoDivergenciaIdentidade.AmbosNegados: ambos++; break;
                default: inconclusivas++; break;
            }

            await Task.Delay(Intervalo, ct);
        }

        // Salva mesmo em interrupção: o que foi arbitrado até aqui não se perde (e sem isto o
        // teto de tempo seria inútil — a rodada pararia sem registrar nada).
        await db.SaveChangesAsync(ct);

        var resultado = new ResultadoVerificacaoDivergencias(
            origem + hub + ambos + inconclusivas, origem, hub, ambos, inconclusivas,
            abortou, estourouTempo, Math.Max(0, pendentes.Count - analisadas));
        logger.LogInformation(
            "Arbitragem: {Analisadas} analisadas em {Seg:F1}s — origem {O}, hub {H}, ambos negados {A}, inconclusivas {I}, restam {R}.",
            resultado.Analisadas, relogio.Elapsed.TotalSeconds, origem, hub, ambos, inconclusivas, resultado.Restantes);
        return resultado;
    }

    /// <summary>
    /// Aplica o <see cref="ArbitroIdentidade"/> (lógica pura) sobre uma linha, ligando o
    /// delegate de consulta à cadeia real de motores. O nome oficial devolvido pela consulta
    /// que VALIDOU é guardado — ajuda a confirmar que é a mesma pessoa na revisão humana.
    /// </summary>
    private async Task<(VeredictoDivergenciaIdentidade Veredicto, string? Motor, string? ValorCorreto,
        string? NomeOficial, string? Detalhe, bool Indisponivel)> ArbitrarAsync(
        PepDivergenciaIdentidade d, CancellationToken ct)
    {
        if (d.Tipo != TipoDivergenciaIdentidade.NascimentoDivergente)
            return (VeredictoDivergenciaIdentidade.Inconclusivo, null, null, null,
                $"Tipo '{d.Tipo}' sem arbitragem automática.", false);

        string? nomeOficial = null;
        var r = await ArbitroIdentidade.ArbitrarNascimentoAsync(d.ValorOrigem, d.ValorHub, async data =>
        {
            var (resultado, resposta, detalhe) = await ConsultarAsync(d.Cpf, data, ct);
            if (resultado == ResultadoConsultaCpf.Validou) nomeOficial = resposta?.Nome;
            return (resultado, detalhe);
        });

        var motor = r.ConsultasGastas > 0 ? MotoresProxy.HubDoDesenvolvedor : null;
        return (r.Veredicto, motor, r.ValorCorreto, nomeOficial, r.Detalhe, r.Indisponivel);
    }

    /// <summary>
    /// Tri-estado da consulta, mapeado das exceções do <c>ProxyExecutor</c>:
    /// <see cref="ValidacaoException"/> = negativa autoritativa de todos os motores;
    /// <see cref="ConflitoException"/> = ninguém respondeu (ou não há motor ativo).
    /// </summary>
    private async Task<(ResultadoConsultaCpf Resultado, HubCpfRespostaDto? Resposta, string? Detalhe)>
        ConsultarAsync(string cpf, DateOnly data, CancellationToken ct)
    {
        try
        {
            var r = await consultaCpf.ConsultarCpfAsync(cpf, data, ct);
            return (ResultadoConsultaCpf.Validou, r, null);
        }
        catch (ValidacaoException ex)
        {
            return (ResultadoConsultaCpf.Negou, null, Resumir(ex.Message));
        }
        catch (ConflitoException ex)
        {
            return (ResultadoConsultaCpf.Indisponivel, null, Resumir(ex.Message));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Consulta de CPF na arbitragem de divergência falhou de forma inesperada.");
            return (ResultadoConsultaCpf.Indisponivel, null, Resumir(ex.Message));
        }
    }

    private static string Resumir(string s)
    {
        var linha = s.Split('\n')[0].Trim();
        return linha.Length <= 200 ? linha : linha[..200];
    }
}
