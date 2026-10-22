using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Exames.ImagensInvalidadas;

/// <summary>
/// Retirar e devolver imagens do que vai ao paciente (ver <see cref="ImagemInvalidada"/>).
///
/// <para><b>Quem pode</b> (Rafael, 20/10/2026): retirar — quem lauda (permissão Laudos/Edição) ou
/// quem tem acesso global; devolver — quem retirou ou quem tem acesso global. Vale também DEPOIS
/// de o laudo ser assinado.</para>
///
/// <para><b>O arquivo do paciente é refeito na hora:</b> os caches do exame (PDF do cidadão e
/// render-cache) são apagados e o PDF é regerado já sem a imagem — o app do cidadão e o acervo
/// passam a entregar a versão nova. Falha ao regerar (PACS fora do ar) não desfaz a invalidação:
/// o próximo acesso regera.</para>
/// </summary>
public interface IImagensInvalidadasService
{
    Task<ImagensInvalidadasDoEstudoDto> ListarAsync(
        string studyInstanceUID, Guid usuarioId, CancellationToken ct = default);

    Task<ImagensInvalidadasDoEstudoDto> InvalidarAsync(
        string studyInstanceUID, string sopInstanceUID, Guid usuarioId, string? motivo,
        CancellationToken ct = default);

    Task<ImagensInvalidadasDoEstudoDto> RestaurarAsync(
        string studyInstanceUID, string sopInstanceUID, Guid usuarioId, CancellationToken ct = default);
}

public sealed class ImagensInvalidadasService(
    SmsMaisDbContext db,
    IIdentidadeService identidade,
    IExamePacsImagensReader imagensReader,
    IExameImagensPdfService imagensPdf,
    ILogger<ImagensInvalidadasService> logger) : IImagensInvalidadasService
{
    public async Task<ImagensInvalidadasDoEstudoDto> ListarAsync(
        string studyInstanceUID, Guid usuarioId, CancellationToken ct = default)
    {
        var study = Normalizar(studyInstanceUID, "studyInstanceUID");
        var global = await AcessoGlobalUsuario.TemAsync(db, usuarioId, ct);
        var podeInvalidar = global || await LaudaAsync(usuarioId, ct);

        var imagens = await db.ImagensInvalidadas.AsNoTracking()
            .Where(i => i.StudyInstanceUID == study && i.ExcluidoEm == null)
            .OrderBy(i => i.CriadoEm)
            .Select(i => new ImagemInvalidadaDto(
                i.SopInstanceUID,
                i.Motivo,
                i.CriadoEm,
                i.CriadoPor,
                i.CriadoPorUsuario!.NomeCompleto,
                global || i.CriadoPor == usuarioId))
            .ToListAsync(ct);

        return new ImagensInvalidadasDoEstudoDto(podeInvalidar, imagens);
    }

    public async Task<ImagensInvalidadasDoEstudoDto> InvalidarAsync(
        string studyInstanceUID, string sopInstanceUID, Guid usuarioId, string? motivo,
        CancellationToken ct = default)
    {
        var study = Normalizar(studyInstanceUID, "studyInstanceUID");
        var sop = Normalizar(sopInstanceUID, "sopInstanceUID");

        if (!await AcessoGlobalUsuario.TemAsync(db, usuarioId, ct) && !await LaudaAsync(usuarioId, ct))
            throw new ConflitoException(
                "imagem_invalidada.sem_permissao",
                "Só quem lauda (ou um usuário com acesso global) pode retirar imagens do exame.");

        var vigentes = await db.ImagensInvalidadas
            .Where(i => i.StudyInstanceUID == study && i.ExcluidoEm == null)
            .Select(i => i.SopInstanceUID)
            .ToListAsync(ct);

        // Idempotente: retirar de novo a mesma imagem não cria outra linha.
        if (vigentes.Contains(sop))
            return await ListarAsync(study, usuarioId, ct);

        // Não deixa o paciente sem nenhuma imagem: o PDF sairia vazio. Com o PACS fora do ar a
        // contagem volta 0 e a checagem é pulada — não se bloqueia a médica por isso.
        var total = await imagensReader.ContarInstanciasPacsAsync(study, ct);
        if (total > 0 && vigentes.Count + 1 >= total)
            throw new ConflitoException(
                "imagem_invalidada.ultima_imagem",
                "Esta é a última imagem que vai ao paciente — não dá para retirar todas.");

        db.ImagensInvalidadas.Add(new ImagemInvalidada
        {
            Id = Guid.CreateVersion7(),
            StudyInstanceUID = study,
            SopInstanceUID = sop,
            Motivo = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim(),
            CriadoPor = usuarioId,
            CriadoEm = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "IMAGEM_INVALIDADA: estudo {Study}, imagem {Sop} retirada do que vai ao paciente por {Usuario}.",
            study, sop, usuarioId);

        await RefazerArquivosDoPacienteAsync(study, ct);
        return await ListarAsync(study, usuarioId, ct);
    }

    public async Task<ImagensInvalidadasDoEstudoDto> RestaurarAsync(
        string studyInstanceUID, string sopInstanceUID, Guid usuarioId, CancellationToken ct = default)
    {
        var study = Normalizar(studyInstanceUID, "studyInstanceUID");
        var sop = Normalizar(sopInstanceUID, "sopInstanceUID");

        var invalidacao = await db.ImagensInvalidadas
            .FirstOrDefaultAsync(i => i.StudyInstanceUID == study && i.SopInstanceUID == sop && i.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(ImagemInvalidada), $"{study}/{sop}");

        if (invalidacao.CriadoPor != usuarioId && !await AcessoGlobalUsuario.TemAsync(db, usuarioId, ct))
            throw new ConflitoException(
                "imagem_invalidada.restaurar_sem_permissao",
                "Só quem retirou a imagem (ou um usuário com acesso global) pode devolvê-la.");

        invalidacao.ExcluidoEm = DateTime.UtcNow;
        invalidacao.ExcluidoPor = usuarioId;
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "IMAGEM_RESTAURADA: estudo {Study}, imagem {Sop} volta ao que vai ao paciente por {Usuario}.",
            study, sop, usuarioId);

        await RefazerArquivosDoPacienteAsync(study, ct);
        return await ListarAsync(study, usuarioId, ct);
    }

    /// <summary>
    /// Troca o arquivo que o paciente recebe: apaga os caches de cada exame ligado ao estudo (pela
    /// worklist ou pela associação) e regera o PDF das imagens já com o filtro novo. O "exame
    /// completo" não tem cache próprio — sai certo no próximo download.
    /// </summary>
    private async Task RefazerArquivosDoPacienteAsync(string study, CancellationToken ct)
    {
        var pelaWorklist = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.ExcluidoEm == null && e.StudyInstanceUID == study)
            .Select(e => e.Id)
            .ToListAsync(ct);
        var pelaAssociacao = await db.ExameAssociacoes.AsNoTracking()
            .Where(a => a.ExcluidoEm == null && a.StudyInstanceUID == study)
            .Select(a => a.ExameImagemId)
            .ToListAsync(ct);
        var exames = pelaWorklist.Concat(pelaAssociacao).Distinct().ToList();

        if (exames.Count == 0)
        {
            // Estudo sem pedido ligado: não há PDF de paciente, só o render-cache.
            await imagensReader.InvalidarCacheAsync(study, ct);
            return;
        }

        foreach (var exameId in exames)
        {
            await imagensPdf.InvalidarAsync(exameId, ct);
            try
            {
                await imagensPdf.GerarOuObterAsync(exameId, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex,
                    "Imagens do estudo {Study} mudaram, mas o PDF do exame {Exame} não foi regerado agora — "
                    + "sai no próximo acesso.", study, exameId);
            }
        }
    }

    private async Task<bool> LaudaAsync(Guid usuarioId, CancellationToken ct)
    {
        try
        {
            var permissoes = await identidade.ObterPermissoesResolvidasAsync(usuarioId, ct);
            var laudos = permissoes.Resolvidas.FirstOrDefault(p => p.Modulo == ModuloPermissao.Laudos);
            return laudos is not null && (laudos.Acoes & AcoesPermissao.Edicao) == AcoesPermissao.Edicao;
        }
        catch (NaoEncontradoException)
        {
            return false;
        }
    }

    private static string Normalizar(string valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ValidacaoException(campo, $"{campo} é obrigatório.");
        return valor.Trim();
    }
}
