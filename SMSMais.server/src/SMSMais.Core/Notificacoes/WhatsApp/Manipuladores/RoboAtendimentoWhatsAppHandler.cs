using Microsoft.EntityFrameworkCore;
using SMSMais.Core.RoboAtendimento.Runtime;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;

/// <summary>
/// Robô de atendimento: quando nenhum fluxo determinístico tratou a mensagem e nenhum humano
/// está na janela, ENFILEIRA uma tarefa (robo_tarefa) para o worker responder fora do webhook.
/// Roda por último (Ordem alta). NÃO chama SaveChanges — o webhook commita.
/// </summary>
public sealed class RoboAtendimentoWhatsAppHandler(
    SmsMaisDbContext db,
    IWhatsAppCliente whatsApp,
    Microsoft.Extensions.Logging.ILogger<RoboAtendimentoWhatsAppHandler> logger) : IManipuladorMensagemWhatsApp
{
    public int Ordem => 1000; // depois de todos os fluxos de domínio.

    public async Task TratarAsync(ManipuladorContexto ctx, CancellationToken ct)
    {
        if (ctx.Consumido) return;

        // Bloqueio forte por conversa (operador clicou "Parar robô"): vence tudo, inclusive a virada
        // de horário. Só é limpo ao devolver a conversa ao robô.
        if (ctx.Conversa.RoboBloqueado) return;

        var ehAtendente = ctx.BotaoPayload?.StartsWith("atendente:", StringComparison.Ordinal) == true
            || EhPedidoLiteralDeAtendente(ctx.Texto);
        // Botões/replies de outros domínios não são do robô; e sem texto (a menos que seja o botão atendente) não há o que tratar.
        if (!ehAtendente && !string.IsNullOrEmpty(ctx.BotaoPayload)) return;
        if (!string.IsNullOrEmpty(ctx.InterativoReplyId)) return;
        if (!ehAtendente && string.IsNullOrWhiteSpace(ctx.Texto)) return;

        // Robô ligado globalmente? (e o expediente humano, para o override por horário)
        var cfg = await db.RoboConfiguracoes.AsNoTracking()
            .Select(c => new { c.Ativo, c.NomeExibicao, c.HoraAtendimentoHumanoInicio, c.HoraAtendimentoHumanoFim, c.DiasSemanaAtendimentoHumano })
            .FirstOrDefaultAsync(ct);
        if (cfg is not { Ativo: true }) return;

        // Diálogo de confirmação em andamento (Sim/Não/motivo) é do fluxo determinístico — não interferir.
        var temEstadoConfirmacao = await db.AgendamentoConfirmacaoEstados.AsNoTracking().AnyAsync(
            e => e.TelefoneCanonical == ctx.Conversa.TelefoneCanonical && e.ExpiraEm > DateTime.UtcNow, ct);
        if (temEstadoConfirmacao) return;

        // Verificação cadastral em andamento idem — a máquina determinística conduz, o robô se cala.
        var temVerificacao = await db.VerificacoesCadastraisEstado.AsNoTracking().AnyAsync(
            e => e.TelefoneCanonical == ctx.Conversa.TelefoneCanonical && e.ExpiraEm > DateTime.UtcNow, ct);
        if (temVerificacao) return;

        // Trava humano: se um humano já respondeu/assumiu desde o corte, cala. Dentro do expediente o
        // corte é a âncora da janela; FORA dele, só a atividade RECENTE cala (o robô assume quando os
        // atendentes saíram, mas recua se um deles acabou de agir — ex.: correção manual às 20h).
        var foraExpediente = TravaHumano.ForaDoExpedienteHumano(
            cfg.HoraAtendimentoHumanoInicio, cfg.HoraAtendimentoHumanoFim, DateTime.UtcNow,
            cfg.DiasSemanaAtendimentoHumano);
        var ancora = TravaHumano.AncoraEfetiva(
            ctx.Conversa.JanelaAbertaEm, ctx.Mensagem.OcorridoEm, ctx.Conversa.RoboRearmadoEm);
        var corteHumano = TravaHumano.CorteHumano(ancora, foraExpediente, DateTime.UtcNow);
        var humanoRespondeu = await db.MensagensWhatsApp.AsNoTracking().AnyAsync(
            m => m.ConversaId == ctx.Conversa.Id && m.Direcao == DirecaoMensagem.Saida
                && m.AutorUsuarioId != null && m.OcorridoEm >= corteHumano, ct);
        if (humanoRespondeu) return;
        var humanoAssumiu = await db.ConversaEventos.AsNoTracking().AnyAsync(
            e => e.ConversaId == ctx.Conversa.Id && e.AtorUsuarioId != null && e.OcorridoEm >= corteHumano
                && (e.Tipo == TipoEventoConversa.Assumida
                    || e.Tipo == TipoEventoConversa.Transferida
                    || e.Tipo == TipoEventoConversa.EncaminhadaUnidade), ct);
        if (humanoAssumiu) return;

        // BOTÃO "Falar com um atendente" (quick-reply do template, ou o texto literal): é pedido
        // DIRETO de humano — não é conversa para o robô decidir. O clique já passou por um LLM
        // duas noites e deu nas duas caras da moeda no MESMO minuto (22h56 de 01/09): para uma
        // pessoa a resposta certa; para outra, "estou transferindo você, aguarde um momento" às
        // onze da noite. Botão não vai mais a modelo:
        // - DENTRO do expediente: silêncio total — a conversa está na fila e um HUMANO atende
        //   imediatamente (é a regra do produto).
        // - FORA do expediente: o ROBÔ assume o atendimento, com abertura DETERMINÍSTICA que
        //   oferece ajuda SEM falar de horário. Só se a pessoa insistir em humano DURANTE esse
        //   atendimento é que o modelo declara o fora-do-horário (guardrail já manda).
        if (ehAtendente)
        {
            if (!foraExpediente) return;
            await AbrirAtendimentoForaDoHorarioAsync(ctx, cfg.NomeExibicao, ct);
            return;
        }

        // CORTESIA AO ATENDENTE: a última mensagem enviada foi de um HUMANO e o cidadão respondeu
        // só um agradecimento/emoji/ok — é o fecho da interação humana, não um pedido novo. O robô
        // reabrindo ("Oi! Como posso ajudar?") vira ruído — caso real de 01/09 22h28: "👏" para o
        // aviso da atendente e o robô recomeçou o atendimento. Nem cria tarefa.
        if (EhCortesiaPura(ctx.Mensagem.Conteudo))
        {
            var ultimaSaida = await db.MensagensWhatsApp.AsNoTracking()
                .Where(m => m.ConversaId == ctx.Conversa.Id && m.Direcao == DirecaoMensagem.Saida
                    && m.TipoMensagem != TipoMensagem.NotaInterna)
                .OrderByDescending(m => m.OcorridoEm)
                .Select(m => new { m.AutorUsuarioId, m.TipoMensagem })
                .FirstOrDefaultAsync(ct);
            if (ultimaSaida is { AutorUsuarioId: not null } && ultimaSaida.TipoMensagem != TipoMensagem.Robo)
                return;
        }

        // NÃO existe limite de interações aqui. Havia um `>= 8` fixo no código, ABAIXO do limite
        // configurável por assunto — e, como o handler nem chega a criar a tarefa, a mensagem sumia
        // sem deixar rastro: o cidadão escrevia e não recebia nada, nem o aviso de passagem. Dois
        // limites concorrentes, e o invisível ganhava.
        //
        // O limite de verdade é `MaxInteracoesSemResolver`, por assunto, aplicado pelo processador —
        // que ainda AVISA o cidadão antes de se calar. Enfileirar é barato: passado o limite, o
        // processador encerra a tarefa sem chamar o modelo, então nada de custo escapa por aqui.

        db.RoboTarefas.Add(new RoboAtendimentoTarefa
        {
            Id = Guid.CreateVersion7(),
            ConversaId = ctx.Conversa.Id,
            MensagemWhatsAppId = ctx.Mensagem.Id,
            PacienteId = ctx.PacienteId,
            Status = StatusRoboTarefa.Pendente,
            CriadoEm = DateTime.UtcNow,
        });
    }

    /// <summary>Mensagem que é SÓ cortesia/fecho (emojis, "ok", "obrigada", "confirmado, estarei
    /// lá") — sem conteúdo novo. Régua conservadora: qualquer palavra fora da lista já NÃO é
    /// cortesia pura e segue o fluxo normal.</summary>
    private static bool EhCortesiaPura(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || texto.Length > 60) return false;
        var norm = RoboAtendimento.Runtime.RoboClassificador.NormalizarTexto(texto);
        var letras = new string([.. norm.Select(c => char.IsLetter(c) ? c : ' ')]);
        var palavras = letras.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (palavras.Length > 6) return false;
        // Sem nenhuma palavra: era só emoji/pontuação — cortesia.
        if (palavras.Length == 0) return true;
        string[] cortesia =
        [
            "ok", "okay", "blz", "beleza", "obrigado", "obrigada", "obg", "obgd", "grato", "grata",
            "gratidao", "valeu", "de", "nada", "amem", "ciente", "confirmado", "confirmo", "sim",
            "ta", "tá", "bom", "bem", "boa", "tarde", "noite", "dia", "certo", "perfeito", "tudo",
            "vou", "estarei", "la", "muito", "deus", "abencoe", "bencao", "joia",
        ];
        return palavras.All(p => cortesia.Contains(p, StringComparer.Ordinal));
    }

    /// <summary>O texto é exatamente o rótulo do botão ("Falar com um atendente")? Digitado
    /// idêntico conta como botão. Frases maiores ("quero falar com atendente sobre a guia")
    /// continuam indo ao robô — aí é conversa, não clique.</summary>
    private static bool EhPedidoLiteralDeAtendente(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return false;
        var n = RoboAtendimento.Runtime.RoboClassificador.NormalizarTexto(texto).Trim('.', '!', ' ');
        return n is "falar com um atendente" or "falar com atendente";
    }

    /// <summary>
    /// Abertura determinística do atendimento fora do expediente: o robô assume, oferecendo ajuda
    /// SEM mencionar horário (a regra do produto: fora do horário, a gente atende; o horário só é
    /// declarado se a pessoa insistir em humano durante o atendimento). Sem LLM — zero variância.
    /// </summary>
    private async Task AbrirAtendimentoForaDoHorarioAsync(ManipuladorContexto ctx, string nomeRobo, CancellationToken ct)
    {
        const string Abertura =
            "Olá! Posso te ajudar por aqui. Me conta, por favor: o que você precisa? "
            + "Se for sobre agendamento, exame ou cadastro, já verifico agora mesmo.";

        // Clique duplo / reenvio do template: não repete a abertura se ela já foi a última fala
        // do robô nas últimas horas.
        var corte = DateTime.UtcNow.AddHours(-6);
        var jaAbriu = await db.MensagensWhatsApp.AsNoTracking().AnyAsync(
            m => m.ConversaId == ctx.Conversa.Id && m.Direcao == DirecaoMensagem.Saida
                && m.TipoMensagem == TipoMensagem.Robo && m.OcorridoEm >= corte
                && m.Conteudo == Abertura, ct);
        if (jaAbriu) return;

        try
        {
            var envio = await whatsApp.EnviarTextoAsync(
                ctx.Conversa.TelefoneCanonical, Abertura, pacienteId: ctx.Conversa.PacienteId, ct: ct);
            if (!envio.Ok)
            {
                Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(
                    logger, "Falha ao abrir atendimento fora do horário na conversa {Conversa}: {Erro}",
                    ctx.Conversa.Id, envio.Erro);
                return;
            }

            if (!string.IsNullOrEmpty(envio.WaMessageId))
            {
                var msg = db.MensagensWhatsApp.Local.FirstOrDefault(m => m.WaMessageId == envio.WaMessageId)
                    ?? await db.MensagensWhatsApp.FirstOrDefaultAsync(m => m.WaMessageId == envio.WaMessageId, ct);
                if (msg is not null)
                {
                    msg.ConversaId = ctx.Conversa.Id;
                    msg.TipoMensagem = TipoMensagem.Robo;
                    msg.AutorNomeExibicao = nomeRobo;
                    msg.AutorUsuarioId = null; // automação — não vira dono nem "atendente"
                }
            }

            ctx.Conversa.UltimaMensagemEm = DateTime.UtcNow;
            ctx.Conversa.UltimaMensagemDirecao = DirecaoMensagem.Saida;
            ctx.Conversa.UltimaMensagemPreview = Abertura.Length <= 160 ? Abertura : Abertura[..160];
            ctx.Conversa.RoboInteracoesNaJanela += 1; // conta no teto por conversa
            // NÃO zera NaoLidas: o robô "ler" não é o operador ler.
        }
        catch (Exception ex)
        {
            Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(
                logger, ex, "Erro ao abrir atendimento fora do horário na conversa {Conversa}.", ctx.Conversa.Id);
        }
    }
}
