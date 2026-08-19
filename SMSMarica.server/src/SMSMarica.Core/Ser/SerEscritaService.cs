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

    /// <summary>Lê os três telefones direto da tela de edição do SER (nada é gravado).</summary>
    Task<SerContatosDto> LerContatosAsync(Guid solicitacaoId, CancellationToken cancellationToken);

    /// <summary>Altera os telefones NO SER. Não toca no nosso hub FHIR.</summary>
    Task<SerContatosDto> AlterarContatosAsync(
        Guid solicitacaoId, SerAlterarContatosRequest pedido, CancellationToken cancellationToken);
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

        // Marco temporal ANTES de escrever: é ele que separa "o meu FollowUP entrou" de "já
        // existia um evento com este texto". Sem isso, repetir a mesma observação numa
        // solicitação que já a tinha confirmaria o registro pelo evento ANTIGO — e uma gravação
        // que falhou passaria por bem-sucedida.
        var inicio = DateTime.UtcNow;

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
            .Select(e => new { Evento = e, Data = ParaData(e.Data) })
            // Só conta evento DESTA gravação. A folga cobre defasagem de relógio entre a nossa
            // máquina e a do Estado; ela é curta o bastante para não aceitar um FollowUP igual
            // registrado ontem.
            .Where(x => x.Data is not null && x.Data >= inicio - FolgaDeRelogio)
            .OrderByDescending(x => x.Data)
            .Select(x => x.Evento)
            .FirstOrDefault();

        if (confirmado is null)
        {
            throw new ValidacaoException(
                "ser.followup_nao_confirmado",
                "O SER respondeu " + (string.IsNullOrWhiteSpace(mensagemDoSer)
                    ? "sem mensagem"
                    : $"\"{mensagemDoSer}\"")
                + ", mas o FollowUP não apareceu como evento novo na releitura do histórico. "
                + "Confira na tela do SER antes de tentar de novo — repetir pode duplicar o "
                + "registro.");
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

    public async Task<SerContatosDto> LerContatosAsync(
        Guid solicitacaoId, CancellationToken cancellationToken)
    {
        var (solicitacao, leitor) = await PrepararAsync(solicitacaoId, cancellationToken);

        try
        {
            var lidos = await leitor.LerContatosAsync(
                solicitacao.IdSer, solicitacao.Situacao, cancellationToken);
            return ParaDto(lidos, editavel: true, motivo: null);
        }
        catch (EdicaoSerIndisponivelException ex)
        {
            // Não é erro: a situação simplesmente não permite editar. A tela usa isto para
            // mostrar os telefones do espelho sem oferecer o botão.
            return new SerContatosDto(
                solicitacao.TelefoneResidencial,
                solicitacao.TelefoneWhatsapp,
                solicitacao.TelefoneContato,
                Editavel: false,
                MotivoNaoEditavel: ex.Message);
        }
    }

    public async Task<SerContatosDto> AlterarContatosAsync(
        Guid solicitacaoId, SerAlterarContatosRequest pedido, CancellationToken cancellationToken)
    {
        var novos = new Dictionary<string, string>(StringComparer.Ordinal);
        // `null` = não mexer; vazio = limpar. Sem essa distinção, "não informei" apagaria o
        // telefone que o Estado tem.
        if (pedido.Residencial is not null) novos["residencial"] = pedido.Residencial;
        if (pedido.WhatsApp is not null) novos["whatsapp"] = pedido.WhatsApp;
        if (pedido.Contato is not null) novos["contato"] = pedido.Contato;

        if (novos.Count == 0)
        {
            throw new ValidacaoException(
                "ser.contatos_sem_alteracao", "Nenhum telefone foi informado para alterar.");
        }

        var (solicitacao, leitor) = await PrepararAsync(solicitacaoId, cancellationToken);

        SerContatosDaTela confirmados;
        try
        {
            confirmados = await leitor.AlterarContatosAsync(
                solicitacao.IdSer, solicitacao.Situacao, novos, cancellationToken);
        }
        catch (EdicaoSerIndisponivelException ex)
        {
            throw new ValidacaoException("ser.edicao_indisponivel", ex.Message);
        }

        // ---- CONFERÊNCIA: o que voltou é o que a tela do SER mostra depois de gravar.
        var naoAplicados = novos
            .Where(n => !Bate(n.Key, n.Value, confirmados))
            .Select(n => n.Key)
            .ToList();

        if (naoAplicados.Count > 0)
        {
            throw new ValidacaoException(
                "ser.contatos_nao_confirmados",
                $"O SER aceitou o envio, mas ao reler a tela {string.Join(" e ", naoAplicados)} "
                + "continua(m) com o valor anterior. Confira na tela do SER — não repita às cegas.");
        }

        AtualizarEspelho(solicitacao, confirmados);
        await db.SaveChangesAsync(cancellationToken);

        return ParaDto(confirmados, editavel: true, motivo: null);
    }

    /// <summary>
    /// Alinha o espelho (<c>ser_solicitacao</c>) com o que o SER passou a ter.
    ///
    /// <para><b>E NÃO toca no hub FHIR — decisão do Bernardo (18/08/2026).</b> Telefone no FHIR
    /// tem regras próprias que esta tela não conhece: o marcador de verificado por OTP é a fonte
    /// única, e contato só ACUMULA (merge nunca apaga — o incidente de 10/08 apagou 8.634
    /// números). Deixar o operador reescrever o telefone do Estado é uma coisa; deixar isso
    /// reescrever o cadastro clínico do cidadão é outra, e não foi pedida.</para>
    ///
    /// <para>Efeito colateral consciente: como espelho e SER passam a bater, a próxima varredura
    /// não vê mudança e não dispara conciliação — ou seja, a alteração fica no SER e no espelho,
    /// e não vaza para o FHIR. É exatamente o que se quer aqui.</para>
    /// </summary>
    private static void AtualizarEspelho(SerSolicitacao s, SerContatosDaTela c)
    {
        if (c.Residencial is { } r) s.TelefoneResidencial = Vazio(r.Valor);
        if (c.WhatsApp is { } w) s.TelefoneWhatsapp = Vazio(w.Valor);
        if (c.Contato is { } t) s.TelefoneContato = Vazio(t.Valor);
    }

    private async Task<(SerSolicitacao Solicitacao, SerLeitorService Leitor)> PrepararAsync(
        Guid solicitacaoId, CancellationToken cancellationToken)
    {
        var solicitacao = await db.SerSolicitacoes
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId, cancellationToken)
            ?? throw new NaoEncontradoException("Solicitação do SER", solicitacaoId);

        var sessaoId = usuarioAtual.SessaoId
            ?? throw new ValidacaoException(
                "ser.sem_operador",
                "Escrita no SER exige um usuário autenticado — a ação é assinada por quem a fez.");

        var sessao = sessoes.Exigir(sessaoId);
        return (solicitacao, new SerLeitorService(sessao, loggerFactory.CreateLogger<SerLeitorService>()));
    }

    private static bool Bate(string chave, string enviado, SerContatosDaTela c)
    {
        var atual = chave switch
        {
            "residencial" => c.Residencial?.Valor,
            "whatsapp" => c.WhatsApp?.Valor,
            _ => c.Contato?.Valor,
        };

        // O SER aplica máscara ao gravar ("21987654321" volta "(21) 98765-4321"): comparar só os
        // dígitos é o que distingue "formatou" de "ignorou".
        return string.Equals(SoDigitos(atual), SoDigitos(enviado), StringComparison.Ordinal);
    }

    private static string SoDigitos(string? t) =>
        new([.. (t ?? string.Empty).Where(char.IsDigit)]);

    private static string? Vazio(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    private static SerContatosDto ParaDto(SerContatosDaTela c, bool editavel, string? motivo) =>
        new(Vazio(c.Residencial?.Valor), Vazio(c.WhatsApp?.Valor), Vazio(c.Contato?.Valor),
            editavel, motivo);

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

    /// <summary>
    /// Folga para a diferença de relógio entre a nossa máquina e a do SER ao decidir se um evento
    /// é "desta gravação". Generosa de propósito: errar para menos rejeitaria um registro que deu
    /// certo, e o custo disso é o operador conferir à toa na tela do Estado.
    /// </summary>
    private static readonly TimeSpan FolgaDeRelogio = TimeSpan.FromMinutes(10);

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
