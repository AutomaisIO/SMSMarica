using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento.Treinamento;

/// <summary>
/// Aplica no material do robô o que o agente decidiu — e sabe desfazer. É o <b>único</b> ponto por
/// onde a análise toca o banco, e ele só conhece dois alvos: treino e condição do assunto
/// (ADR do treinamento). Persona, horário, comando e guardrail não passam por aqui: viram
/// pendência.
///
/// Nada é apagado de verdade. Remover é desativar (<c>Ativo = false</c>), porque a contrapartida
/// de deixar o agente aplicar sozinho é o desfazer funcionar sempre.
/// </summary>
public sealed class RoboTreinamentoAplicador(SmsMaisDbContext db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---------- treinos (regras locais) ----------

    public async Task<RoboTreinamentoAlteracao> CriarTreinoAsync(
        Guid itemId, Guid assuntoId, TipoTreinoRobo tipo, string? titulo, string conteudo,
        string justificativa, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(conteudo))
            throw new ValidacaoException("conteudo", "O treino precisa de conteúdo.");

        await GarantirAssuntoAsync(assuntoId, ct);

        var ordem = await db.RoboAssuntoTreinos
            .Where(t => t.RoboAssuntoId == assuntoId)
            .Select(t => (int?)t.Ordem)
            .MaxAsync(ct) ?? 0;

        var treino = new RoboAssuntoTreino
        {
            Id = Guid.CreateVersion7(),
            RoboAssuntoId = assuntoId,
            Tipo = tipo,
            Titulo = string.IsNullOrWhiteSpace(titulo) ? null : titulo.Trim(),
            Conteudo = conteudo.Trim(),
            Ordem = ordem + 10,
            Ativo = true,
        };
        db.RoboAssuntoTreinos.Add(treino);

        return Registrar(itemId, AlvoAlteracaoTreinamento.TreinoAssunto,
            OperacaoAlteracaoTreinamento.Criar, assuntoId, treino.Id,
            anterior: null, novo: Serializar(treino), justificativa);
    }

    public async Task<RoboTreinamentoAlteracao> AtualizarTreinoAsync(
        Guid itemId, Guid treinoId, TipoTreinoRobo? tipo, string? titulo, string? conteudo,
        string justificativa, CancellationToken ct)
    {
        var treino = await db.RoboAssuntoTreinos.FirstOrDefaultAsync(t => t.Id == treinoId, ct)
            ?? throw new NaoEncontradoException("Treino", treinoId);

        var antes = Serializar(treino);
        if (tipo is { } t) treino.Tipo = t;
        if (titulo is not null) treino.Titulo = string.IsNullOrWhiteSpace(titulo) ? null : titulo.Trim();
        if (!string.IsNullOrWhiteSpace(conteudo)) treino.Conteudo = conteudo.Trim();
        treino.Ativo = true;

        return Registrar(itemId, AlvoAlteracaoTreinamento.TreinoAssunto,
            OperacaoAlteracaoTreinamento.Atualizar, treino.RoboAssuntoId, treino.Id,
            antes, Serializar(treino), justificativa);
    }

    public async Task<RoboTreinamentoAlteracao> DesativarTreinoAsync(
        Guid itemId, Guid treinoId, string justificativa, CancellationToken ct)
    {
        var treino = await db.RoboAssuntoTreinos.FirstOrDefaultAsync(t => t.Id == treinoId, ct)
            ?? throw new NaoEncontradoException("Treino", treinoId);

        var antes = Serializar(treino);
        treino.Ativo = false;

        return Registrar(itemId, AlvoAlteracaoTreinamento.TreinoAssunto,
            OperacaoAlteracaoTreinamento.Desativar, treino.RoboAssuntoId, treino.Id,
            antes, Serializar(treino), justificativa);
    }

    // ---------- condições (roteamento) ----------

    public async Task<RoboTreinamentoAlteracao> CriarCondicaoAsync(
        Guid itemId, Guid assuntoId, TipoCondicaoRobo tipo, string valor,
        string justificativa, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ValidacaoException("valor", "A condição precisa de um valor.");

        await GarantirAssuntoAsync(assuntoId, ct);
        ValidarCondicao(tipo, valor);

        // Condição repetida no mesmo assunto não muda nada e polui o roteamento.
        var v = valor.Trim();
        var jaTem = await db.RoboAssuntoCondicoes
            .AnyAsync(c => c.RoboAssuntoId == assuntoId && c.Tipo == tipo && c.Valor == v && c.Ativo, ct);
        if (jaTem)
            throw new ConflitoException("robo_treinamento.condicao_duplicada",
                $"Este assunto já tem a condição {tipo} \"{v}\".");

        var ordem = await db.RoboAssuntoCondicoes
            .Where(c => c.RoboAssuntoId == assuntoId)
            .Select(c => (int?)c.Ordem)
            .MaxAsync(ct) ?? 0;

        var cond = new RoboAssuntoCondicao
        {
            Id = Guid.CreateVersion7(),
            RoboAssuntoId = assuntoId,
            Tipo = tipo,
            Valor = v,
            Ordem = ordem + 10,
            Ativo = true,
        };
        db.RoboAssuntoCondicoes.Add(cond);

        return Registrar(itemId, AlvoAlteracaoTreinamento.CondicaoAssunto,
            OperacaoAlteracaoTreinamento.Criar, assuntoId, cond.Id,
            anterior: null, novo: Serializar(cond), justificativa);
    }

    public async Task<RoboTreinamentoAlteracao> DesativarCondicaoAsync(
        Guid itemId, Guid condicaoId, string justificativa, CancellationToken ct)
    {
        var cond = await db.RoboAssuntoCondicoes.FirstOrDefaultAsync(c => c.Id == condicaoId, ct)
            ?? throw new NaoEncontradoException("Condição", condicaoId);

        var antes = Serializar(cond);
        cond.Ativo = false;

        return Registrar(itemId, AlvoAlteracaoTreinamento.CondicaoAssunto,
            OperacaoAlteracaoTreinamento.Desativar, cond.RoboAssuntoId, cond.Id,
            antes, Serializar(cond), justificativa);
    }

    // ---------- desfazer ----------

    /// <summary>
    /// Reverte uma alteração aplicada. Criação vira desativação (não DELETE: o histórico da
    /// alteração aponta para a linha, e apagá-la deixaria o item contando uma história sem objeto).
    /// </summary>
    public async Task DesfazerAsync(RoboTreinamentoAlteracao alt, Guid? quem, CancellationToken ct)
    {
        if (alt.DesfeitoEm is not null)
            throw new ConflitoException("robo_treinamento.ja_desfeita", "Esta alteração já foi desfeita.");

        switch (alt.Alvo)
        {
            case AlvoAlteracaoTreinamento.TreinoAssunto:
            {
                var treino = await db.RoboAssuntoTreinos.FirstOrDefaultAsync(t => t.Id == alt.AlvoId, ct)
                    ?? throw new NaoEncontradoException("Treino", alt.AlvoId);
                if (alt.Operacao == OperacaoAlteracaoTreinamento.Criar) treino.Ativo = false;
                else RestaurarTreino(treino, alt.ValorAnteriorJson);
                break;
            }
            case AlvoAlteracaoTreinamento.CondicaoAssunto:
            {
                var cond = await db.RoboAssuntoCondicoes.FirstOrDefaultAsync(c => c.Id == alt.AlvoId, ct)
                    ?? throw new NaoEncontradoException("Condição", alt.AlvoId);
                if (alt.Operacao == OperacaoAlteracaoTreinamento.Criar) cond.Ativo = false;
                else RestaurarCondicao(cond, alt.ValorAnteriorJson);
                break;
            }
            default:
                throw new ValidacaoException("alvo", "Alvo de alteração desconhecido.");
        }

        alt.DesfeitoEm = DateTime.UtcNow;
        alt.DesfeitoPor = quem;
    }

    // ---------- apoio ----------

    private RoboTreinamentoAlteracao Registrar(
        Guid itemId, AlvoAlteracaoTreinamento alvo, OperacaoAlteracaoTreinamento op,
        Guid assuntoId, Guid alvoId, string? anterior, string? novo, string justificativa)
    {
        var alt = new RoboTreinamentoAlteracao
        {
            Id = Guid.CreateVersion7(),
            RoboTreinamentoItemId = itemId,
            Alvo = alvo,
            Operacao = op,
            RoboAssuntoId = assuntoId,
            AlvoId = alvoId,
            ValorAnteriorJson = anterior,
            ValorNovoJson = novo,
            Justificativa = string.IsNullOrWhiteSpace(justificativa) ? null : justificativa.Trim(),
            AplicadoEm = DateTime.UtcNow,
        };
        db.RoboTreinamentoAlteracoes.Add(alt);
        return alt;
    }

    private async Task GarantirAssuntoAsync(Guid assuntoId, CancellationToken ct)
    {
        var existe = await db.RoboAssuntos.AnyAsync(a => a.Id == assuntoId && a.ExcluidoEm == null, ct);
        if (!existe) throw new NaoEncontradoException("Assunto do robô", assuntoId);
    }

    /// <summary>Regex inválida entraria no classificador e derrubaria o roteamento de TODAS as
    /// mensagens — o pré-match roda antes de qualquer assunto ser escolhido.</summary>
    private static void ValidarCondicao(TipoCondicaoRobo tipo, string valor)
    {
        if (tipo != TipoCondicaoRobo.Regex) return;
        try
        {
            _ = System.Text.RegularExpressions.Regex.Match(
                string.Empty, valor, System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                TimeSpan.FromMilliseconds(50));
        }
        catch (ArgumentException ex)
        {
            throw new ValidacaoException("valor", $"Expressão regular inválida: {ex.Message}");
        }
    }

    private static string Serializar(RoboAssuntoTreino t) => JsonSerializer.Serialize(new
    {
        t.Id, Tipo = t.Tipo.ToString(), t.Titulo, t.Conteudo, t.Ordem, t.Ativo,
    }, Json);

    private static string Serializar(RoboAssuntoCondicao c) => JsonSerializer.Serialize(new
    {
        c.Id, Tipo = c.Tipo.ToString(), c.Valor, c.Ordem, c.Ativo,
    }, Json);

    private static void RestaurarTreino(RoboAssuntoTreino treino, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        if (r.TryGetProperty("tipo", out var t) && Enum.TryParse<TipoTreinoRobo>(t.GetString(), true, out var tipo))
            treino.Tipo = tipo;
        if (r.TryGetProperty("titulo", out var ti))
            treino.Titulo = ti.ValueKind == JsonValueKind.String ? ti.GetString() : null;
        if (r.TryGetProperty("conteudo", out var c) && c.ValueKind == JsonValueKind.String)
            treino.Conteudo = c.GetString() ?? treino.Conteudo;
        if (r.TryGetProperty("ordem", out var o) && o.TryGetInt32(out var ordem)) treino.Ordem = ordem;
        if (r.TryGetProperty("ativo", out var a)) treino.Ativo = a.ValueKind == JsonValueKind.True;
    }

    private static void RestaurarCondicao(RoboAssuntoCondicao cond, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        if (r.TryGetProperty("tipo", out var t) && Enum.TryParse<TipoCondicaoRobo>(t.GetString(), true, out var tipo))
            cond.Tipo = tipo;
        if (r.TryGetProperty("valor", out var v) && v.ValueKind == JsonValueKind.String)
            cond.Valor = v.GetString() ?? cond.Valor;
        if (r.TryGetProperty("ordem", out var o) && o.TryGetInt32(out var ordem)) cond.Ordem = ordem;
        if (r.TryGetProperty("ativo", out var a)) cond.Ativo = a.ValueKind == JsonValueKind.True;
    }
}
