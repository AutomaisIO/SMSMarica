using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Core.Notificacoes.Comunicacao;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Notificacoes;

namespace SMSMais.Core.Notificacoes.AgendamentosExternos;

/// <summary>Onde o paciente vai, com o endereço do cadastro de Unidades de Atendimento.</summary>
/// <param name="Nome">O nome da unidade de atendimento ligada (o do cadastro, mais limpo que o do sistema).</param>
/// <param name="Endereco">"Rua X, 375 · Bairro · Cidade/UF" — nulo quando a unidade não tem endereço.</param>
public sealed record LocalComEnderecoDto(
    Guid UnidadeAtendimentoId, string Nome, string? Endereco, string? PontoReferencia, string? Telefone);

/// <summary>Uma unidade de atendimento oferecida como ligação de um nome.</summary>
public sealed record UnidadeAtendimentoOpcaoDto(Guid Id, string Nome, bool TemEndereco);

/// <summary>Um local como o SER/SERNIT/ESUS o escreve, com quantos agendamentos futuros o usam.</summary>
/// <param name="Ligada">A unidade de atendimento ligada por uma pessoa (é dela o endereço do aviso).</param>
/// <param name="Sugerida">O palpite da máquina — só aparece quando não há ligação, e nunca vale sozinho.</param>
public sealed record LocalAgendamentoExternoDto(
    string Nome,
    IReadOnlyList<string> Sistemas,
    int AgendamentosFuturos,
    DateOnly? ProximaData,
    UnidadeAtendimentoOpcaoDto? Ligada,
    UnidadeAtendimentoOpcaoDto? Sugerida);

/// <summary>Ligar (ou desligar, com <see cref="UnidadeAtendimentoId"/> nulo) um nome a uma unidade.</summary>
public sealed record VincularLocalExternoRequest(string Nome, Guid? UnidadeAtendimentoId);

public interface ILocaisAgendamentoExternoService
{
    /// <summary>Os locais dos agendamentos futuros do SER/SERNIT/ESUS, os sem endereço primeiro.</summary>
    Task<IReadOnlyList<LocalAgendamentoExternoDto>> ListarAsync(CancellationToken ct = default);

    /// <summary>Liga o nome a uma unidade de atendimento (ou desfaz a ligação). Salva.</summary>
    Task VincularAsync(VincularLocalExternoRequest request, CancellationToken ct = default);

    /// <summary>A unidade ligada ao nome, com endereço — nulo quando ninguém ligou ainda.</summary>
    Task<LocalComEnderecoDto?> ResolverAsync(string? nomeNoSistema, CancellationToken ct = default);
}

/// <summary>
/// Os locais dos agendamentos externos e a ligação de cada um com o cadastro de Unidades de
/// Atendimento — de onde sai o ENDEREÇO do aviso ao paciente (10/10/2026). Os espelhos só trazem o
/// nome ("MS HGB HOSPITAL GERAL DE BONSUCESSO").
///
/// <para>A ligação é de uma pessoa. A sugestão existe para poupar digitação, não para decidir:
/// endereço errado num aviso manda o paciente para outra cidade.</para>
/// </summary>
public sealed class LocaisAgendamentoExternoService(SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual)
    : ILocaisAgendamentoExternoService
{
    /// <summary>Palavras que não distinguem uma unidade de outra (esfera, preposições, a UF).</summary>
    private static readonly HashSet<string> Vazias =
    [
        "DE", "DA", "DO", "DOS", "DAS", "E", "MS", "SES", "SESDEC", "RJ", "RIO", "JANEIRO", "UNIDADE",
    ];

    public async Task<IReadOnlyList<LocalAgendamentoExternoDto>> ListarAsync(CancellationToken ct = default)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var agendamentos = await db.AgendamentosExternos.AsNoTracking()
            .Where(a => a.Situacao == SituacaoAgendamentoExterno.Ativo && a.Data >= hoje && a.LocalNome != null)
            .Select(a => new { a.LocalNome, a.Sistema, a.Data })
            .ToListAsync(ct);

        var unidades = await db.UnidadesAtendimento.AsNoTracking()
            .Where(u => u.Ativo)
            .Select(u => new { u.Id, u.Nome, TemEndereco = u.Endereco != null && u.Endereco.Logradouro != null })
            .ToListAsync(ct);
        var opcoes = unidades.ToDictionary(u => u.Id, u => new UnidadeAtendimentoOpcaoDto(u.Id, u.Nome, u.TemEndereco));

        var ligacoes = await db.UnidadeAtendimentoNomesExternos.AsNoTracking()
            .Select(n => new { n.NomeNormalizado, n.UnidadeAtendimentoId })
            .ToListAsync(ct);
        var ligadaPorNome = ligacoes.ToDictionary(n => n.NomeNormalizado, n => n.UnidadeAtendimentoId);

        var tokensDasUnidades = unidades.Select(u => (u.Id, Tokens: Tokens(u.Nome))).ToList();

        return agendamentos
            .GroupBy(a => LeituraAgendamentoExterno.NormalizarNome(a.LocalNome))
            .Select(g =>
            {
                var nome = g.Select(a => a.LocalNome!).First();
                UnidadeAtendimentoOpcaoDto? ligada = ligadaPorNome.TryGetValue(g.Key, out var uid)
                    ? opcoes.GetValueOrDefault(uid) : null;
                return new LocalAgendamentoExternoDto(
                    nome,
                    g.Select(a => AgendamentoExternoService.Rotulo(a.Sistema)).Distinct().Order().ToList(),
                    g.Count(),
                    g.Min(a => a.Data),
                    ligada,
                    ligada is null && Sugerir(nome, tokensDasUnidades) is { } sug ? opcoes.GetValueOrDefault(sug) : null);
            })
            // Os que ainda mandam aviso SEM endereço primeiro, e entre eles os que mais pesam.
            .OrderBy(l => l.Ligada is { TemEndereco: true } ? 1 : 0)
            .ThenByDescending(l => l.AgendamentosFuturos)
            .ThenBy(l => l.Nome)
            .ToList();
    }

    public async Task VincularAsync(VincularLocalExternoRequest request, CancellationToken ct = default)
    {
        var chave = LeituraAgendamentoExterno.NormalizarNome(request.Nome);
        if (chave.Length == 0)
            throw new ValidacaoException("nome", "Informe o nome do local como o sistema de regulação mostra.");
        if (chave.Length > 300)
            throw new ValidacaoException("nome", "Nome do local longo demais.");

        var existente = await db.UnidadeAtendimentoNomesExternos.FirstOrDefaultAsync(n => n.NomeNormalizado == chave, ct);
        if (request.UnidadeAtendimentoId is not { } unidadeId)
        {
            if (existente is not null) db.UnidadeAtendimentoNomesExternos.Remove(existente);
            await db.SaveChangesAsync(ct);
            return;
        }

        var unidadeExiste = await db.UnidadesAtendimento.AnyAsync(u => u.Id == unidadeId && u.Ativo, ct);
        if (!unidadeExiste)
            throw new NaoEncontradoException("Unidade de atendimento", unidadeId);

        if (existente is null)
        {
            db.UnidadeAtendimentoNomesExternos.Add(new UnidadeAtendimentoNomeExterno
            {
                Id = Guid.CreateVersion7(),
                UnidadeAtendimentoId = unidadeId,
                Nome = LeituraAgendamentoExterno.Espacos(request.Nome),
                NomeNormalizado = chave,
                CriadoEm = DateTime.UtcNow,
                CriadoPor = usuarioAtual.UsuarioId,
            });
        }
        else
        {
            existente.UnidadeAtendimentoId = unidadeId;
            existente.CriadoEm = DateTime.UtcNow;
            existente.CriadoPor = usuarioAtual.UsuarioId;
        }
        await db.SaveChangesAsync(ct);
    }

    public Task<LocalComEnderecoDto?> ResolverAsync(string? nomeNoSistema, CancellationToken ct = default) =>
        ResolverAsync(db, nomeNoSistema, ct);

    /// <summary>A mesma resolução, para quem já tem o contexto (o envio do aviso).</summary>
    internal static async Task<LocalComEnderecoDto?> ResolverAsync(
        SmsMaisDbContext db, string? nomeNoSistema, CancellationToken ct)
    {
        var chave = LeituraAgendamentoExterno.NormalizarNome(nomeNoSistema);
        if (chave.Length == 0) return null;
        var u = await db.UnidadeAtendimentoNomesExternos.AsNoTracking()
            .Where(n => n.NomeNormalizado == chave && n.UnidadeAtendimento!.Ativo)
            .Select(n => n.UnidadeAtendimento!)
            .FirstOrDefaultAsync(ct);
        if (u is null) return null;
        return new LocalComEnderecoDto(
            u.Id, u.Nome, ComunicacaoPacienteService.EnderecoEmUmaLinha(u.Endereco),
            string.IsNullOrWhiteSpace(u.Endereco?.PontoReferencia) ? null : u.Endereco!.PontoReferencia!.Trim(),
            string.IsNullOrWhiteSpace(u.Telefone) ? null : u.Telefone.Trim());
    }

    /// <summary>
    /// O palpite: a unidade com mais palavras em comum (Jaccard), se ela se destaca da segunda. Sem
    /// destaque claro, não sugere — palpite fraco ensina a pessoa a clicar sem ler.
    /// </summary>
    internal static Guid? Sugerir(string nome, IReadOnlyList<(Guid Id, HashSet<string> Tokens)> unidades)
    {
        var alvo = Tokens(nome);
        if (alvo.Count == 0) return null;
        var notas = unidades
            .Select(u => (u.Id, Nota: Jaccard(alvo, u.Tokens)))
            .OrderByDescending(x => x.Nota)
            .Take(2)
            .ToList();
        if (notas.Count == 0 || notas[0].Nota < 0.4) return null;
        if (notas.Count > 1 && notas[0].Nota - notas[1].Nota < 0.15) return null;
        return notas[0].Id;
    }

    internal static HashSet<string> Tokens(string? nome) =>
        LeituraAgendamentoExterno.NormalizarNome(nome)
            .Split([' ', '-', '(', ')', '.', ',', '/'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 1 && !Vazias.Contains(t))
            .ToHashSet();

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var inter = a.Count(b.Contains);
        return (double)inter / (a.Count + b.Count - inter);
    }
}
