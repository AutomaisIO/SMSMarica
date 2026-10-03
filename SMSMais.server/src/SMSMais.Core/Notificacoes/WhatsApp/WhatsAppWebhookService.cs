using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SMSMais.Core.Conversas;
using SMSMais.Core.Conversas.Midias;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Core.Notificacoes.WhatsApp.Manipuladores;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities.Conversas;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Notificacoes.WhatsApp;

public interface IWhatsAppWebhookService
{
    Task ProcessarAsync(string rawJson, CancellationToken ct = default);
}

/// <summary>
/// Processa mensagens recebidas no webhook do WhatsApp. Caminho principal (genérico, chat
/// multi-operador): idempotência por <c>wamid</c> → resolve/abre a <see cref="Conversa"/> do
/// contato (herdando o operador/unidade da última conversa — roteamento sticky) → anexa a
/// mensagem → renova a janela de 24h → incrementa não-lidas. Só então aplica manipuladores de
/// domínio (ex.: confirmação de acompanhante do TFD). Notifica em tempo real APÓS o commit.
/// </summary>
public sealed class WhatsAppWebhookService(
    SmsMaisDbContext db,
    IPacientesService pacientes,
    IConversaNotificador notificador,
    IEnumerable<IManipuladorMensagemWhatsApp> manipuladores,
    IOptions<ComunicacaoPacienteOptions> notificadorOptions,
    IContatoComprometidoService contatosComprometidos,
    ILogger<WhatsAppWebhookService> logger,
    SinalMidiasWhatsApp? sinalMidias = null) : IWhatsAppWebhookService
{
    public async Task ProcessarAsync(string rawJson, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return;

        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawJson); }
        catch { logger.LogWarning("Webhook WhatsApp: corpo JSON inválido."); return; }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return;

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes)) continue;
                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value)) continue;

                    // Recibos de entrega/leitura/falha das mensagens ENVIADAS (não abrem conversa).
                    if (value.TryGetProperty("statuses", out var statuses) && statuses.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var st in statuses.EnumerateArray())
                        {
                            try { await ProcessarStatusAsync(st, ct); }
                            catch (Exception ex) { logger.LogWarning(ex, "Falha ao processar status do WhatsApp."); }
                        }
                    }

                    if (!value.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
                        continue;

                    var contatos = MapearContatos(value);
                    foreach (var m in messages.EnumerateArray())
                    {
                        var from = m.TryGetProperty("from", out var f) ? f.GetString() : null;
                        var nomeContato = from is not null && contatos.TryGetValue(from, out var nome) ? nome : null;
                        await ProcessarMensagemAsync(m, nomeContato, ct);
                    }
                }
            }
        }
    }

    private async Task ProcessarMensagemAsync(JsonElement m, string? nomeContato, CancellationToken ct)
    {
        var waId = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var from = m.TryGetProperty("from", out var fromEl) ? fromEl.GetString() : null;
        if (string.IsNullOrEmpty(waId) || string.IsNullOrEmpty(from)) return;

        if (await db.MensagensWhatsApp.AsNoTracking().AnyAsync(x => x.WaMessageId == waId, ct))
            return; // idempotência

        var texto = ExtrairTexto(m);
        var tipo = ExtrairTipo(m);
        var midia = ExtrairMidia(m);
        var contexto = m.TryGetProperty("context", out var ctxEl) && ctxEl.TryGetProperty("id", out var ctxId)
            ? ctxId.GetString() : null;
        var ocorridoEm = ExtrairTimestamp(m);
        var fone = TelefoneWhatsApp.Canonizar(from);

        var paciente = await pacientes.ObterPorTelefoneAsync(from, ct);
        var pacienteId = paciente?.Id;

        var conversa = await ResolverOuAbrirConversaAsync(fone, pacienteId, nomeContato, contexto, ocorridoEm, ct);

        var msg = new MensagemWhatsApp
        {
            Id = Guid.CreateVersion7(),
            ConversaId = conversa.Id,
            PacienteId = pacienteId ?? conversa.PacienteId,
            Telefone = fone,
            Direcao = DirecaoMensagem.Entrada,
            TipoMensagem = tipo,
            Conteudo = texto,
            Status = StatusMensagemWhatsApp.Recebida,
            WaMessageId = waId,
            ContextoWaMessageId = contexto,
            OcorridoEm = ocorridoEm,
            CriadoEm = DateTime.UtcNow,
        };
        if (midia is not null)
        {
            // Só anota; o download é do worker (o relay espera a resposta por poucos segundos).
            // Foto e PDF vão para a fila; áudio/vídeo ficam só registrados.
            msg.MidiaWaId = midia.Id;
            msg.MidiaMimeType = midia.MimeType;
            msg.MidiaNomeArquivo = Truncar(midia.NomeArquivo, 255);
            msg.MidiaLegenda = Truncar(midia.Legenda, 2000);
            msg.MidiaSha256 = midia.Sha256 is { Length: <= 64 } sha ? sha : null;
            msg.MidiaSituacao = tipo is TipoMensagem.Imagem or TipoMensagem.Documento
                ? SituacaoMidiaWhatsApp.Recebendo
                : null;
        }
        db.MensagensWhatsApp.Add(msg);

        // Efeitos da mensagem inbound na conversa (uniforme p/ conversa nova ou existente).
        void AplicarEntrada(Conversa c)
        {
            c.PacienteId ??= pacienteId;
            c.NomeContato ??= nomeContato;
            c.Status = StatusConversa.Aberta;                    // reabre Pendente/Resolvida/Fechada
            // Robô — âncora da JANELA corrente: abre janela nova = primeiro contato OU a anterior já
            // expirou. Zera o contador de interações do robô. É a primitiva da trava humano-por-janela
            // (o robô se cala se houve resposta/atribuição humana desde JanelaAbertaEm). Precede a
            // renovação de JanelaExpiraEm de propósito — senão a comparação nunca detectaria expiração.
            if (c.JanelaExpiraEm is null || c.JanelaExpiraEm <= ocorridoEm)
            {
                c.JanelaAbertaEm = ocorridoEm;
                c.RoboInteracoesNaJanela = 0;
            }
            c.JanelaExpiraEm = ocorridoEm.AddHours(24);          // renova janela de 24h
            c.UltimaMensagemEm = ocorridoEm;
            c.UltimaMensagemDirecao = DirecaoMensagem.Entrada;
            c.UltimaMensagemPreview = Truncar(texto ?? PreviewMidia(tipo, midia));
            c.NaoLidas += 1;
            c.AtualizadoEm = DateTime.UtcNow;
        }

        // A mensagem do cidadão e a conversa gravam ANTES dos manipuladores. Antes elas ficavam
        // pendentes e iam junto no primeiro SaveChanges de quem respondesse — o do WhatsAppCliente,
        // DEPOIS da ida ao Zap: numa corrida com outro webhook do mesmo número a gravação inteira
        // caía (a resposta já tinha saído), nada ficava registrado e o aviso dizia "o cidadão ficou
        // sem retorno". Medido em 03/10/2026: …0353 (duas conversas novas ao mesmo tempo,
        // IX_conversa_telefone_canonical_canal) e …1270 (xmin da conversa).
        if (await SalvarEntradaAsync(conversa, msg, AplicarEntrada, ct) is not { } gravada)
            return; // o webhook concorrente já gravou esta mesma mensagem
        conversa = gravada;

        // Manipuladores de domínio (não fazem SaveChanges).
        var botaoPayload = m.TryGetProperty("button", out var btnEl) && btnEl.TryGetProperty("payload", out var bp)
            ? bp.GetString() : null;
        var interativoReplyId = m.TryGetProperty("interactive", out var itEl)
            && itEl.TryGetProperty("button_reply", out var brEl) && brEl.TryGetProperty("id", out var bri)
            ? bri.GetString() : null;
        var ctx = new ManipuladorContexto(conversa, msg, texto, conversa.PacienteId, botaoPayload, interativoReplyId);
        foreach (var manipulador in manipuladores.OrderBy(x => x.Ordem))
        {
            try { await manipulador.TratarAsync(ctx, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Manipulador {Tipo} falhou.", manipulador.GetType().Name); }
        }

        await SalvarEfeitosDosManipuladoresAsync(conversa, ct);

        if (msg.MidiaSituacao == SituacaoMidiaWhatsApp.Recebendo) sinalMidias?.Sinalizar();

        // Tempo real APÓS o commit.
        await notificador.MensagemRecebidaAsync(new ConversaEventoRealtime(
            conversa.Id, conversa.OperadorResponsavelId, conversa.UnidadeId,
            conversa.TelefoneCanonical, conversa.NomeContato, conversa.UltimaMensagemPreview,
            conversa.NaoLidas, ocorridoEm), ct);
    }

    private const int MaxTentativasGravacao = 3;
    private const string IndiceConversaVivaDoTelefone = "IX_conversa_telefone_canonical_canal";
    private const string IndiceWamid = "IX_whatsapp_mensagem_wa_message_id";

    /// <summary>
    /// Grava a mensagem do cidadão e os efeitos dela na conversa, resolvendo as duas corridas com
    /// outro webhook do MESMO número (o cidadão manda duas mensagens juntas, ou toca o botão duas
    /// vezes): (1) os dois abriram conversa nova — quem perdeu adota a conversa que o outro gravou;
    /// (2) a conversa mudou depois de lida (<c>xmin</c>) — relê e reaplica por cima do estado atual,
    /// como o robô faz desde 01/10. Devolve a conversa gravada, ou <c>null</c> quando esta mesma
    /// mensagem já tinha sido gravada pelo outro webhook (entrega repetida do relay).
    /// </summary>
    private async Task<Conversa?> SalvarEntradaAsync(
        Conversa conversa, MensagemWhatsApp msg, Action<Conversa> aplicar, CancellationToken ct)
    {
        aplicar(conversa);
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return conversa;
            }
            catch (DbUpdateException ex) when (ViolouIndice(ex, IndiceWamid))
            {
                logger.LogInformation("Webhook WhatsApp: mensagem {Wamid} já gravada por outro webhook — ignorada.",
                    msg.WaMessageId);
                db.ChangeTracker.Clear();
                return null;
            }
            catch (DbUpdateException ex) when (tentativa < MaxTentativasGravacao
                && db.Entry(conversa).State == EntityState.Added && ViolouIndice(ex, IndiceConversaVivaDoTelefone))
            {
                var viva = await db.Conversas.FirstOrDefaultAsync(
                    c => c.TelefoneCanonical == conversa.TelefoneCanonical && c.Canal == conversa.Canal
                      && c.ExcluidoEm == null
                      && (c.Status == StatusConversa.Aberta || c.Status == StatusConversa.Pendente), ct);
                if (viva is null) throw;
                logger.LogInformation(
                    "Webhook WhatsApp (…{Fone4}): outro webhook abriu a conversa primeiro — adotando {Conversa}.",
                    TelefoneWhatsApp.Ultimos4(conversa.TelefoneCanonical), viva.Id);
                db.Entry(conversa).State = EntityState.Detached;
                // A navegação também: com ela apontando a conversa descartada, o SaveChanges a
                // reanexaria como nova e a violação se repetiria.
                msg.Conversa = viva;
                msg.ConversaId = viva.Id;
                msg.PacienteId ??= viva.PacienteId;
                conversa = viva;
                aplicar(conversa);
            }
            catch (DbUpdateConcurrencyException ex) when (tentativa < MaxTentativasGravacao
                && ex.Entries.All(e => e.Entity is Conversa c && c.Id == conversa.Id))
            {
                logger.LogInformation(
                    "Webhook WhatsApp: conversa {Conversa} mudou enquanto a mensagem era gravada — relendo e reaplicando (tentativa {Tentativa}).",
                    conversa.Id, tentativa);
                await db.Entry(conversa).ReloadAsync(ct);
                aplicar(conversa);
            }
        }
    }

    /// <summary>
    /// Grava o que os manipuladores deixaram pendente (estado do diálogo, liberações, marcas na
    /// conversa). Na corrida com outro webhook pela conversa, relê e reaplica por cima o que ELES
    /// mudaram nela. Falha que sobra é erro de verdade e vai como Error (vira aviso no celular),
    /// com o que de fato aconteceu: a mensagem do cidadão já está gravada, a resposta pode ter saído,
    /// o efeito não. Não relança — um 409 faria o relay reentregar à toa (a mensagem já está gravada).
    /// </summary>
    private async Task SalvarEfeitosDosManipuladoresAsync(Conversa conversa, CancellationToken ct)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex) when (tentativa < MaxTentativasGravacao
                && ex.Entries.All(e => e.Entity is Conversa c && c.Id == conversa.Id))
            {
                logger.LogInformation(
                    "Webhook WhatsApp: conversa {Conversa} mudou enquanto os manipuladores respondiam — relendo e reaplicando (tentativa {Tentativa}).",
                    conversa.Id, tentativa);
                var entrada = db.Entry(conversa);
                var mudadas = entrada.Properties
                    .Where(p => p.IsModified)
                    .Select(p => (p.Metadata.Name, p.CurrentValue))
                    .ToList();
                await entrada.ReloadAsync(ct);
                foreach (var (nome, valor) in mudadas)
                    entrada.Property(nome).CurrentValue = valor;
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex,
                    "Webhook WhatsApp (…{Fone4}): a mensagem do cidadão está gravada, mas o que os manipuladores "
                    + "fizeram com ela NÃO gravou ({Erro}) — a resposta pode ter saído e o diálogo ficado uma etapa atrás.",
                    TelefoneWhatsApp.Ultimos4(conversa.TelefoneCanonical), ex.InnerException?.Message ?? ex.Message);

                // Ao menos o registro das respostas que SAÍRAM: sem ele a thread omite o que o
                // cidadão recebeu. A conversa já está gravada (SalvarEntradaAsync) — fica como está.
                foreach (var e in db.ChangeTracker.Entries().ToList())
                {
                    if (e.Entity is MensagemWhatsApp && e.State == EntityState.Added) continue;
                    e.State = e.Entity is Conversa ? EntityState.Unchanged : EntityState.Detached;
                }
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException ex2)
                {
                    logger.LogWarning(ex2, "Webhook WhatsApp: nem o registro das respostas enviadas gravou.");
                }
                db.ChangeTracker.Clear();
                return;
            }
        }
    }

    private static bool ViolouIndice(DbUpdateException ex, string indice) =>
        ex.InnerException is Npgsql.PostgresException { SqlState: "23505" } pg && pg.ConstraintName == indice;

    /// <summary>
    /// Aplica um recibo (sent/delivered/read/failed) à mensagem outbound (por wamid) e espelha
    /// na ComunicacaoPaciente vinculada. Promoção monotônica: Enviada→Entregue→Lida nunca
    /// regride; failed sempre vira Falha + ErroMeta. Idempotente (repetir o mesmo recibo não muda nada).
    /// </summary>
    private async Task ProcessarStatusAsync(JsonElement st, CancellationToken ct)
    {
        var wamid = st.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var status = st.TryGetProperty("status", out var stEl) ? stEl.GetString() : null;
        if (string.IsNullOrEmpty(wamid) || string.IsNullOrEmpty(status)) return;

        var novo = status switch
        {
            "sent" => StatusMensagemWhatsApp.Enviada,
            "delivered" => StatusMensagemWhatsApp.Entregue,
            "read" => StatusMensagemWhatsApp.Lida,
            "failed" => StatusMensagemWhatsApp.Falha,
            _ => (StatusMensagemWhatsApp?)null,
        };
        if (novo is null) return;

        var msg = await db.MensagensWhatsApp.FirstOrDefaultAsync(
            m => m.WaMessageId == wamid && m.Direcao == DirecaoMensagem.Saida, ct);
        if (msg is null) return;

        var ocorridoEm = ExtrairTimestamp(st);
        string? erro = null;
        if (novo == StatusMensagemWhatsApp.Falha)
        {
            erro = ExtrairErroStatus(st);
            msg.Status = StatusMensagemWhatsApp.Falha;
            msg.ErroMeta = erro is { Length: > 500 } ? erro[..500] : erro;
        }
        else if (msg.Status is StatusMensagemWhatsApp.Enviada or StatusMensagemWhatsApp.Entregue
                 && novo > msg.Status)
        {
            msg.Status = novo.Value; // promoção monotônica; Lida não regride para Entregue
        }

        // Espelho na notificação de agendamento (tela de gestão + retentativa em falha de entrega).
        var notificacao = await db.ComunicacoesPaciente.FirstOrDefaultAsync(
            n => n.MensagemWhatsAppId == msg.Id, ct);
        if (notificacao is not null)
        {
            notificacao.AtualizadoEm = DateTime.UtcNow;
            switch (novo)
            {
                case StatusMensagemWhatsApp.Entregue:
                    notificacao.EntregueEm ??= ocorridoEm;
                    if (notificacao.Status == StatusComunicacao.Enviada)
                        notificacao.Status = StatusComunicacao.Entregue;
                    break;
                case StatusMensagemWhatsApp.Lida:
                    notificacao.EntregueEm ??= ocorridoEm;
                    notificacao.LidoEm ??= ocorridoEm;
                    if (notificacao.Status is StatusComunicacao.Enviada or StatusComunicacao.Entregue)
                        notificacao.Status = StatusComunicacao.Lida;
                    break;
                case StatusMensagemWhatsApp.Falha:
                    notificacao.MotivoFalha = erro ?? "Falha de entrega reportada pelo WhatsApp.";
                    if (notificacao.Tentativas < notificadorOptions.Value.MaxTentativas && ErroEntregaRetentavel(erro))
                    {
                        // Volta pra fila — o worker reenvia com magic link novo.
                        notificacao.Status = StatusComunicacao.Pendente;
                        notificacao.ProximaTentativaEm = DateTime.UtcNow.AddMinutes(
                            5 * Math.Pow(2, Math.Max(0, notificacao.Tentativas - 1)));
                    }
                    else
                    {
                        notificacao.Status = StatusComunicacao.Falha;
                        notificacao.ProximaTentativaEm = null;

                        // Recusa permanente (não é WhatsApp): marca o CADASTRO. A maioria das
                        // falhas 131026 chega por aqui, no recibo — não na resposta do envio.
                        if (!ErroEntregaRetentavel(erro))
                            await contatosComprometidos.MarcarAsync(
                                notificacao.PacienteId, notificacao.Telefone,
                                MotivoContatoComprometido.NaoEhWhatsApp, erro, notificacao.Id, ct);
                    }
                    break;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>"(code) title — details" dos errors[] do recibo failed.</summary>
    private static string? ExtrairErroStatus(JsonElement st)
    {
        if (!st.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array
            || errors.GetArrayLength() == 0)
            return null;
        var e = errors[0];
        var code = e.TryGetProperty("code", out var c) ? c.ToString() : null;
        var title = e.TryGetProperty("title", out var t) ? t.GetString() : null;
        var details = e.TryGetProperty("error_data", out var ed) && ed.TryGetProperty("details", out var d)
            ? d.GetString() : null;
        var txt = $"({code}) {title}";
        return string.IsNullOrEmpty(details) ? txt : $"{txt} — {details}";
    }

    /// <summary>Falhas permanentes (número não é WhatsApp / destinatário inválido) não retentam.</summary>
    private static bool ErroEntregaRetentavel(string? erro) =>
        erro is null || !(erro.Contains("(131026)") || erro.Contains("(131030)"));

    private async Task<Conversa> ResolverOuAbrirConversaAsync(
        string fone, Guid? pacienteId, string? nomeContato, string? contexto, DateTime ocorridoEm, CancellationToken ct)
    {
        Conversa? conversa = null;

        // 1. Âncora forte: a mensagem respondida (context.id) aponta a conversa.
        if (!string.IsNullOrEmpty(contexto))
        {
            var conversaIdCtx = await db.MensagensWhatsApp.AsNoTracking()
                .Where(x => x.WaMessageId == contexto && x.ConversaId != null)
                .Select(x => x.ConversaId)
                .FirstOrDefaultAsync(ct);
            if (conversaIdCtx is Guid cid)
                conversa = await db.Conversas.FirstOrDefaultAsync(c => c.Id == cid && c.ExcluidoEm == null, ct);
        }

        // 2. Conversa viva (Aberta/Pendente) do telefone.
        conversa ??= await db.Conversas.FirstOrDefaultAsync(
            c => c.TelefoneCanonical == fone && c.Canal == CanalConversa.WhatsApp && c.ExcluidoEm == null
              && (c.Status == StatusConversa.Aberta || c.Status == StatusConversa.Pendente), ct);
        if (conversa is not null) return conversa;

        // 3. Cria nova, herdando operador/unidade da última conversa do telefone (sticky).
        var ultima = await db.Conversas.AsNoTracking()
            .Where(c => c.TelefoneCanonical == fone && c.Canal == CanalConversa.WhatsApp && c.ExcluidoEm == null)
            .OrderByDescending(c => c.UltimaMensagemEm ?? c.CriadoEm)
            .Select(c => new { c.OperadorResponsavelId, c.UnidadeId, c.PacienteId, c.NomeContato })
            .FirstOrDefaultAsync(ct);

        var nova = new Conversa
        {
            Id = Guid.CreateVersion7(),
            Canal = CanalConversa.WhatsApp,
            TelefoneCanonical = fone,
            PacienteId = pacienteId ?? ultima?.PacienteId,
            NomeContato = nomeContato ?? ultima?.NomeContato,
            Status = StatusConversa.Aberta,
            OperadorResponsavelId = ultima?.OperadorResponsavelId,
            UnidadeId = ultima?.UnidadeId,
            JanelaExpiraEm = ocorridoEm.AddHours(24),
            PrimeiroContatoEm = ocorridoEm,
            NaoLidas = 0,
            CriadoEm = DateTime.UtcNow,
        };
        db.Conversas.Add(nova);
        db.ConversaEventos.Add(new ConversaEvento
        {
            Id = Guid.CreateVersion7(),
            ConversaId = nova.Id,
            Tipo = TipoEventoConversa.Criada,
            OcorridoEm = ocorridoEm,
            CriadoEm = DateTime.UtcNow,
        });
        return nova;
    }

    private static Dictionary<string, string?> MapearContatos(JsonElement value)
    {
        var mapa = new Dictionary<string, string?>();
        if (!value.TryGetProperty("contacts", out var contatos) || contatos.ValueKind != JsonValueKind.Array)
            return mapa;
        foreach (var c in contatos.EnumerateArray())
        {
            var waId = c.TryGetProperty("wa_id", out var w) ? w.GetString() : null;
            if (string.IsNullOrEmpty(waId)) continue;
            var nome = c.TryGetProperty("profile", out var p) && p.TryGetProperty("name", out var nm) ? nm.GetString() : null;
            mapa[waId] = nome;
        }
        return mapa;
    }

    private static DateTime ExtrairTimestamp(JsonElement m)
    {
        if (m.TryGetProperty("timestamp", out var ts) && ts.GetString() is { } s
            && long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch))
        {
            return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        }
        return DateTime.UtcNow;
    }

    private static TipoMensagem ExtrairTipo(JsonElement m)
    {
        var tipo = m.TryGetProperty("type", out var t) ? t.GetString() : null;
        return tipo switch
        {
            "image" => TipoMensagem.Imagem,
            "document" => TipoMensagem.Documento,
            "audio" or "voice" => TipoMensagem.Audio,
            "video" => TipoMensagem.Video,
            _ => TipoMensagem.Texto,
        };
    }

    private static string? ExtrairTexto(JsonElement m)
    {
        if (m.TryGetProperty("text", out var t) && t.TryGetProperty("body", out var b)) return b.GetString();
        if (m.TryGetProperty("button", out var btn) && btn.TryGetProperty("text", out var bt)) return bt.GetString();
        if (m.TryGetProperty("interactive", out var it))
        {
            if (it.TryGetProperty("button_reply", out var br) && br.TryGetProperty("title", out var brt)) return brt.GetString();
            if (it.TryGetProperty("list_reply", out var lr) && lr.TryGetProperty("title", out var lrt)) return lrt.GetString();
        }
        return null;
    }

    private static string? Truncar(string? s) => s is null ? null : s.Length <= 200 ? s : s[..200];

    private static string? Truncar(string? s, int maximo) => s is null ? null : s.Length <= maximo ? s : s[..maximo];

    private sealed record MidiaRecebidaInfo(string Id, string? MimeType, string? NomeArquivo, string? Legenda, string? Sha256);

    /// <summary>
    /// Objeto de mídia da Cloud API (<c>image</c>, <c>document</c>, <c>audio</c>, <c>video</c>,
    /// <c>sticker</c>): id, mime, nome do arquivo (só documento) e legenda.
    /// </summary>
    private static MidiaRecebidaInfo? ExtrairMidia(JsonElement m)
    {
        var tipo = m.TryGetProperty("type", out var t) ? t.GetString() : null;
        if (tipo is not ("image" or "document" or "audio" or "voice" or "video" or "sticker")) return null;
        if (!m.TryGetProperty(tipo, out var obj) || obj.ValueKind != JsonValueKind.Object) return null;

        string? Ler(string prop) =>
            obj.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        var id = Ler("id");
        return string.IsNullOrWhiteSpace(id)
            ? null
            : new MidiaRecebidaInfo(id, Ler("mime_type"), Ler("filename"), Ler("caption"), Ler("sha256"));
    }

    private static string? PreviewMidia(TipoMensagem tipo, MidiaRecebidaInfo? midia)
    {
        var rotulo = tipo switch
        {
            TipoMensagem.Imagem => "📷 Imagem",
            TipoMensagem.Documento => "📎 " + (midia?.NomeArquivo ?? "Documento"),
            TipoMensagem.Audio => "🎤 Áudio",
            TipoMensagem.Video => "🎬 Vídeo",
            _ => null,
        };
        return midia?.Legenda is { Length: > 0 } legenda && rotulo is not null ? $"{rotulo} — {legenda}" : rotulo;
    }
}
