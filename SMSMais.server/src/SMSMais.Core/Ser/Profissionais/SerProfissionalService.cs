using System.Globalization;
using System.Text;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Integracoes.SerWeb.Profissionais;
using SMSMais.Core.Medicos;
using SMSMais.Data;
using SMSMais.Data.Entities.Ser;

namespace SMSMais.Core.Ser.Profissionais;

public sealed record SerProfissionalDto(
    Guid Id,
    string Nome,
    string? Cpf,
    string? Documento,
    string? TipoDocumento,
    bool Ativo,
    int Ocorrencias,
    bool PresenteNoSer,
    DateTime UltimaLeituraEm,
    Guid? MedicoId,
    string? MedicoNome,
    DateTime? LigadoEm);

public sealed record PaginaSerProfissionaisDto(
    IReadOnlyList<SerProfissionalDto> Itens, int Total, int Pagina, int TamanhoPagina);

public sealed record SerProfissionaisResumoDto(
    int Total,
    int Ativos,
    int ComCpf,
    int Ligados,
    int ForaDoSer,
    DateTime? UltimaLeituraEm,
    bool ImportacaoEmExecucao,
    string? UltimoErro);

public sealed record SerProfissionaisFiltro
{
    /// <summary>Nome (sem exigir acento), CPF ou documento.</summary>
    public string? Termo { get; init; }

    /// <summary><c>ativos</c>, <c>inativos</c>, <c>fora</c> (sumiu do SER) ou vazio = todos no SER.</summary>
    public string? Situacao { get; init; }

    /// <summary><c>ligados</c>, <c>soltos</c> ou vazio.</summary>
    public string? Ligacao { get; init; }

    public int Pagina { get; init; } = 1;
    public int TamanhoPagina { get; init; } = 50;
}

public sealed record LigarMedicoSerRequest(Guid MedicoId);

public sealed record ResultadoImportacaoProfissionaisSer(
    int Lidas, int Novos, int Atualizados, int Sumiram, double DuracaoSegundos);

public interface ISerProfissionalService
{
    Task<PaginaSerProfissionaisDto> ListarAsync(SerProfissionaisFiltro filtro, CancellationToken ct);
    Task<SerProfissionaisResumoDto> ResumoAsync(CancellationToken ct);

    /// <summary>Lê a pesquisa inteira do SER e atualiza o espelho. Só leitura no SER.</summary>
    Task<ResultadoImportacaoProfissionaisSer> ImportarAsync(CancellationToken ct);

    /// <summary>Uma pessoa confirma: este profissional do SER é aquele médico nosso.</summary>
    Task<SerProfissionalDto> LigarAsync(Guid id, LigarMedicoSerRequest req, CancellationToken ct);

    Task<SerProfissionalDto> DesligarAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// O espelho dos profissionais do SER (ADR-0065).
///
/// <para><b>À parte do nosso cadastro, de propósito.</b> O cadastro do SER é ruim (CPF quase
/// sempre vazio, duplicados, homônimos): nada daqui entra no nosso cadastro de Médicos, e a
/// ligação com um médico nosso é sempre feita por uma pessoa.</para>
/// </summary>
public sealed class SerProfissionalService(
    SmsMaisDbContext db,
    ISerProfissionalLeitor leitor,
    IMedicosService medicos,
    IUsuarioAtualAccessor usuarioAtual,
    Background.ISerProfissionalImportacaoFila fila) : ISerProfissionalService
{
    public async Task<PaginaSerProfissionaisDto> ListarAsync(SerProfissionaisFiltro filtro, CancellationToken ct)
    {
        var q = db.SerProfissionais.AsNoTracking();

        q = (filtro.Situacao ?? string.Empty).ToLowerInvariant() switch
        {
            "ativos" => q.Where(p => p.PresenteNoSer && p.Ativo),
            "inativos" => q.Where(p => p.PresenteNoSer && !p.Ativo),
            "fora" => q.Where(p => !p.PresenteNoSer),
            _ => q.Where(p => p.PresenteNoSer),
        };

        q = (filtro.Ligacao ?? string.Empty).ToLowerInvariant() switch
        {
            "ligados" => q.Where(p => p.MedicoId != null),
            "soltos" => q.Where(p => p.MedicoId == null),
            _ => q,
        };

        if (!string.IsNullOrWhiteSpace(filtro.Termo))
        {
            var digitos = new string([.. filtro.Termo.Where(char.IsAsciiDigit)]);
            var nome = Normalizar(filtro.Termo);
            q = digitos.Length >= 3
                ? q.Where(p => (p.Cpf != null && p.Cpf.StartsWith(digitos))
                               || (p.Documento != null && p.Documento.StartsWith(digitos)))
                : q.Where(p => p.NomeNormalizado.Contains(nome));
        }

        var tamanho = Math.Clamp(filtro.TamanhoPagina, 1, 200);
        var pagina = Math.Max(1, filtro.Pagina);
        var total = await q.CountAsync(ct);

        var itens = await q
            .OrderBy(p => p.NomeNormalizado)
            .Skip((pagina - 1) * tamanho)
            .Take(tamanho)
            .Select(p => Dto(p))
            .ToListAsync(ct);

        return new PaginaSerProfissionaisDto(itens, total, pagina, tamanho);
    }

    public async Task<SerProfissionaisResumoDto> ResumoAsync(CancellationToken ct)
    {
        var presentes = db.SerProfissionais.AsNoTracking().Where(p => p.PresenteNoSer);
        return new SerProfissionaisResumoDto(
            await presentes.CountAsync(ct),
            await presentes.CountAsync(p => p.Ativo, ct),
            await presentes.CountAsync(p => p.Cpf != null, ct),
            await presentes.CountAsync(p => p.MedicoId != null, ct),
            await db.SerProfissionais.CountAsync(p => !p.PresenteNoSer, ct),
            await db.SerProfissionais.MaxAsync(p => (DateTime?)p.UltimaLeituraEm, ct),
            fila.EmExecucao,
            fila.UltimoErro);
    }

    public async Task<ResultadoImportacaoProfissionaisSer> ImportarAsync(CancellationToken ct)
    {
        var inicio = DateTime.UtcNow;
        var linhas = await leitor.LerTodosAsync(ct);

        var presentesAntes = await db.SerProfissionais.CountAsync(p => p.PresenteNoSer, ct);

        // Leitura que voltou com menos da metade do que havia é leitura PARCIAL (sessão caiu,
        // tela mudou) — marcar o resto como "fora do SER" apagaria a realidade com um defeito.
        if (linhas.Count == 0 || (presentesAntes > 0 && linhas.Count < presentesAntes / 2))
        {
            throw new ValidacaoException(
                "ser.profissionais.leitura_parcial",
                $"A pesquisa do SER devolveu {linhas.Count} profissionais (havia {presentesAntes}). "
                + "Parece leitura incompleta — nada foi alterado. Tente de novo.");
        }

        var grupos = linhas
            .GroupBy(Chave)
            .Select(g => (Chave: g.Key, Linha: g.First(), Qtd: g.Count(), Ativo: g.Any(l => l.Ativo)))
            .ToList();

        var existentes = await db.SerProfissionais.ToDictionaryAsync(p => p.Chave, ct);
        var agora = DateTime.UtcNow;
        int novos = 0, atualizados = 0;

        foreach (var (chave, l, qtd, ativo) in grupos)
        {
            if (!existentes.TryGetValue(chave, out var p))
            {
                p = new SerProfissional
                {
                    Id = Guid.CreateVersion7(),
                    Chave = chave,
                    PrimeiraLeituraEm = agora,
                };
                db.SerProfissionais.Add(p);
                novos++;
            }
            else
            {
                atualizados++;
            }

            p.Nome = l.Nome;
            p.NomeNormalizado = Normalizar(l.Nome);
            p.Cpf = CpfOuNulo(l.Cpf);
            p.Documento = l.Documento;
            p.TipoDocumento = l.TipoDocumento;
            p.Ativo = ativo;
            p.Ocorrencias = qtd;
            p.PresenteNoSer = true;
            p.UltimaLeituraEm = agora;
        }

        var vistas = grupos.Select(g => g.Chave).ToHashSet(StringComparer.Ordinal);
        var sumiram = 0;
        foreach (var p in existentes.Values.Where(p => p.PresenteNoSer && !vistas.Contains(p.Chave)))
        {
            // Não apaga: a ligação com o nosso médico e a história ficam. Só sai da lista.
            p.PresenteNoSer = false;
            sumiram++;
        }

        await db.SaveChangesAsync(ct);

        return new ResultadoImportacaoProfissionaisSer(
            linhas.Count, novos, atualizados, sumiram, (DateTime.UtcNow - inicio).TotalSeconds);
    }

    public async Task<SerProfissionalDto> LigarAsync(Guid id, LigarMedicoSerRequest req, CancellationToken ct)
    {
        var p = await CarregarAsync(id, ct);

        // Confirma que o médico existe no nosso cadastro (lança NaoEncontrado) e guarda o nome
        // para a tela listar sem ir ao FHIR a cada linha.
        var medico = await medicos.ObterPorIdAsync(req.MedicoId, ct);

        p.MedicoId = medico.Id;
        p.MedicoNome = medico.NomeCompleto;
        p.LigadoEm = DateTime.UtcNow;
        p.LigadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
        return Dto(p);
    }

    public async Task<SerProfissionalDto> DesligarAsync(Guid id, CancellationToken ct)
    {
        var p = await CarregarAsync(id, ct);
        p.MedicoId = null;
        p.MedicoNome = null;
        p.LigadoEm = null;
        p.LigadoPor = null;
        await db.SaveChangesAsync(ct);
        return Dto(p);
    }

    // ------------------------------------------------------------------ apoio

    private async Task<SerProfissional> CarregarAsync(Guid id, CancellationToken ct) =>
        await db.SerProfissionais.FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw new NaoEncontradoException("ser.profissional", $"Profissional do SER {id} não encontrado.");

    private static SerProfissionalDto Dto(SerProfissional p) => new(
        p.Id, p.Nome, p.Cpf, p.Documento, p.TipoDocumento, p.Ativo, p.Ocorrencias, p.PresenteNoSer,
        p.UltimaLeituraEm, p.MedicoId, p.MedicoNome, p.LigadoEm);

    /// <summary>CPF quando tem 11 dígitos (o SER não valida o DV, então não validamos também —
    /// o espelho guarda o que o SER tem); senão o documento + tipo + nome.</summary>
    internal static string Chave(SerProfissionalLinha l) =>
        CpfOuNulo(l.Cpf) is { } cpf
            ? $"cpf:{cpf}"
            : $"doc:{l.TipoDocumento ?? "-"}:{l.Documento ?? "-"}:{Normalizar(l.Nome)}";

    private static string? CpfOuNulo(string? cpf) =>
        cpf is { Length: 11 } && cpf.All(char.IsAsciiDigit) ? cpf : null;

    /// <summary>Maiúsculo, sem acento, espaços colapsados.</summary>
    internal static string Normalizar(string texto)
    {
        var d = (texto ?? string.Empty).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        var espaco = false;
        foreach (var c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c))
            {
                espaco = sb.Length > 0;
                continue;
            }
            if (espaco) sb.Append(' ');
            espaco = false;
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }
}
