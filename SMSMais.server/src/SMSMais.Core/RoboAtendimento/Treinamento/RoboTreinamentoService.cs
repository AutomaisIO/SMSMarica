using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.RoboAtendimento.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento.Treinamento;

public interface IRoboTreinamentoService
{
    Task<Guid> AbrirAsync(AbrirTreinamentoRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<TreinamentoItemResumoDto>> ListarAsync(string? status, Guid? assuntoId, CancellationToken ct = default);
    Task<TreinamentoItemDto> ObterAsync(Guid id, CancellationToken ct = default);
    Task TreinarAsync(Guid id, TreinarRequest request, CancellationToken ct = default);
    Task ResponderPendenciaAsync(Guid pendenciaId, ResponderPendenciaRequest request, CancellationToken ct = default);
    Task DispensarPendenciaAsync(Guid pendenciaId, string? motivo, CancellationToken ct = default);
    Task DesfazerAlteracaoAsync(Guid alteracaoId, CancellationToken ct = default);
    Task<TreinamentoSimulacaoDto> SimularAsync(Guid id, SimularTreinamentoRequest request, CancellationToken ct = default);
    Task DescartarAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// A fachada do treinamento: abre o item a partir da crítica na bolha, enfileira a análise, deixa o
/// humano responder pendência e desfazer alteração, e roda a simulação sob demanda.
///
/// O que ele deliberadamente NÃO faz é chamar o agente: análise de item roda no
/// <see cref="RoboTreinamentoWorker"/>. Um POST que segura a requisição por minutos enquanto o
/// modelo raciocina é uma tela travada e um timeout de proxy — aqui o clique só muda o status para
/// <see cref="StatusTreinamentoRobo.Analisando"/> e a tela acompanha.
/// </summary>
public sealed class RoboTreinamentoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    RoboTreinamentoAplicador aplicador,
    IRoboTreinamentoSimulador simulador) : IRoboTreinamentoService
{
    private const int Limite = 300;

    /// <summary>Turnos congelados em volta da mensagem criticada. O mesmo tamanho que o robô lê no
    /// atendimento — analisar com MAIS contexto do que ele teve levaria a regra errada.</summary>
    private const int TurnosDeContexto = 12;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Guid> AbrirAsync(AbrirTreinamentoRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Critica))
            throw new ValidacaoException("critica", "Escreva a correção ou a crítica à resposta do robô.");

        var me = usuarioAtual.UsuarioId;
        var agora = DateTime.UtcNow;
        var critica = request.Critica.Trim();

        Guid? assuntoId = request.RoboAssuntoId;
        string? trecho = null;
        string? contextoJson = null;
        Guid? erroId = null;

        if (request.ConversaId is { } conversaId)
        {
            var conversa = await db.Conversas.AsNoTracking()
                .Where(c => c.Id == conversaId)
                .Select(c => new { c.Id, c.RoboAssuntoId })
                .FirstOrDefaultAsync(ct)
                ?? throw new NaoEncontradoException("Conversa", conversaId);
            assuntoId ??= conversa.RoboAssuntoId;
        }

        if (request.MensagemWhatsAppId is { } msgId)
        {
            var msg = await db.MensagensWhatsApp.AsNoTracking()
                .Where(m => m.Id == msgId)
                .Select(m => new { m.Conteudo, m.TipoMensagem, m.ConversaId, m.OcorridoEm })
                .FirstOrDefaultAsync(ct)
                ?? throw new NaoEncontradoException("Mensagem", msgId);

            if (msg.TipoMensagem != TipoMensagem.Robo)
                throw new ValidacaoException("mensagemWhatsAppId",
                    "Só dá para treinar a partir de uma mensagem do robô.");

            // Um item por bolha: clicar de novo acrescenta a crítica ao item existente em vez de
            // abrir um processo paralelo sobre a mesma resposta.
            var existente = await db.RoboTreinamentoItens
                .FirstOrDefaultAsync(i => i.MensagemWhatsAppId == msgId, ct);
            if (existente is not null)
            {
                existente.Critica = existente.Critica.TrimEnd() + "\n\n---\n\n" + critica;
                existente.AtualizadoEm = agora;
                existente.AtualizadoPor = me;
                if (existente.Status is StatusTreinamentoRobo.Concluido or StatusTreinamentoRobo.Descartado)
                    existente.Status = StatusTreinamentoRobo.Aberto;
                await db.SaveChangesAsync(ct);
                return existente.Id;
            }

            trecho = msg.Conteudo;
            if (msg.ConversaId is { } cid)
                contextoJson = await CongelarContextoAsync(cid, msgId, msg.OcorridoEm, ct);

            // Reaproveita a captura do fluxo antigo (a marcação de erro), quando houver — assim o
            // relatório de erros por assunto e o treinamento falam do mesmo evento.
            erroId = await db.RoboErrosResposta
                .Where(e => e.MensagemWhatsAppId == msgId)
                .OrderByDescending(e => e.CriadoEm)
                .Select(e => (Guid?)e.Id)
                .FirstOrDefaultAsync(ct);
        }

        var item = new RoboTreinamentoItem
        {
            Id = Guid.CreateVersion7(),
            RoboErroRespostaId = erroId,
            ConversaId = request.ConversaId,
            MensagemWhatsAppId = request.MensagemWhatsAppId,
            RoboAssuntoId = assuntoId,
            Trecho = trecho,
            ContextoJson = contextoJson,
            Critica = critica,
            Status = StatusTreinamentoRobo.Aberto,
            CriadoEm = agora,
            CriadoPor = me,
        };
        db.RoboTreinamentoItens.Add(item);
        await db.SaveChangesAsync(ct);
        return item.Id;
    }

    public async Task<IReadOnlyList<TreinamentoItemResumoDto>> ListarAsync(
        string? status, Guid? assuntoId, CancellationToken ct = default)
    {
        var q = db.RoboTreinamentoItens.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<StatusTreinamentoRobo>(status, true, out var s))
                throw new ValidacaoException("status", $"Status inválido: {status}.");
            q = q.Where(i => i.Status == s);
        }
        if (assuntoId is { } a) q = q.Where(i => i.RoboAssuntoId == a);

        return await q
            .OrderByDescending(i => i.CriadoEm)
            .Take(Limite)
            .Select(i => new TreinamentoItemResumoDto(
                i.Id,
                i.ConversaId,
                i.MensagemWhatsAppId,
                i.RoboAssunto != null ? i.RoboAssunto.Nome : null,
                i.Critica,
                i.Trecho,
                i.Status.ToString(),
                i.Pendencias.Count(p => p.Status == StatusPendenciaTreinamento.Aberta),
                i.Alteracoes.Count(x => x.DesfeitoEm == null),
                i.Simulacoes.OrderByDescending(s => s.CriadoEm)
                    .Select(s => s.Veredito != null ? s.Veredito.ToString() : null).FirstOrDefault(),
                i.CriadoEm,
                db.Usuarios.Where(u => u.Id == i.CriadoPor).Select(u => u.NomeCompleto).FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<TreinamentoItemDto> ObterAsync(Guid id, CancellationToken ct = default)
    {
        var item = await db.RoboTreinamentoItens.AsNoTracking()
            .Include(i => i.RoboAssunto)
            .Include(i => i.Pendencias)
            .Include(i => i.Alteracoes).ThenInclude(a => a.RoboAssunto)
            .Include(i => i.Simulacoes)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NaoEncontradoException("Item de treinamento", id);

        var pessoas = await NomesAsync(item, ct);

        return new TreinamentoItemDto(
            item.Id,
            item.ConversaId,
            item.MensagemWhatsAppId,
            item.RoboAssuntoId,
            item.RoboAssunto?.Nome,
            item.Critica,
            item.Observacao,
            item.Trecho,
            LerContexto(item.ContextoJson),
            item.Status.ToString(),
            item.Analise,
            item.Modelo,
            item.CustoUsd == 0m ? null : item.CustoUsd,
            item.AnalisadoEm,
            item.ErroMensagem,
            [.. item.Pendencias.OrderBy(p => p.CriadoEm).Select(p => new TreinamentoPendenciaDto(
                p.Id, p.Tipo.ToString(), p.Pergunta, p.Contexto, LerOpcoes(p.OpcoesJson),
                p.Status.ToString(), p.Resposta, p.Autorizado, p.CriadoEm, p.RespondidoEm,
                p.RespondidoPor is { } r && pessoas.TryGetValue(r, out var nr) ? nr : null))],
            [.. item.Alteracoes.OrderBy(a => a.AplicadoEm).Select(a => new TreinamentoAlteracaoDto(
                a.Id, a.Alvo.ToString(), a.Operacao.ToString(), a.RoboAssuntoId, a.RoboAssunto?.Nome,
                a.AlvoId, Resumir(a.ValorAnteriorJson), Resumir(a.ValorNovoJson), a.Justificativa,
                a.AplicadoEm, a.DesfeitoEm,
                a.DesfeitoPor is { } d && pessoas.TryGetValue(d, out var nd) ? nd : null))],
            [.. item.Simulacoes.OrderByDescending(s => s.CriadoEm).Select(s => MapearSimulacao(s,
                s.CriadoPor is { } c && pessoas.TryGetValue(c, out var nc) ? nc : null))],
            item.CriadoEm,
            item.CriadoPor is { } cp && pessoas.TryGetValue(cp, out var ncp) ? ncp : null);
    }

    public async Task TreinarAsync(Guid id, TreinarRequest request, CancellationToken ct = default)
    {
        var item = await db.RoboTreinamentoItens
            .Include(i => i.Pendencias)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NaoEncontradoException("Item de treinamento", id);

        if (item.Status == StatusTreinamentoRobo.Analisando)
            throw new ConflitoException("robo_treinamento.em_analise", "Este item já está sendo analisado.");

        var abertas = item.Pendencias.Count(p => p.Status == StatusPendenciaTreinamento.Aberta);
        if (abertas > 0)
            throw new ConflitoException("robo_treinamento.pendencia_aberta",
                $"Responda as {abertas} pendência(s) deste item antes de treinar de novo.");

        if (!string.IsNullOrWhiteSpace(request.Observacao))
        {
            // Acumula: mandar treinar de novo depois de responder uma pendência não pode apagar o
            // que foi observado na primeira vez.
            item.Observacao = string.IsNullOrWhiteSpace(item.Observacao)
                ? request.Observacao.Trim()
                : item.Observacao.TrimEnd() + "\n\n" + request.Observacao.Trim();
        }

        item.Status = StatusTreinamentoRobo.Analisando;
        item.ErroMensagem = null;
        item.TentativasAnalise = 0;
        item.AtualizadoEm = DateTime.UtcNow;
        item.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    public async Task ResponderPendenciaAsync(
        Guid pendenciaId, ResponderPendenciaRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Resposta))
            throw new ValidacaoException("resposta", "Escreva a resposta à pendência.");

        var p = await db.RoboTreinamentoPendencias
            .Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == pendenciaId, ct)
            ?? throw new NaoEncontradoException("Pendência", pendenciaId);

        if (p.Status != StatusPendenciaTreinamento.Aberta)
            throw new ConflitoException("robo_treinamento.pendencia_respondida", "Esta pendência já foi respondida.");

        if (p.Tipo == TipoPendenciaTreinamento.AlteracaoCodigo && request.Autorizado is null)
            throw new ValidacaoException("autorizado",
                "Pendência de alteração de código precisa de uma decisão: autorizar ou não.");

        p.Status = StatusPendenciaTreinamento.Respondida;
        p.Resposta = request.Resposta.Trim();
        p.Autorizado = p.Tipo == TipoPendenciaTreinamento.AlteracaoCodigo ? request.Autorizado : null;
        p.RespondidoEm = DateTime.UtcNow;
        p.RespondidoPor = usuarioAtual.UsuarioId;

        await DestravarSeUltimaAsync(p, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task DispensarPendenciaAsync(Guid pendenciaId, string? motivo, CancellationToken ct = default)
    {
        var p = await db.RoboTreinamentoPendencias
            .Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == pendenciaId, ct)
            ?? throw new NaoEncontradoException("Pendência", pendenciaId);

        if (p.Status != StatusPendenciaTreinamento.Aberta)
            throw new ConflitoException("robo_treinamento.pendencia_respondida", "Esta pendência já foi respondida.");

        p.Status = StatusPendenciaTreinamento.Dispensada;
        p.Resposta = string.IsNullOrWhiteSpace(motivo) ? "Dispensada sem justificativa." : motivo.Trim();
        p.RespondidoEm = DateTime.UtcNow;
        p.RespondidoPor = usuarioAtual.UsuarioId;

        await DestravarSeUltimaAsync(p, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task DesfazerAlteracaoAsync(Guid alteracaoId, CancellationToken ct = default)
    {
        var alt = await db.RoboTreinamentoAlteracoes
            .Include(a => a.Item)
            .FirstOrDefaultAsync(a => a.Id == alteracaoId, ct)
            ?? throw new NaoEncontradoException("Alteração", alteracaoId);

        await aplicador.DesfazerAsync(alt, usuarioAtual.UsuarioId, ct);

        // Desfazer muda o robô: o veredito da última simulação passa a valer para um modelo que
        // não existe mais.
        if (alt.Item is { } item && item.Status == StatusTreinamentoRobo.Concluido)
        {
            item.Status = StatusTreinamentoRobo.SimulacaoPendente;
            item.AtualizadoEm = DateTime.UtcNow;
            item.AtualizadoPor = usuarioAtual.UsuarioId;
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<TreinamentoSimulacaoDto> SimularAsync(
        Guid id, SimularTreinamentoRequest request, CancellationToken ct = default)
    {
        var item = await db.RoboTreinamentoItens.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NaoEncontradoException("Item de treinamento", id);

        var (mensagem, historico) = ResolverCaso(item, request);
        if (string.IsNullOrWhiteSpace(mensagem))
            throw new ValidacaoException("mensagem",
                "Escreva a mensagem do cidadão: este item não guardou uma fala para repetir.");

        var sim = await simulador.SimularAsync(
            item, mensagem, historico, automatica: false, usuarioAtual.UsuarioId, ct);

        if (item.Status == StatusTreinamentoRobo.SimulacaoPendente && sim.ErroMensagem is null)
        {
            item.Status = StatusTreinamentoRobo.Concluido;
            item.AtualizadoEm = DateTime.UtcNow;
            item.AtualizadoPor = usuarioAtual.UsuarioId;
        }
        await db.SaveChangesAsync(ct);

        var nome = usuarioAtual.UsuarioId is { } me
            ? await db.Usuarios.Where(u => u.Id == me).Select(u => u.NomeCompleto).FirstOrDefaultAsync(ct)
            : null;
        return MapearSimulacao(sim, nome);
    }

    public async Task DescartarAsync(Guid id, CancellationToken ct = default)
    {
        var item = await db.RoboTreinamentoItens
            .Include(i => i.Pendencias)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new NaoEncontradoException("Item de treinamento", id);

        item.Status = StatusTreinamentoRobo.Descartado;
        foreach (var p in item.Pendencias.Where(p => p.Status == StatusPendenciaTreinamento.Aberta))
        {
            p.Status = StatusPendenciaTreinamento.Dispensada;
            p.Resposta = "Item descartado.";
            p.RespondidoEm = DateTime.UtcNow;
            p.RespondidoPor = usuarioAtual.UsuarioId;
        }
        item.AtualizadoEm = DateTime.UtcNow;
        item.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    // ---------- apoio ----------

    /// <summary>Respondida a última pendência, o item volta para a fila da análise por conta
    /// própria — o humano não deveria ter que lembrar de clicar em "treinar" de novo.</summary>
    private async Task DestravarSeUltimaAsync(RoboTreinamentoPendencia p, CancellationToken ct)
    {
        var restam = await db.RoboTreinamentoPendencias
            .CountAsync(x => x.RoboTreinamentoItemId == p.RoboTreinamentoItemId
                && x.Id != p.Id && x.Status == StatusPendenciaTreinamento.Aberta, ct);
        if (restam > 0) return;

        var item = p.Item ?? await db.RoboTreinamentoItens
            .FirstOrDefaultAsync(i => i.Id == p.RoboTreinamentoItemId, ct);
        if (item is null || item.Status != StatusTreinamentoRobo.AguardandoHumano) return;

        item.Status = StatusTreinamentoRobo.Analisando;
        item.TentativasAnalise = 0;
        item.AtualizadoEm = DateTime.UtcNow;
        item.AtualizadoPor = usuarioAtual.UsuarioId;
    }

    private (string? Mensagem, List<(string Papel, string Texto)> Historico) ResolverCaso(
        RoboTreinamentoItem item, SimularTreinamentoRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Mensagem))
        {
            var hist = request.Historico is null
                ? []
                : request.Historico.Select(h => (h.Papel, h.Texto)).ToList();
            return (request.Mensagem.Trim(), hist);
        }

        var contexto = LerContexto(item.ContextoJson);
        var idx = contexto.ToList().FindLastIndex(t =>
            t.Papel.Equals("cidadao", StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return (null, []);

        return (contexto[idx].Texto,
            [.. contexto.Take(idx).Select(t => (t.Papel, t.Texto))]);
    }

    /// <summary>
    /// Congela o diálogo em volta da mensagem criticada. Congelar (e não ler ao vivo depois) é
    /// deliberado: a conversa segue andando, e analisar a crítica com mensagens que só chegaram
    /// DEPOIS levaria o agente a corrigir um erro que o robô não tinha como não cometer.
    /// </summary>
    private async Task<string?> CongelarContextoAsync(
        Guid conversaId, Guid mensagemId, DateTime ocorridoEm, CancellationToken ct)
    {
        var anteriores = await db.MensagensWhatsApp.AsNoTracking()
            .Where(m => m.ConversaId == conversaId
                && m.TipoMensagem != TipoMensagem.NotaInterna
                && m.Conteudo != null
                && m.Id != mensagemId
                && m.OcorridoEm <= ocorridoEm)
            .OrderByDescending(m => m.OcorridoEm)
            .Take(TurnosDeContexto)
            .Select(m => new { m.Direcao, m.TipoMensagem, m.AutorUsuarioId, m.Conteudo, m.OcorridoEm })
            .ToListAsync(ct);
        if (anteriores.Count == 0) return null;

        anteriores.Reverse();
        return JsonSerializer.Serialize(anteriores.Select(m => new
        {
            papel = Papel(m.Direcao, m.TipoMensagem, m.AutorUsuarioId),
            texto = m.Conteudo!,
            em = m.OcorridoEm,
        }), Json);
    }

    /// <summary>Mesma regra do atendimento (<c>RoboAtendimentoProcessador</c>): o agente precisa
    /// enxergar a conversa com os mesmos papéis que o robô enxergou.</summary>
    private static string Papel(DirecaoMensagem direcao, TipoMensagem? tipo, Guid? autor)
    {
        if (direcao == DirecaoMensagem.Entrada) return "cidadao";
        if (tipo == TipoMensagem.Robo) return "robo";
        if (autor != null) return "atendente";
        return "sistema";
    }

    private async Task<Dictionary<Guid, string>> NomesAsync(RoboTreinamentoItem item, CancellationToken ct)
    {
        var ids = new List<Guid?> { item.CriadoPor };
        ids.AddRange(item.Pendencias.Select(p => p.RespondidoPor));
        ids.AddRange(item.Alteracoes.Select(a => a.DesfeitoPor));
        ids.AddRange(item.Simulacoes.Select(s => s.CriadoPor));

        var alvo = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (alvo.Count == 0) return [];

        return await db.Usuarios.AsNoTracking()
            .Where(u => alvo.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NomeCompleto, ct);
    }

    private static TreinamentoSimulacaoDto MapearSimulacao(RoboTreinamentoSimulacao s, string? quem) =>
        new(s.Id, s.Mensagem, s.AssuntoNome, s.Resposta,
            LerChamadas(s.ChamadasJson),
            s.Veredito?.ToString(), s.Analise,
            s.CustoUsd == 0m ? null : s.CustoUsd,
            s.DuracaoMs, s.Automatica, s.ErroMensagem, s.CriadoEm, quem);

    private static IReadOnlyList<RoboSimulacaoChamadaDto> LerChamadas(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<RoboSimulacaoChamadaDto>>(json, Json) ?? [];
        }
        catch (JsonException) { return []; }
    }

    private static IReadOnlyList<TreinamentoContextoTurnoDto> LerContexto(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<TreinamentoContextoTurnoDto>>(json, Json) ?? [];
        }
        catch (JsonException) { return []; }
    }

    private static IReadOnlyList<string> LerOpcoes(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, Json) ?? [];
        }
        catch (JsonException) { return []; }
    }

    /// <summary>Diff legível na tela: o JSON cru da linha não diz nada a quem opera.</summary>
    private static string? Resumir(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var tipo = r.TryGetProperty("tipo", out var t) ? t.GetString() : null;
            var titulo = r.TryGetProperty("titulo", out var ti) && ti.ValueKind == JsonValueKind.String
                ? ti.GetString() : null;
            var corpo = r.TryGetProperty("conteudo", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString()
                : r.TryGetProperty("valor", out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString() : null;
            var ativo = r.TryGetProperty("ativo", out var a) && a.ValueKind == JsonValueKind.False
                ? " (inativo)" : string.Empty;

            var prefixo = string.IsNullOrWhiteSpace(titulo) ? tipo : $"{tipo} · {titulo}";
            return string.IsNullOrWhiteSpace(corpo) ? $"{prefixo}{ativo}" : $"{prefixo}: {corpo}{ativo}";
        }
        catch (JsonException) { return json; }
    }
}
