using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Armazenamento;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Pacientes;
using SMSMarica.Data;
using SMSMarica.Data.Entities;

namespace SMSMarica.Core.Exames;

/// <summary>
/// Gera, sob demanda, o PDF consolidado das imagens de um exame (estudo no PACS) e o
/// guarda no armazenamento de objetos (S3) na pasta do paciente. A chave é estável
/// (derivada do StudyInstanceUID), então a 2ª chamada em diante reaproveita o cache.
/// </summary>
public interface IExameImagensPdfService
{
    /// <summary>Gera (ou recupera do cache) o PDF consolidado das imagens do estudo da solicitação.</summary>
    Task<byte[]> GerarOuObterAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compara (só leitura) o que o cache serviu com o que o PACS tem agora para o estudo do exame.
    /// Usado pela re-validação de fundo para detectar estudos completados após a materialização.
    /// </summary>
    Task<ReavaliacaoImagens> ReavaliarAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default);

    /// <summary>Invalida os caches do exame: PDF consolidado do cidadão + render-cache do estudo.</summary>
    Task InvalidarAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default);
}

/// <summary>Fotografia da defasagem do cache de imagens de um exame frente ao PACS.</summary>
/// <param name="StudyInstanceUID">Estudo resolvido (associação ativa ou o da própria solicitação).</param>
/// <param name="ImagensCache">Nº de imagens no render-cache (null se não há cache).</param>
/// <param name="ImagensPacs">Nº de instâncias no PACS agora.</param>
/// <param name="Defasado">true quando o PACS tem MAIS imagens do que o cache serviu.</param>
public sealed record ReavaliacaoImagens(string? StudyInstanceUID, int? ImagensCache, int ImagensPacs, bool Defasado);

public sealed class ExameImagensPdfService(
    SmsMaricaDbContext db,
    IArmazenamentoArquivos armazenamento,
    IExamePacsImagensReader imagensReader,
    IPacientesService pacientes) : IExameImagensPdfService
{
    /// <summary>Teto de imagens incluídas no PDF (estudos de imagem da SMS são pequenos; trava de segurança).</summary>
    private const int MaxImagens = 300;

    public async Task<byte[]> GerarOuObterAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var sol = await db.ExamesImagem.AsNoTracking()
            .Include(s => s.TipoExame)
            .Include(s => s.Solicitacao!).ThenInclude(so => so.UnidadeExecutante)
            .FirstOrDefaultAsync(s => s.Id == solicitacaoExameId && s.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), solicitacaoExameId);

        // Exames sem worklist (ex.: mamografia no Fuji, que gera o próprio StudyInstanceUID)
        // chegam ao PACS sob o UID do EQUIPAMENTO, ligado à solicitação via ExameAssociacao.
        // As imagens vivem sob esse UID real — não sob o pré-gerado da solicitação. Preferimos
        // o UID da associação ativa; sem associação, usamos o da própria solicitação.
        var studyUid = await db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.ExameImagemId == sol.Id && a.ExcluidoEm == null && a.StudyInstanceUID != "")
            .OrderByDescending(a => a.CriadoEm)
            .Select(a => a.StudyInstanceUID)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(studyUid)) studyUid = sol.StudyInstanceUID;

        if (string.IsNullOrWhiteSpace(studyUid))
            throw new ConflitoException("exame.sem_imagens", "Este exame ainda não tem imagens disponíveis.");

        var chave = $"imagens-exame/{sol.Solicitacao!.PacienteId}/{studyUid}.pdf";

        var cache = await armazenamento.LerAsync(chave, cancellationToken);
        if (cache is { Length: > 0 })
            return cache;

        var imagens = await imagensReader.ObterImagensAsync(studyUid, MaxImagens, cancellationToken);
        if (imagens.Count == 0)
            throw new ConflitoException("exame.sem_imagens", "Este exame ainda não tem imagens disponíveis no PACS.");

        var paciente = await pacientes.ObterPorIdAsync(sol.Solicitacao!.PacienteId, cancellationToken);

        var capa = new ExameImagensCapa(
            PacienteNome: paciente.NomeCompleto,
            PacienteCpf: paciente.Cpf,
            PacienteCns: paciente.Cns,
            PacienteNascimento: paciente.DataNascimento,
            ExameNome: sol.TipoExame?.Nome ?? "Exame de imagem",
            // RealizadoEm é UTC → converte p/ Brasília na exibição (mesma regra dos outros PDFs).
            RealizadoEm: FusoBrasilia.ParaExibicao(sol.RealizadoEm),
            Unidade: sol.Solicitacao!.UnidadeExecutante?.Nome,
            Descricao: PrimeiroNaoVazio(sol.Solicitacao!.Justificativa, sol.Solicitacao!.Observacoes),
            Anamnese: Resumir(sol.Solicitacao!.Observacoes, sol.Solicitacao!.Justificativa));

        var pdf = new ExameImagensPdfDocument(capa, imagens, ExameRecursos.Logo).Gerar();

        await armazenamento.SalvarAsync(chave, pdf, cancellationToken);
        return pdf;
    }

    public async Task<ReavaliacaoImagens> ReavaliarAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var (studyUid, _) = await ResolverEstudoAsync(solicitacaoExameId, cancellationToken);
        if (studyUid is null) return new ReavaliacaoImagens(null, null, 0, false);

        var cache = await imagensReader.ContarImagensCacheadasAsync(studyUid, cancellationToken);
        var pacs = await imagensReader.ContarInstanciasPacsAsync(studyUid, cancellationToken);
        // Defasado só quando o cache existe e o PACS cresceu além dele (nunca invalida por PACS fora do ar).
        var defasado = cache is int c && pacs > c && pacs > 0;
        return new ReavaliacaoImagens(studyUid, cache, pacs, defasado);
    }

    public async Task InvalidarAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var (studyUid, pacienteId) = await ResolverEstudoAsync(solicitacaoExameId, cancellationToken);
        if (studyUid is null) return;

        // PDF consolidado do cidadão (por paciente) + render-cache do estudo (que o "exame completo" reusa).
        if (pacienteId != Guid.Empty)
            await armazenamento.ExcluirAsync($"imagens-exame/{pacienteId}/{studyUid}.pdf", cancellationToken);
        await imagensReader.InvalidarCacheAsync(studyUid, cancellationToken);
    }

    /// <summary>
    /// Resolve o StudyInstanceUID REAL do exame (associação ativa mais recente; senão o da própria
    /// solicitação) e o paciente — mesma regra do <see cref="GerarOuObterAsync"/>.
    /// </summary>
    private async Task<(string? StudyUid, Guid PacienteId)> ResolverEstudoAsync(Guid solicitacaoExameId, CancellationToken ct)
    {
        var sol = await db.ExamesImagem.AsNoTracking()
            .Where(s => s.Id == solicitacaoExameId && s.ExcluidoEm == null)
            .Select(s => new { s.StudyInstanceUID, PacienteId = s.Solicitacao!.PacienteId })
            .FirstOrDefaultAsync(ct);
        if (sol is null) return (null, Guid.Empty);

        var studyUid = await db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.ExameImagemId == solicitacaoExameId && a.ExcluidoEm == null && a.StudyInstanceUID != "")
            .OrderByDescending(a => a.CriadoEm)
            .Select(a => a.StudyInstanceUID)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(studyUid)) studyUid = sol.StudyInstanceUID;

        return (string.IsNullOrWhiteSpace(studyUid) ? null : studyUid, sol.PacienteId);
    }

    // ---- Helpers de texto da capa ----

    private static string? PrimeiroNaoVazio(params string?[] valores) =>
        valores.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>Resumo curto para a capa (sem IA nesta fase): texto truncado em ~600 caracteres.</summary>
    private static string? Resumir(params string?[] valores)
    {
        var texto = PrimeiroNaoVazio(valores);
        if (string.IsNullOrWhiteSpace(texto)) return null;
        texto = texto.Trim();
        return texto.Length <= 600 ? texto : texto[..600].TrimEnd() + "…";
    }
}
