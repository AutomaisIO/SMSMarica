using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Integracoes.SerWeb;
using SMSMarica.Core.Integracoes.SerWeb.Varredura;
using SMSMarica.Core.Ser.Dtos;
using SMSMarica.Core.Ser.Sessao;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Ser;

/// <summary>
/// As escritas que a tela oferece no SER — hoje, o FollowUP (docs/ser.md §9).
///
/// <para><b>Assinadas pelo operador, nunca pela credencial de sincronismo.</b> A sessão vem de
/// <see cref="ISerSessaoOperadorStore"/>; sem ela a operação é recusada com um código que a tela
/// usa para pedir a senha do SER.</para>
///
/// <para><b>"Registrado!" não é prova.</b> Toda escrita aqui é confirmada RELENDO o histórico do
/// SER — foi a lição de 10/08/2026, quando a Hipótese respondeu "salva com sucesso" e não gravou
/// nada. Só depois de achar o evento na trilha é que gravamos qualquer coisa do nosso lado.</para>
/// </summary>
public interface ISerEscritaService
{
    Task<SerFollowUpResultadoDto> RegistrarFollowUpAsync(
        Guid solicitacaoId, string texto, CancellationToken cancellationToken);
}

public sealed class SerEscritaService(
    SmsMaricaDbContext db,
    ISerSessaoOperadorStore sessoes,
    IUsuarioAtualAccessor usuarioAtual,
    ILoggerFactory loggerFactory,
    ILogger<SerEscritaService> logger) : ISerEscritaService
{
    public async Task<SerFollowUpResultadoDto> RegistrarFollowUpAsync(
        Guid solicitacaoId, string texto, CancellationToken cancellationToken)
    {
        var limpo = (texto ?? string.Empty).Trim();
        if (limpo.Length == 0)
        {
            throw new ValidacaoException(
                "ser.followup_sem_texto", "Escreva a observação do FollowUP.");
        }

        var solicitacao = await db.SerSolicitacoes
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId, cancellationToken)
            ?? throw new NaoEncontradoException("Solicitação do SER", solicitacaoId);

        var operador = usuarioAtual.UsuarioId;
        var sessaoId = usuarioAtual.SessaoId
            ?? throw new ValidacaoException(
                "ser.sem_operador",
                "Escrita no SER exige um usuário autenticado — a ação é assinada por quem a fez.");

        var sessao = sessoes.Exigir(sessaoId);
        var leitor = new SerLeitorService(sessao, loggerFactory.CreateLogger<SerLeitorService>());

        string mensagemDoSer;
        try
        {
            mensagemDoSer = await leitor.RegistrarFollowUpAsync(
                solicitacao.IdSer, solicitacao.Situacao, limpo, cancellationToken);
        }
        catch (FollowUpSerIndisponivelException ex)
        {
            // Não é erro do sistema: o SER simplesmente não oferece a ação nessa situação.
            throw new ValidacaoException("ser.followup_indisponivel", ex.Message);
        }

        logger.LogInformation(
            "SER: FollowUP enviado para {IdSer} por {Usuario}; SER respondeu: {Mensagem}",
            solicitacao.IdSer, operador, mensagemDoSer);

        // ---- CONFIRMAÇÃO: reler a trilha do zero. Sem isto, "registrado!" viraria prova.
        var historico = await leitor.LerHistoricoPorIdAsync(
            solicitacao.IdSer, solicitacao.Situacao, cancellationToken);

        var confirmado = historico.Eventos
            .Where(e => EhFollowUp(e.Evento) && ComparaTexto(e.Observacao, limpo))
            .OrderByDescending(e => ParaData(e.Data) ?? DateTime.MinValue)
            .FirstOrDefault();

        if (confirmado is null)
        {
            throw new ValidacaoException(
                "ser.followup_nao_confirmado",
                "O SER respondeu " + (string.IsNullOrWhiteSpace(mensagemDoSer)
                    ? "sem mensagem"
                    : $"\"{mensagemDoSer}\"")
                + ", mas o FollowUP não apareceu na releitura do histórico. Confira na tela do "
                + "SER antes de tentar de novo — repetir pode duplicar o registro.");
        }

        var novos = await GravarEventosNovosAsync(solicitacao, historico, cancellationToken);

        return new SerFollowUpResultadoDto(
            solicitacao.IdSer,
            mensagemDoSer,
            new SerEventoDiretoDto(
                confirmado.Data, confirmado.Evento, confirmado.EstadoAnterior, confirmado.EstadoAtual,
                confirmado.CentralRegulacao, confirmado.UnidadeExecutora, confirmado.Usuario,
                confirmado.LotacaoEvento, confirmado.Ip, confirmado.Observacao),
            novos);
    }

    /// <summary>
    /// Espelha na nossa base os eventos que a releitura trouxe — inclusive o que acabamos de
    /// criar, senão a tela mostraria a trilha sem a ação do próprio operador até a próxima
    /// varredura.
    ///
    /// <para><b>Não gera gatilho.</b> Gatilho é "o Estado mexeu, alguém precisa olhar"; notificar
    /// o município sobre a própria ação encheria a fila de notificações de ruído.</para>
    /// </summary>
    private async Task<int> GravarEventosNovosAsync(
        SerSolicitacao solicitacao, SerHistorico historico, CancellationToken cancellationToken)
    {
        var jaTemos = await db.SerEventos
            .Where(e => e.SerSolicitacaoId == solicitacao.Id)
            .Select(e => new { e.DataEvento, e.Evento })
            .ToListAsync(cancellationToken);

        // Mesma chave do sincronizador: o SER não numera eventos, a identidade é (data, evento).
        var conhecidos = jaTemos
            .Select(e => Chave(e.DataEvento, e.Evento))
            .ToHashSet(StringComparer.Ordinal);

        var agora = DateTime.UtcNow;
        var maisRecente = solicitacao.UltimoEventoEm;
        var novos = 0;

        foreach (var lido in historico.Eventos)
        {
            var data = ParaData(lido.Data);
            if (data is null || string.IsNullOrWhiteSpace(lido.Evento)) continue;

            if (maisRecente is null || data > maisRecente) maisRecente = data;
            if (!conhecidos.Add(Chave(data.Value, lido.Evento!))) continue;

            db.SerEventos.Add(new SerEvento
            {
                Id = Guid.NewGuid(),
                SerSolicitacaoId = solicitacao.Id,
                DataEvento = data.Value,
                Evento = lido.Evento!,
                EstadoAnterior = lido.EstadoAnterior,
                EstadoAtual = lido.EstadoAtual,
                CentralRegulacao = lido.CentralRegulacao,
                UnidadeExecutora = lido.UnidadeExecutora,
                Usuario = lido.Usuario,
                LotacaoEvento = lido.LotacaoEvento,
                Ip = lido.Ip,
                Observacao = lido.Observacao,
                CapturadoEm = agora,
            });
            novos++;
        }

        solicitacao.HistoricoLidoEm = agora;
        solicitacao.EventosCount = conhecidos.Count;
        solicitacao.UltimoEventoEm = maisRecente;

        await db.SaveChangesAsync(cancellationToken);
        return novos;
    }

    /// <summary>O SER escreve "FollowUP"; toleramos caixa e hífen, como o resto do motor.</summary>
    private static bool EhFollowUp(string? evento) =>
        (evento ?? string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty)
            .Contains("followup", StringComparison.OrdinalIgnoreCase);

    /// <summary>O SER normaliza espaços do texto ao exibir; comparar cru daria falso negativo.</summary>
    private static bool ComparaTexto(string? doSer, string? enviado) =>
        string.Equals(Espremer(doSer), Espremer(enviado), StringComparison.OrdinalIgnoreCase);

    private static string Espremer(string? t) =>
        string.Join(' ', (t ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Chave(DateTime data, string evento) =>
        $"{data:O}|{evento.Trim().ToLowerInvariant()}";

    private static DateTime? ParaData(string? texto)
    {
        var t = texto?.Trim();
        if (string.IsNullOrEmpty(t)) return null;

        string[] formatos = ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy"];
        // O SER grava wall-clock de Brasília e a coluna é `timestamp with time zone`.
        return DateTime.TryParseExact(t, formatos, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt)
            ? FusoBrasilia.DeBrasiliaParaUtc(dt)
            : null;
    }
}
