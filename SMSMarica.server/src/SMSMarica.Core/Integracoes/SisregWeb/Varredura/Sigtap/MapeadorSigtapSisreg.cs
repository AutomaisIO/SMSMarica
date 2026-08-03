using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Sisreg;

namespace SMSMarica.Core.Integracoes.SisregWeb.Varredura.Sigtap;

/// <summary>Um procedimento do SISREG e o estado do seu de-para para o SIGTAP.</summary>
public sealed record ProcedimentoSigtapDeParaDto(
    Guid Id,
    string Codigo,
    string Nome,
    bool Grupo,
    string? CodigoSigtap,
    bool Confirmado,
    Guid? SugeridoSigtapId,
    string? SugeridoCodigo,
    string? SugeridoNome,
    float? SugeridoScore,
    DateTime? ConfirmadoEm);

public interface IMapeadorSigtapSisreg
{
    /// <summary>Registra os procedimentos vistos no SISREG no catálogo do de-para (upsert por código).</summary>
    Task RegistrarVistosAsync(IReadOnlyCollection<ProcedimentoVisto> vistos, CancellationToken cancellationToken);

    /// <summary>SIGTAP (só dígitos) CONFIRMADO para um código do SISREG. Null = ainda não mapeado.</summary>
    Task<string?> ResolverConfirmadoAsync(string codigoSisreg, CancellationToken cancellationToken);

    /// <summary>
    /// SIGTAP a partir do NOME do procedimento como o agendamento o informa. É a resolução
    /// principal: o código do SISREG usado na consulta é só o filtro da varredura — quem diz o que
    /// o exame é de verdade é o próprio registro. Assim, varrer por um "GRUPO -" não carimba todos
    /// os agendamentos com o procedimento do grupo.
    ///
    /// <para><b>Só nome idêntico</b> (depois de normalizar). "MAMOGRAFIA BILATERAL" e "MAMOGRAFIA
    /// BILATERAL PARA RASTREAMENTO" são SIGTAPs diferentes com nomes vizinhos: um palpite aqui não
    /// dá erro em lugar nenhum — dá worklist errada e laudo no exame errado, semanas depois.</para>
    /// </summary>
    Task<string?> ResolverPorNomeExatoAsync(string? nomeProcedimento, CancellationToken cancellationToken);

    /// <summary>Versão em lote: código do SISREG → SIGTAP confirmado. Só entram os confirmados.</summary>
    Task<IReadOnlyDictionary<string, string>> ResolverConfirmadosAsync(
        IReadOnlyCollection<string> codigosSisreg, CancellationToken cancellationToken);

    /// <summary>
    /// Estado de catálogo dos códigos informados (confirmados ou não). A tela de mapeamento
    /// precisa disto para mostrar, em cada procedimento, o SIGTAP e o botão de confirmação —
    /// que é chaveado pelo id do CATÁLOGO, não pelo id do procedimento na unidade.
    /// </summary>
    Task<IReadOnlyDictionary<string, ProcedimentoCatalogoInfo>> ObterCatalogoAsync(
        IReadOnlyCollection<string> codigosSisreg, CancellationToken cancellationToken);

    /// <summary>
    /// Códigos do SISREG (<c>pa</c>) que mapeiam para um código SIGTAP. Usado só pela importação
    /// por ARQUIVO, que não conhece o <c>pa</c> — o caminho está em extinção, mas enquanto existir
    /// tem que respeitar o mesmo gatilho de confirmação da varredura.
    /// </summary>
    Task<IReadOnlyList<string>> ResolverCodigosPorSigtapAsync(
        string codigoSigtap, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcedimentoSigtapDeParaDto>> ListarAsync(
        bool somenteNaoConfirmados, CancellationToken cancellationToken);

    /// <summary>Roda a heurística sobre os não confirmados. Devolve quantos ganharam sugestão.</summary>
    Task<int> SugerirAsync(CancellationToken cancellationToken);

    /// <summary>Confirma o de-para. É o que libera o procedimento para a varredura.</summary>
    Task<ProcedimentoSigtapDeParaDto> ConfirmarAsync(
        Guid id, Guid procedimentoSigtapId, CancellationToken cancellationToken);
}

/// <summary>Procedimento como o SISREG o devolveu no AJAX de mapeamento.</summary>
public sealed record ProcedimentoVisto(string Codigo, string Nome, bool Grupo);

/// <summary>O que a tela de mapeamento precisa saber do catálogo sobre um procedimento.</summary>
/// <param name="DeParaId">Id da linha do CATÁLOGO — é por ele que os toggles são chamados.</param>
public sealed record ProcedimentoCatalogoInfo(
    Guid DeParaId, string? CodigoSigtap, bool Confirmado);

/// <summary>Confirmação do de-para: qual procedimento SIGTAP corresponde ao <c>pa</c> do SISREG.</summary>
public sealed record ConfirmarDeParaSigtapRequest(Guid ProcedimentoSigtapId);

/// <summary>
/// Mantém o de-para entre o código de procedimento do SISREG (o <c>pa</c>) e o SIGTAP oficial.
/// Ver <see cref="SisregProcedimentoSigtap"/> para o porquê de ser catálogo global.
/// </summary>
public sealed class MapeadorSigtapSisreg(
    SmsMaricaDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<MapeadorSigtapSisreg> logger) : IMapeadorSigtapSisreg
{
    public async Task RegistrarVistosAsync(
        IReadOnlyCollection<ProcedimentoVisto> vistos, CancellationToken cancellationToken)
    {
        if (vistos.Count == 0) return;

        var codigos = vistos.Select(v => v.Codigo).Distinct(StringComparer.Ordinal).ToArray();
        var existentes = await db.SisregProcedimentosSigtap
            .Where(x => codigos.Contains(x.Codigo))
            .ToDictionaryAsync(x => x.Codigo, StringComparer.Ordinal, cancellationToken);

        var agora = DateTime.UtcNow;

        foreach (var visto in vistos.DistinctBy(v => v.Codigo, StringComparer.Ordinal))
        {
            if (existentes.TryGetValue(visto.Codigo, out var catalogado))
            {
                // O `pa` deveria ser nacional. Se o mesmo código voltar com outro nome, o de-para
                // confirmado pode estar apontando para o procedimento errado — não sobrescrevemos
                // nada e deixamos o rastro para decidir se o catálogo precisa virar por unidade.
                if (catalogado.ConfirmadoEm is not null
                    && !NomesEquivalentes(catalogado.Nome, visto.Nome))
                {
                    logger.LogWarning(
                        "SISREG_PA_DIVERGENTE: código {Codigo} estava mapeado como \"{Antigo}\" e voltou "
                        + "como \"{Novo}\" — o de-para confirmado NÃO foi alterado.",
                        visto.Codigo, catalogado.Nome, visto.Nome);
                }
                else
                {
                    catalogado.Nome = visto.Nome;
                }

                catalogado.Grupo = visto.Grupo;
                catalogado.VistoEm = agora;
                continue;
            }

            db.SisregProcedimentosSigtap.Add(new SisregProcedimentoSigtap
            {
                Id = Guid.CreateVersion7(),
                Codigo = visto.Codigo,
                Nome = visto.Nome,
                Grupo = visto.Grupo,
                PrimeiroVistoEm = agora,
                VistoEm = agora,
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string?> ResolverConfirmadoAsync(string codigoSisreg, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(codigoSisreg)) return null;

        var codigo = codigoSisreg.Trim();
        return await db.SisregProcedimentosSigtap
            .AsNoTracking()
            .Where(x => x.Codigo == codigo && x.ConfirmadoEm != null && x.CodigoSigtap != null)
            .Select(x => x.CodigoSigtap)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolverConfirmadosAsync(
        IReadOnlyCollection<string> codigosSisreg, CancellationToken cancellationToken)
    {
        if (codigosSisreg.Count == 0)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var codigos = codigosSisreg
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var pares = await db.SisregProcedimentosSigtap
            .AsNoTracking()
            .Where(x => codigos.Contains(x.Codigo) && x.ConfirmadoEm != null && x.CodigoSigtap != null)
            .Select(x => new { x.Codigo, x.CodigoSigtap })
            .ToListAsync(cancellationToken);

        return pares.ToDictionary(p => p.Codigo, p => p.CodigoSigtap!, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyDictionary<string, ProcedimentoCatalogoInfo>> ObterCatalogoAsync(
        IReadOnlyCollection<string> codigosSisreg, CancellationToken cancellationToken)
    {
        if (codigosSisreg.Count == 0)
            return new Dictionary<string, ProcedimentoCatalogoInfo>(StringComparer.Ordinal);

        var codigos = codigosSisreg
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var itens = await db.SisregProcedimentosSigtap.AsNoTracking()
            .Where(x => codigos.Contains(x.Codigo))
            .Select(x => new { x.Codigo, x.Id, x.CodigoSigtap, x.ConfirmadoEm })
            .ToListAsync(cancellationToken);

        return itens.ToDictionary(
            x => x.Codigo,
            x => new ProcedimentoCatalogoInfo(
                x.Id, x.CodigoSigtap, x.ConfirmadoEm is not null),
            StringComparer.Ordinal);
    }

    public async Task<string?> ResolverPorNomeExatoAsync(
        string? nomeProcedimento, CancellationToken cancellationToken)
    {
        var alvo = SugestaoSigtap.Normalizar(nomeProcedimento ?? string.Empty);
        if (alvo.Length == 0) return null;

        // Normalização (acento, pontuação, prefixo "GRUPO -") não tem equivalente em SQL aqui —
        // pg_trgm/unaccent não cobrem o mesmo conjunto de regras. O catálogo tem alguns milhares
        // de linhas; comparar em memória custa menos que manter duas implementações divergentes.
        var catalogo = await db.ProcedimentosSigtap.AsNoTracking()
            .Where(p => p.Ativo)
            .Select(p => new { p.Codigo, p.Nome })
            .ToListAsync(cancellationToken);

        var achados = catalogo
            .Where(p => string.Equals(SugestaoSigtap.Normalizar(p.Nome), alvo, StringComparison.Ordinal))
            .Select(p => SoDigitos(p.Codigo))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (achados.Count == 1) return achados[0];

        if (achados.Count > 1)
        {
            // Dois SIGTAPs com o mesmo nome normalizado: escolher um seria sortear. Vira pendência.
            logger.LogWarning(
                "SIGTAP_NOME_AMBIGUO: \"{Nome}\" casa com {Qtd} procedimentos SIGTAP ({Codigos}) — "
                + "sem resolucao automatica.", nomeProcedimento, achados.Count, string.Join(", ", achados));
        }

        return null;
    }

    public async Task<IReadOnlyList<string>> ResolverCodigosPorSigtapAsync(
        string codigoSigtap, CancellationToken cancellationToken)
    {
        var sigtap = SoDigitos(codigoSigtap ?? string.Empty);
        if (sigtap.Length == 0) return [];

        return await db.SisregProcedimentosSigtap.AsNoTracking()
            .Where(x => x.CodigoSigtap == sigtap)
            .Select(x => x.Codigo)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcedimentoSigtapDeParaDto>> ListarAsync(
        bool somenteNaoConfirmados, CancellationToken cancellationToken)
    {
        var query = db.SisregProcedimentosSigtap.AsNoTracking();
        if (somenteNaoConfirmados) query = query.Where(x => x.ConfirmadoEm == null);

        var itens = await query
            .OrderBy(x => x.Nome)
            .Select(x => new
            {
                x.Id, x.Codigo, x.Nome, x.Grupo, x.CodigoSigtap, x.ConfirmadoEm,
                x.SugeridoSigtapId, x.SugeridoScore,
                SugeridoCodigo = db.ProcedimentosSigtap
                    .Where(p => p.Id == x.SugeridoSigtapId).Select(p => p.Codigo).FirstOrDefault(),
                SugeridoNome = db.ProcedimentosSigtap
                    .Where(p => p.Id == x.SugeridoSigtapId).Select(p => p.Nome).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        return [.. itens.Select(x => new ProcedimentoSigtapDeParaDto(
            x.Id, x.Codigo, x.Nome, x.Grupo, x.CodigoSigtap, x.ConfirmadoEm is not null,
            x.SugeridoSigtapId, x.SugeridoCodigo, x.SugeridoNome, x.SugeridoScore, x.ConfirmadoEm))];
    }

    public async Task<int> SugerirAsync(CancellationToken cancellationToken)
    {
        var pendentes = await db.SisregProcedimentosSigtap
            .Where(x => x.ConfirmadoEm == null)
            .ToListAsync(cancellationToken);

        if (pendentes.Count == 0) return 0;

        // O catálogo SIGTAP tem alguns milhares de linhas — carregar uma vez e comparar em memória
        // custa menos que uma consulta por procedimento, e permite heurística que o SQL não faz
        // (pg_trgm não está instalado neste banco).
        var catalogo = await db.ProcedimentosSigtap
            .AsNoTracking()
            .Where(p => p.Ativo)
            .Select(p => new SugestaoSigtap.Candidato(p.Id, p.Codigo, p.Nome))
            .ToListAsync(cancellationToken);

        var agora = DateTime.UtcNow;
        var sugeridos = 0;

        foreach (var pendente in pendentes)
        {
            var sugestao = SugestaoSigtap.Sugerir(pendente.Nome, catalogo);
            if (sugestao is null) continue;

            pendente.SugeridoSigtapId = sugestao.ProcedimentoSigtapId;
            pendente.SugeridoScore = (float)sugestao.Score;
            sugeridos++;

            // Só nome idêntico se auto-confirma. Semelhança espera o operador — ver SugestaoSigtap.
            if (sugestao.Exata)
            {
                pendente.ProcedimentoSigtapId = sugestao.ProcedimentoSigtapId;
                pendente.CodigoSigtap = SoDigitos(sugestao.Codigo);
                pendente.ConfirmadoEm = agora;
                pendente.ConfirmadoPor = usuarioAtual.UsuarioId;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return sugeridos;
    }

    public async Task<ProcedimentoSigtapDeParaDto> ConfirmarAsync(
        Guid id, Guid procedimentoSigtapId, CancellationToken cancellationToken)
    {
        var dePara = await db.SisregProcedimentosSigtap.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Procedimento do SISREG", id);

        var sigtap = await db.ProcedimentosSigtap
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == procedimentoSigtapId, cancellationToken)
            ?? throw new NaoEncontradoException("Procedimento SIGTAP", procedimentoSigtapId);

        dePara.ProcedimentoSigtapId = sigtap.Id;
        dePara.CodigoSigtap = SoDigitos(sigtap.Codigo);
        dePara.ConfirmadoEm = DateTime.UtcNow;
        dePara.ConfirmadoPor = usuarioAtual.UsuarioId;

        await db.SaveChangesAsync(cancellationToken);

        return new ProcedimentoSigtapDeParaDto(
            dePara.Id, dePara.Codigo, dePara.Nome, dePara.Grupo, dePara.CodigoSigtap, true,
            dePara.SugeridoSigtapId, sigtap.Codigo, sigtap.Nome, dePara.SugeridoScore, dePara.ConfirmadoEm);
    }

    /// <summary>Comparação tolerante a acento/pontuação — o SISREG oscila na grafia entre varreduras.</summary>
    private static bool NomesEquivalentes(string a, string b) =>
        string.Equals(SugestaoSigtap.Normalizar(a), SugestaoSigtap.Normalizar(b), StringComparison.Ordinal);

    private static string SoDigitos(string valor) => new([.. valor.Where(char.IsDigit)]);
}
