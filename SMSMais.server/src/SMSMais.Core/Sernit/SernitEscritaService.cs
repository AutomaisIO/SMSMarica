using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.SernitWeb;
using SMSMais.Core.Integracoes.SernitWeb.Varredura;
using SMSMais.Core.Sernit.Dtos;
using SMSMais.Core.Sernit.Sessao;
using SMSMais.Data;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Sernit;

/// <summary>
/// As escritas que a tela oferece no SERNIT: FollowUP e alteração de telefones — subsistema irmão
/// do SER-RJ.
///
/// <para><b>Assinadas pelo operador</b> (sessão de <see cref="ISernitSessaoOperadorStore"/>); sem
/// ela a operação é recusada com um código que a tela usa para pedir a senha.</para>
///
/// <para><b>"Registrado!" não é prova:</b> toda escrita é confirmada RELENDO o histórico do SERNIT.</para>
/// </summary>
public interface ISernitEscritaService
{
    Task<SernitFollowUpResultadoDto> RegistrarFollowUpAsync(
        Guid solicitacaoId, string texto, CancellationToken cancellationToken);

    Task<SernitContatosDto> LerContatosAsync(Guid solicitacaoId, CancellationToken cancellationToken);

    Task<SernitContatosDto> AlterarContatosAsync(
        Guid solicitacaoId, SernitAlterarContatosRequest pedido, CancellationToken cancellationToken);
}

public sealed class SernitEscritaService(
    SmsMaisDbContext db,
    ISernitSessaoOperadorStore sessoes,
    IUsuarioAtualAccessor usuarioAtual,
    ILoggerFactory loggerFactory,
    ILogger<SernitEscritaService> logger) : ISernitEscritaService
{
    public async Task<SernitFollowUpResultadoDto> RegistrarFollowUpAsync(
        Guid solicitacaoId, string texto, CancellationToken cancellationToken)
    {
        var limpo = (texto ?? string.Empty).Trim();
        if (limpo.Length == 0)
        {
            throw new ValidacaoException("sernit.followup_sem_texto", "Escreva a observação do FollowUP.");
        }

        var (solicitacao, leitor) = await PrepararAsync(solicitacaoId, cancellationToken);
        var operador = usuarioAtual.UsuarioId;

        // Marco temporal ANTES de escrever: separa "o meu FollowUP entrou" de "já existia".
        var inicio = DateTime.UtcNow;

        string mensagemDoSernit;
        try
        {
            mensagemDoSernit = await leitor.RegistrarFollowUpAsync(
                solicitacao.IdSernit, solicitacao.Situacao, limpo, cancellationToken);
        }
        catch (FollowUpSernitIndisponivelException ex)
        {
            throw new ValidacaoException("sernit.followup_indisponivel", ex.Message);
        }

        logger.LogInformation(
            "SERNIT: FollowUP enviado para {Id} por {Usuario}; SERNIT respondeu: {Mensagem}",
            solicitacao.IdSernit, operador, mensagemDoSernit);

        // CONFIRMAÇÃO: reler a trilha do zero.
        var historico = await leitor.LerHistoricoPorIdAsync(
            solicitacao.IdSernit, solicitacao.Situacao, cancellationToken);

        var confirmado = historico.Eventos
            .Where(e => EhFollowUp(e.Evento) && ComparaTexto(e.Observacao, limpo))
            .Select(e => new { Evento = e, Data = ParaData(e.Data) })
            .Where(x => x.Data is not null && x.Data >= inicio - FolgaDeRelogio)
            .OrderByDescending(x => x.Data)
            .Select(x => x.Evento)
            .FirstOrDefault();

        if (confirmado is null)
        {
            throw new ValidacaoException(
                "sernit.followup_nao_confirmado",
                "O SERNIT respondeu " + (string.IsNullOrWhiteSpace(mensagemDoSernit)
                    ? "sem mensagem"
                    : $"\"{mensagemDoSernit}\"")
                + ", mas o FollowUP não apareceu como evento novo na releitura do histórico. "
                + "Confira na tela do SERNIT antes de tentar de novo — repetir pode duplicar o registro.");
        }

        var novos = await GravarEventosNovosAsync(solicitacao, historico, cancellationToken);

        return new SernitFollowUpResultadoDto(
            solicitacao.IdSernit,
            mensagemDoSernit,
            new SernitEventoDiretoDto(
                confirmado.Data, confirmado.Evento, confirmado.EstadoAnterior, confirmado.EstadoAtual,
                confirmado.CentralRegulacao, confirmado.UnidadeExecutora, confirmado.Usuario,
                confirmado.LotacaoEvento, confirmado.Ip, confirmado.Observacao),
            novos);
    }

    public async Task<SernitContatosDto> LerContatosAsync(
        Guid solicitacaoId, CancellationToken cancellationToken)
    {
        var (solicitacao, leitor) = await PrepararAsync(solicitacaoId, cancellationToken);

        try
        {
            var lidos = await leitor.LerContatosAsync(
                solicitacao.IdSernit, solicitacao.Situacao, cancellationToken);
            return ParaDto(lidos, editavel: true, motivo: null);
        }
        catch (EdicaoSernitIndisponivelException ex)
        {
            return new SernitContatosDto(
                solicitacao.TelefoneResidencial,
                solicitacao.TelefoneWhatsapp,
                solicitacao.TelefoneContato,
                Editavel: false,
                MotivoNaoEditavel: ex.Message);
        }
    }

    public async Task<SernitContatosDto> AlterarContatosAsync(
        Guid solicitacaoId, SernitAlterarContatosRequest pedido, CancellationToken cancellationToken)
    {
        var novos = new Dictionary<string, string>(StringComparer.Ordinal);
        if (pedido.Residencial is not null) novos["residencial"] = pedido.Residencial;
        if (pedido.WhatsApp is not null) novos["whatsapp"] = pedido.WhatsApp;
        if (pedido.Contato is not null) novos["contato"] = pedido.Contato;

        if (novos.Count == 0)
        {
            throw new ValidacaoException(
                "sernit.contatos_sem_alteracao", "Nenhum telefone foi informado para alterar.");
        }

        var (solicitacao, leitor) = await PrepararAsync(solicitacaoId, cancellationToken);

        SernitContatosDaTela confirmados;
        try
        {
            confirmados = await leitor.AlterarContatosAsync(
                solicitacao.IdSernit, solicitacao.Situacao, novos, cancellationToken);
        }
        catch (EdicaoSernitIndisponivelException ex)
        {
            throw new ValidacaoException("sernit.edicao_indisponivel", ex.Message);
        }

        var naoAplicados = novos
            .Where(n => !Bate(n.Key, n.Value, confirmados))
            .Select(n => n.Key)
            .ToList();

        if (naoAplicados.Count > 0)
        {
            throw new ValidacaoException(
                "sernit.contatos_nao_confirmados",
                $"O SERNIT aceitou o envio, mas ao reler a tela {string.Join(" e ", naoAplicados)} "
                + "continua(m) com o valor anterior. Confira na tela do SERNIT — não repita às cegas.");
        }

        AtualizarEspelho(solicitacao, confirmados);
        await db.SaveChangesAsync(cancellationToken);

        return ParaDto(confirmados, editavel: true, motivo: null);
    }

    /// <summary>
    /// Alinha o espelho (<c>sernit_solicitacao</c>) com o que o SERNIT passou a ter. <b>Não toca no
    /// hub FHIR</b> (mesma decisão do SER-RJ): telefone no FHIR tem regras próprias (verificado por
    /// OTP, contato só acumula). Como espelho e SERNIT passam a bater, a varredura seguinte não vê
    /// mudança e não dispara conciliação — a alteração fica no SERNIT e no espelho.
    /// </summary>
    private static void AtualizarEspelho(SernitSolicitacao s, SernitContatosDaTela c)
    {
        if (c.Residencial is { } r) s.TelefoneResidencial = Vazio(r.Valor);
        if (c.WhatsApp is { } w) s.TelefoneWhatsapp = Vazio(w.Valor);
        if (c.Contato is { } t) s.TelefoneContato = Vazio(t.Valor);
    }

    private async Task<(SernitSolicitacao Solicitacao, SernitLeitorService Leitor)> PrepararAsync(
        Guid solicitacaoId, CancellationToken cancellationToken)
    {
        var solicitacao = await db.SernitSolicitacoes
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId, cancellationToken)
            ?? throw new NaoEncontradoException("Solicitação do SERNIT", solicitacaoId);

        var sessaoId = usuarioAtual.SessaoId
            ?? throw new ValidacaoException(
                "sernit.sem_operador",
                "Escrita no SERNIT exige um usuário autenticado — a ação é assinada por quem a fez.");

        var sessao = sessoes.Exigir(sessaoId);
        return (solicitacao, new SernitLeitorService(sessao, loggerFactory.CreateLogger<SernitLeitorService>()));
    }

    private static bool Bate(string chave, string enviado, SernitContatosDaTela c)
    {
        var atual = chave switch
        {
            "residencial" => c.Residencial?.Valor,
            "whatsapp" => c.WhatsApp?.Valor,
            _ => c.Contato?.Valor,
        };

        // O SERNIT aplica máscara ao gravar: comparar só os dígitos distingue "formatou" de "ignorou".
        return string.Equals(SoDigitos(atual), SoDigitos(enviado), StringComparison.Ordinal);
    }

    private static string SoDigitos(string? t) => new([.. (t ?? string.Empty).Where(char.IsDigit)]);

    private static string? Vazio(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim();

    private static SernitContatosDto ParaDto(SernitContatosDaTela c, bool editavel, string? motivo) =>
        new(Vazio(c.Residencial?.Valor), Vazio(c.WhatsApp?.Valor), Vazio(c.Contato?.Valor), editavel, motivo);

    private async Task<int> GravarEventosNovosAsync(
        SernitSolicitacao solicitacao, SernitHistorico historico, CancellationToken cancellationToken)
    {
        var jaTemos = await db.SernitEventos
            .Where(e => e.SernitSolicitacaoId == solicitacao.Id)
            .Select(e => new { e.DataEvento, e.Evento })
            .ToListAsync(cancellationToken);

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

            db.SernitEventos.Add(new SernitEvento
            {
                Id = Guid.NewGuid(),
                SernitSolicitacaoId = solicitacao.Id,
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

    private static readonly TimeSpan FolgaDeRelogio = TimeSpan.FromMinutes(10);

    private static bool EhFollowUp(string? evento) =>
        (evento ?? string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty)
            .Contains("followup", StringComparison.OrdinalIgnoreCase);

    private static bool ComparaTexto(string? doSernit, string? enviado) =>
        string.Equals(Espremer(doSernit), Espremer(enviado), StringComparison.OrdinalIgnoreCase);

    private static string Espremer(string? t) =>
        string.Join(' ', (t ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static string Chave(DateTime data, string evento) =>
        $"{data:O}|{evento.Trim().ToLowerInvariant()}";

    private static DateTime? ParaData(string? texto)
    {
        var t = texto?.Trim();
        if (string.IsNullOrEmpty(t)) return null;

        string[] formatos = ["dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy"];
        return DateTime.TryParseExact(t, formatos, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt)
            ? FusoBrasilia.DeBrasiliaParaUtc(dt)
            : null;
    }
}
