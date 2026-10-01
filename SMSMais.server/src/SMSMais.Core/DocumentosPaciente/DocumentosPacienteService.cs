using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Armazenamento;
using SMSMais.Core.Cidadao;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Exames;
using SMSMais.Core.Identidade;
using SMSMais.Core.Laudos.Assinatura;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.DocumentosPaciente;

/// <summary>
/// Acervo do paciente: o que fica perene no cadastro, em "Exames anexados".
///
/// <para>Junta numa lista só o que hoje mora em lugares diferentes — documentos do acervo, PDFs
/// da anamnese (QR), laudos <b>assinados</b> e o PDF das imagens dos exames — e é dessa lista
/// que a solicitação escolhe o que anexar. Só o que está <see cref="SituacaoDocumentoPaciente.Aceito"/>
/// pode ser anexado: o que o paciente mandou espera alguém da equipe conferir.</para>
/// </summary>
public interface IDocumentosPacienteService
{
    /// <summary>Teto de documentos esperando conferência por paciente — trava quem manda arquivo em massa.</summary>
    const int LimitePendentes = 10;

    Task<IReadOnlyList<ItemAcervoDto>> ListarAsync(
        Guid pacienteId, bool incluirPendentes, CancellationToken ct = default);

    /// <summary>Conteúdo de um item, conferindo que é do paciente. Pendente só com <paramref name="permitirPendente"/>.</summary>
    Task<ConteudoAcervo> ObterConteudoAsync(
        Guid pacienteId, TipoItemAcervo tipo, Guid id, bool permitirPendente, CancellationToken ct = default);

    /// <summary>O mesmo, pela chave <c>tipo:id</c> do <see cref="ItemAcervoDto"/>. Nunca devolve pendente.</summary>
    Task<ConteudoAcervo> ObterConteudoPorChaveAsync(Guid pacienteId, string chave, CancellationToken ct = default);

    /// <summary>
    /// Põe um documento no acervo. Se o paciente já tem o mesmo arquivo (mesmo hash), devolve o
    /// existente — e, se este chega aceito e aquele estava pendente, o existente passa a aceito.
    /// </summary>
    Task<ItemAcervoDto> AdicionarAsync(NovoDocumentoPaciente novo, CancellationToken ct = default);

    /// <summary>
    /// <see cref="AdicionarAsync"/> que não derruba quem chamou: a solicitação anexa o arquivo de
    /// qualquer jeito; a cópia no cadastro é consequência, e a falha dela fica no log.
    /// </summary>
    Task RegistrarCopiaAsync(NovoDocumentoPaciente novo, CancellationToken ct = default);

    Task<ItemAcervoDto> EditarAsync(
        Guid pacienteId, Guid documentoId, EditarDocumentoPacienteRequest req, CancellationToken ct = default);

    /// <summary>Pendente → aceito (com a chance de corrigir título e descrição).</summary>
    Task<ItemAcervoDto> AceitarAsync(
        Guid pacienteId, Guid documentoId, EditarDocumentoPacienteRequest? req, CancellationToken ct = default);

    Task ExcluirAsync(Guid pacienteId, Guid documentoId, CancellationToken ct = default);

    /// <summary>
    /// O paciente retira, pelo app, um documento que ele mesmo enviou e que ninguém conferiu ainda.
    /// Depois de aceito, o documento é do cadastro — só a equipe tira.
    /// </summary>
    Task RetirarEnvioDoPacienteAsync(Guid pacienteId, Guid documentoId, CancellationToken ct = default);

    Task<int> ContarPendentesAsync(Guid pacienteId, CancellationToken ct = default);

    /// <summary>
    /// Anexa um item do acervo na anamnese de um exame de imagem (vira <c>documento_exame</c> já
    /// salvo). Fica aqui, e não no serviço de anexos, porque o acervo já depende dele.
    /// </summary>
    Task<Guid> AnexarNaAnamneseAsync(Guid exameImagemId, string chave, CancellationToken ct = default);

    /// <summary>Paciente dono de um exame de imagem (para a anamnese abrir o acervo dele).</summary>
    Task<Guid> PacienteDoExameImagemAsync(Guid exameImagemId, CancellationToken ct = default);
}

/// <inheritdoc cref="IDocumentosPacienteService"/>
public sealed class DocumentosPacienteService(
    SmsMaisDbContext db,
    IArmazenamentoArquivos armazenamento,
    ICidadaoClinicoService clinico,
    ILaudoAssinaturaService assinatura,
    IExameImagensPdfService imagensPdf,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<DocumentosPacienteService> logger) : IDocumentosPacienteService
{
    /// <summary>25 MB — mesmo teto do PDF digitalizado pelo PWA de arquivos.</summary>
    public const long TamanhoMaximoBytes = 25 * 1024 * 1024;

    private const string MimePdf = "application/pdf";

    /// <summary>O que o acervo aceita: PDF e as imagens que o navegador mostra sem plugin.</summary>
    public static readonly IReadOnlySet<string> TiposAceitos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        MimePdf, "image/jpeg", "image/png", "image/webp", "image/gif",
    };

    public async Task<IReadOnlyList<ItemAcervoDto>> ListarAsync(
        Guid pacienteId, bool incluirPendentes, CancellationToken ct = default)
    {
        var itens = new List<ItemAcervoDto>();
        var hashes = new HashSet<string>(StringComparer.Ordinal);

        // 1) Acervo propriamente dito.
        var docs = await db.DocumentosPaciente.AsNoTracking()
            .Where(d => d.PacienteId == pacienteId && d.ExcluidoEm == null
                && (incluirPendentes || d.Situacao == SituacaoDocumentoPaciente.Aceito))
            .OrderByDescending(d => d.CriadoEm)
            .ToListAsync(ct);
        foreach (var d in docs)
        {
            hashes.Add(d.HashSha256);
            itens.Add(Mapear(d));
        }

        // 2) PDFs da anamnese (QR). O mesmo arquivo pode já estar no acervo — mostra uma vez só.
        var anexos = await (
            from d in db.DocumentosExame.AsNoTracking()
            join e in db.ExamesImagem.AsNoTracking() on d.ExameImagemId equals e.Id
            where e.Solicitacao!.PacienteId == pacienteId && e.ExcluidoEm == null
                  && d.ExcluidoEm == null && d.Status == StatusDocumentoExame.Salvo
            orderby d.CriadoEm descending
            select d).ToListAsync(ct);
        foreach (var d in anexos)
        {
            if (!hashes.Add(d.HashSha256)) continue;
            itens.Add(new ItemAcervoDto(
                Chave(TipoItemAcervo.AnexoExame, d.Id), TipoItemAcervo.AnexoExame, d.Id,
                d.Nome, d.Descricao, d.MimeType, d.TamanhoBytes, d.Paginas, d.CriadoEm,
                "Anamnese", SituacaoDocumentoPaciente.Aceito, Editavel: false));
        }

        // 3) Exames de imagem: o PDF das imagens e o laudo assinado (a lista do app do cidadão
        //    já resolve o study efetivo e a assinatura — mesma regra nos dois lados).
        var exames = await clinico.ListarExamesAsync(pacienteId, ct);
        var laudosVistos = new HashSet<Guid>();
        foreach (var e in exames)
        {
            if (e.LaudoId is { } laudoId && e.LaudoAssinado && laudosVistos.Add(laudoId))
            {
                itens.Add(new ItemAcervoDto(
                    Chave(TipoItemAcervo.Laudo, laudoId), TipoItemAcervo.Laudo, laudoId,
                    $"Laudo — {e.Nome}", "Laudo assinado digitalmente", MimePdf, null, null, e.Data,
                    "Laudo", SituacaoDocumentoPaciente.Aceito, Editavel: false));
            }
            if (e.TemImagens)
            {
                itens.Add(new ItemAcervoDto(
                    Chave(TipoItemAcervo.ImagensExame, e.Id), TipoItemAcervo.ImagensExame, e.Id,
                    $"Imagens — {e.Nome}", "PDF com as imagens do exame", MimePdf, null, null, e.Data,
                    "Exame de imagem", SituacaoDocumentoPaciente.Aceito, Editavel: false));
            }
        }

        // 4) Laudos assinados do paciente que não casaram com nenhum exame da lista acima.
        foreach (var l in await clinico.ListarLaudosAsync(pacienteId, ct))
        {
            if (!laudosVistos.Add(l.Id)) continue;
            itens.Add(new ItemAcervoDto(
                Chave(TipoItemAcervo.Laudo, l.Id), TipoItemAcervo.Laudo, l.Id,
                string.IsNullOrWhiteSpace(l.Titulo) ? "Laudo" : $"Laudo — {l.Titulo}",
                "Laudo assinado digitalmente", MimePdf, null, null, l.Data,
                "Laudo", SituacaoDocumentoPaciente.Aceito, Editavel: false));
        }

        return [.. itens.OrderByDescending(i => i.Data)];
    }

    public async Task<ConteudoAcervo> ObterConteudoAsync(
        Guid pacienteId, TipoItemAcervo tipo, Guid id, bool permitirPendente, CancellationToken ct = default)
    {
        switch (tipo)
        {
            case TipoItemAcervo.Documento:
            {
                var d = await db.DocumentosPaciente.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id && x.PacienteId == pacienteId && x.ExcluidoEm == null, ct)
                    ?? throw new NaoEncontradoException("Documento do paciente", id);
                if (!permitirPendente && d.Situacao != SituacaoDocumentoPaciente.Aceito)
                {
                    throw new ConflitoException(
                        "documento.pendente", "Este documento ainda não foi aceito por ninguém da equipe.");
                }
                var bytes = await armazenamento.LerAsync(d.ChaveArmazenamento, ct)
                    ?? throw new NaoEncontradoException("Conteúdo do documento", id);
                return new ConteudoAcervo(bytes, d.MimeType, d.NomeArquivo, d.Titulo, d.Descricao);
            }
            case TipoItemAcervo.AnexoExame:
            {
                var d = await (
                    from x in db.DocumentosExame.AsNoTracking()
                    join e in db.ExamesImagem.AsNoTracking() on x.ExameImagemId equals e.Id
                    where x.Id == id && x.ExcluidoEm == null && x.Status == StatusDocumentoExame.Salvo
                          && e.Solicitacao!.PacienteId == pacienteId && e.ExcluidoEm == null
                    select x).FirstOrDefaultAsync(ct)
                    ?? throw new NaoEncontradoException("Anexo da anamnese", id);
                var bytes = await armazenamento.LerAsync(d.ChaveArmazenamento, ct)
                    ?? throw new NaoEncontradoException("Conteúdo do anexo", id);
                return new ConteudoAcervo(bytes, d.MimeType, NomeArquivo(d.Nome, d.MimeType), d.Nome, d.Descricao);
            }
            case TipoItemAcervo.Laudo:
            {
                var laudo = await db.Laudos.AsNoTracking()
                    .Where(l => l.Id == id && !l.Excluido)
                    .Select(l => new { l.PacienteId, l.Titulo })
                    .FirstOrDefaultAsync(ct);
                if (laudo?.PacienteId != pacienteId || !await assinatura.EstaAssinadoAsync(id, ct))
                {
                    throw new NaoEncontradoException("Laudo assinado", id);
                }
                var pdf = await assinatura.ObterPdfParaDownloadAsync(id, ct);
                var titulo = string.IsNullOrWhiteSpace(laudo.Titulo) ? "Laudo" : $"Laudo — {laudo.Titulo}";
                return new ConteudoAcervo(pdf.Conteudo, MimePdf, NomeArquivo(titulo, MimePdf), titulo, null);
            }
            case TipoItemAcervo.ImagensExame:
            {
                var exame = await db.ExamesImagem.AsNoTracking()
                    .Where(e => e.Id == id && e.Solicitacao!.PacienteId == pacienteId && e.ExcluidoEm == null)
                    .Select(e => new { Nome = e.TipoExame != null ? e.TipoExame.Nome : "Exame de imagem" })
                    .FirstOrDefaultAsync(ct)
                    ?? throw new NaoEncontradoException("Exame de imagem", id);
                var pdf = await imagensPdf.GerarOuObterAsync(id, ct);
                var titulo = $"Imagens — {exame.Nome}";
                return new ConteudoAcervo(pdf, MimePdf, NomeArquivo(titulo, MimePdf), titulo, null);
            }
            default:
                throw new ValidacaoException("acervo.tipo", "Tipo de item do acervo desconhecido.");
        }
    }

    public Task<ConteudoAcervo> ObterConteudoPorChaveAsync(Guid pacienteId, string chave, CancellationToken ct = default)
    {
        var (tipo, id) = LerChave(chave);
        return ObterConteudoAsync(pacienteId, tipo, id, permitirPendente: false, ct);
    }

    public async Task<ItemAcervoDto> AdicionarAsync(NovoDocumentoPaciente novo, CancellationToken ct = default)
    {
        var mime = (novo.MimeType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        if (novo.Conteudo is null || novo.Conteudo.Length == 0)
        {
            throw new ValidacaoException("documento.vazio", "Arquivo vazio.");
        }
        if (novo.Conteudo.LongLength > TamanhoMaximoBytes)
        {
            throw new ValidacaoException("documento.muito_grande",
                $"Arquivo acima do limite de {TamanhoMaximoBytes / (1024 * 1024)} MB.");
        }
        if (!TiposAceitos.Contains(mime))
        {
            throw new ValidacaoException("documento.tipo_invalido", "Só são aceitos PDF e imagens (JPG, PNG, WEBP, GIF).");
        }

        var titulo = (novo.Titulo ?? string.Empty).Trim();
        if (titulo.Length == 0)
        {
            throw new ValidacaoException("documento.titulo_obrigatorio", "Informe o nome do documento.");
        }
        if (titulo.Length > 200)
        {
            throw new ValidacaoException("documento.titulo_grande", "O nome deve ter no máximo 200 caracteres.");
        }
        var descricao = string.IsNullOrWhiteSpace(novo.Descricao) ? null : novo.Descricao.Trim();
        if (descricao is { Length: > 2000 })
        {
            throw new ValidacaoException("documento.descricao_grande", "A descrição deve ter no máximo 2000 caracteres.");
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(novo.Conteudo));
        var agora = DateTime.UtcNow;

        var existente = await db.DocumentosPaciente
            .FirstOrDefaultAsync(d => d.PacienteId == novo.PacienteId && d.HashSha256 == hash && d.ExcluidoEm == null, ct);
        if (existente is not null)
        {
            if (novo.Situacao == SituacaoDocumentoPaciente.Aceito
                && existente.Situacao == SituacaoDocumentoPaciente.Pendente)
            {
                existente.Situacao = SituacaoDocumentoPaciente.Aceito;
                existente.AceitoEm = agora;
                existente.AceitoPor = usuarioAtual.UsuarioId;
                await db.SaveChangesAsync(ct);
            }
            return Mapear(existente);
        }

        // Quem vem de fora (app do paciente) entra pendente — e o que ninguém conferiu tem teto.
        if (novo.Situacao == SituacaoDocumentoPaciente.Pendente
            && await ContarPendentesAsync(novo.PacienteId, ct) >= IDocumentosPacienteService.LimitePendentes)
        {
            throw new ConflitoException(
                "documento.limite_pendentes",
                $"Já há {IDocumentosPacienteService.LimitePendentes} documentos esperando a equipe conferir. "
                + "Aguarde a conferência antes de enviar outros.");
        }

        var id = Guid.CreateVersion7();
        var chave = armazenamento.MontarChaveDocumento(novo.PacienteId, id, Extensao(mime));
        // Arquivo antes da linha: se o armazenamento falhar, não fica registro apontando para o nada.
        await armazenamento.SalvarAsync(chave, novo.Conteudo, ct);

        var aceito = novo.Situacao == SituacaoDocumentoPaciente.Aceito;
        var doc = new DocumentoPaciente
        {
            Id = id,
            PacienteId = novo.PacienteId,
            Titulo = titulo,
            Descricao = descricao,
            NomeArquivo = LimparNomeArquivo(novo.NomeArquivo, titulo, mime),
            MimeType = mime,
            TamanhoBytes = novo.Conteudo.LongLength,
            HashSha256 = hash,
            ChaveArmazenamento = chave,
            Origem = novo.Origem,
            OrigemReferencia = novo.OrigemReferencia is { Length: > 120 } r ? r[..120] : novo.OrigemReferencia,
            Situacao = novo.Situacao,
            AceitoEm = aceito ? agora : null,
            AceitoPor = aceito ? usuarioAtual.UsuarioId : null,
            CriadoEm = agora,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.DocumentosPaciente.Add(doc);
        await db.SaveChangesAsync(ct);
        return Mapear(doc);
    }

    public async Task RegistrarCopiaAsync(NovoDocumentoPaciente novo, CancellationToken ct = default)
    {
        try
        {
            await AdicionarAsync(novo, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Acervo: não foi possível guardar a cópia de {Origem} ({Referencia}) no cadastro do paciente {PacienteId}",
                novo.Origem, novo.OrigemReferencia, novo.PacienteId);
        }
    }

    public async Task<ItemAcervoDto> EditarAsync(
        Guid pacienteId, Guid documentoId, EditarDocumentoPacienteRequest req, CancellationToken ct = default)
    {
        var doc = await ExigirAsync(pacienteId, documentoId, ct);
        AplicarTexto(doc, req);
        doc.AtualizadoEm = DateTime.UtcNow;
        doc.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
        return Mapear(doc);
    }

    public async Task<ItemAcervoDto> AceitarAsync(
        Guid pacienteId, Guid documentoId, EditarDocumentoPacienteRequest? req, CancellationToken ct = default)
    {
        var doc = await ExigirAsync(pacienteId, documentoId, ct);
        if (req is not null) AplicarTexto(doc, req);
        var agora = DateTime.UtcNow;
        if (doc.Situacao != SituacaoDocumentoPaciente.Aceito)
        {
            doc.Situacao = SituacaoDocumentoPaciente.Aceito;
            doc.AceitoEm = agora;
            doc.AceitoPor = usuarioAtual.UsuarioId;
        }
        doc.AtualizadoEm = agora;
        doc.AtualizadoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
        return Mapear(doc);
    }

    public async Task ExcluirAsync(Guid pacienteId, Guid documentoId, CancellationToken ct = default)
    {
        var doc = await ExigirAsync(pacienteId, documentoId, ct);

        // O registro fica (auditoria); o arquivo sai do armazenamento — é dado de paciente, e as
        // solicitações que usaram este documento guardaram a própria cópia.
        await armazenamento.ExcluirAsync(doc.ChaveArmazenamento, ct);
        doc.ExcluidoEm = DateTime.UtcNow;
        doc.ExcluidoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    public async Task RetirarEnvioDoPacienteAsync(Guid pacienteId, Guid documentoId, CancellationToken ct = default)
    {
        var doc = await ExigirAsync(pacienteId, documentoId, ct);
        if (doc.Origem != OrigemDocumentoPaciente.AppCidadao || doc.Situacao != SituacaoDocumentoPaciente.Pendente)
        {
            throw new ConflitoException(
                "documento.nao_retiravel", "Só dá para retirar o que você enviou e ainda está em conferência.");
        }
        await armazenamento.ExcluirAsync(doc.ChaveArmazenamento, ct);
        doc.ExcluidoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<int> ContarPendentesAsync(Guid pacienteId, CancellationToken ct = default) =>
        db.DocumentosPaciente.CountAsync(
            d => d.PacienteId == pacienteId && d.ExcluidoEm == null
                && d.Situacao == SituacaoDocumentoPaciente.Pendente, ct);

    public async Task<Guid> PacienteDoExameImagemAsync(Guid exameImagemId, CancellationToken ct = default) =>
        await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == exameImagemId && e.ExcluidoEm == null)
            .Select(e => (Guid?)e.Solicitacao!.PacienteId)
            .FirstOrDefaultAsync(ct)
        ?? throw new NaoEncontradoException(nameof(ExameImagem), exameImagemId);

    public async Task<Guid> AnexarNaAnamneseAsync(Guid exameImagemId, string chave, CancellationToken ct = default)
    {
        var exame = await db.ExamesImagem.AsNoTracking()
            .Where(e => e.Id == exameImagemId && e.ExcluidoEm == null)
            .Select(e => new { e.Id, e.Solicitacao!.PacienteId })
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), exameImagemId);

        var conteudo = await ObterConteudoPorChaveAsync(exame.PacienteId, chave, ct);
        var hash = Convert.ToHexStringLower(SHA256.HashData(conteudo.Conteudo));

        var jaTem = await db.DocumentosExame.AsNoTracking()
            .Where(d => d.ExameImagemId == exame.Id && d.HashSha256 == hash && d.ExcluidoEm == null)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(ct);
        if (jaTem is { } existente) return existente;

        var id = Guid.CreateVersion7();
        var chaveArmazenamento = armazenamento.MontarChaveDocumento(exame.PacienteId, id, Extensao(conteudo.MimeType));
        await armazenamento.SalvarAsync(chaveArmazenamento, conteudo.Conteudo, ct);

        db.DocumentosExame.Add(new DocumentoExame
        {
            Id = id,
            ExameImagemId = exame.Id,
            Nome = conteudo.Titulo.Length > 200 ? conteudo.Titulo[..200] : conteudo.Titulo,
            Descricao = conteudo.Descricao,
            MimeType = conteudo.MimeType,
            TamanhoBytes = conteudo.Conteudo.LongLength,
            HashSha256 = hash,
            ChaveArmazenamento = chaveArmazenamento,
            // Veio do cadastro, que já é conferido: entra salvo, sem a revisão do QR.
            Status = StatusDocumentoExame.Salvo,
            Origem = "acervo",
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        });
        await db.SaveChangesAsync(ct);
        return id;
    }

    // ---------------------------------------------------------------- apoio

    public static string Chave(TipoItemAcervo tipo, Guid id) => $"{tipo.ToString().ToLowerInvariant()}:{id:D}";

    public static (TipoItemAcervo Tipo, Guid Id) LerChave(string chave)
    {
        var partes = (chave ?? string.Empty).Split(':', 2);
        if (partes.Length == 2
            && Enum.TryParse<TipoItemAcervo>(partes[0], ignoreCase: true, out var tipo)
            && Enum.IsDefined(tipo)
            && Guid.TryParse(partes[1], out var id))
        {
            return (tipo, id);
        }
        throw new ValidacaoException("acervo.chave", "Item do cadastro inválido.");
    }

    /// <summary>Extensão do arquivo a partir do Content-Type.</summary>
    public static string Extensao(string mime) => (mime ?? string.Empty).ToLowerInvariant() switch
    {
        "application/pdf" => "pdf",
        "image/jpeg" => "jpg",
        "image/png" => "png",
        "image/webp" => "webp",
        "image/gif" => "gif",
        _ => "bin",
    };

    /// <summary>Nome de arquivo a partir de um título, com a extensão certa para o tipo.</summary>
    public static string NomeArquivo(string titulo, string mime)
    {
        var limpo = new string([.. (titulo ?? string.Empty).Where(c => !Path.GetInvalidFileNameChars().Contains(c))]).Trim();
        if (limpo.Length == 0) limpo = "documento";
        if (limpo.Length > 180) limpo = limpo[..180];
        var ext = "." + Extensao(mime);
        return limpo.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? limpo : limpo + ext;
    }

    private static string LimparNomeArquivo(string? nomeArquivo, string titulo, string mime)
    {
        var nome = Path.GetFileName(nomeArquivo ?? string.Empty).Trim();
        return string.IsNullOrEmpty(nome) ? NomeArquivo(titulo, mime) : (nome.Length > 255 ? nome[^255..] : nome);
    }

    private static void AplicarTexto(DocumentoPaciente doc, EditarDocumentoPacienteRequest req)
    {
        var titulo = (req.Titulo ?? string.Empty).Trim();
        if (titulo.Length == 0)
        {
            throw new ValidacaoException("documento.titulo_obrigatorio", "Informe o nome do documento.");
        }
        if (titulo.Length > 200)
        {
            throw new ValidacaoException("documento.titulo_grande", "O nome deve ter no máximo 200 caracteres.");
        }
        var descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim();
        if (descricao is { Length: > 2000 })
        {
            throw new ValidacaoException("documento.descricao_grande", "A descrição deve ter no máximo 2000 caracteres.");
        }
        doc.Titulo = titulo;
        doc.Descricao = descricao;
    }

    private async Task<DocumentoPaciente> ExigirAsync(Guid pacienteId, Guid documentoId, CancellationToken ct) =>
        await db.DocumentosPaciente
            .FirstOrDefaultAsync(d => d.Id == documentoId && d.PacienteId == pacienteId && d.ExcluidoEm == null, ct)
        ?? throw new NaoEncontradoException("Documento do paciente", documentoId);

    private static ItemAcervoDto Mapear(DocumentoPaciente d) => new(
        Chave(TipoItemAcervo.Documento, d.Id), TipoItemAcervo.Documento, d.Id,
        d.Titulo, d.Descricao, d.MimeType, d.TamanhoBytes, null, d.CriadoEm,
        RotuloOrigem(d.Origem), d.Situacao, Editavel: true);

    private static string RotuloOrigem(OrigemDocumentoPaciente o) => o switch
    {
        OrigemDocumentoPaciente.Painel => "Cadastro",
        OrigemDocumentoPaciente.Solicitacao => "Solicitação",
        OrigemDocumentoPaciente.WhatsApp => "WhatsApp",
        OrigemDocumentoPaciente.AppCidadao => "Enviado pelo paciente",
        _ => "—",
    };
}
