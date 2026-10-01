using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.DocumentosPaciente;
using SMSMais.Core.Midias.Dtos;
using SMSMais.Core.Pacientes;
using SMSMais.Data.Entities.Enums;
using SMSMais.Core.Identidade;
using SMSMais.Core.Midias;
using SMSMais.Core.Regulacao.Legado;
using SMSMais.Core.Sernit.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Sernit;

/// <summary>
/// Pedidos do SERNIT montados e guardados na NOSSA base, com anexos, esperando autorização para
/// serem enviados. Tudo local — o SERNIT só entra quando o envio for ligado (decisão explícita).
/// Espelho do <c>SerRascunhoService</c>, sem "ambulatório estadual".
/// </summary>
public interface ISernitRascunhoService
{
    Task<IReadOnlyList<SernitRascunhoListaDto>> ListarAsync(
        StatusRascunhoSernit? status, CancellationToken cancellationToken);
    Task<SernitRascunhoDetalheDto> ObterAsync(Guid id, CancellationToken cancellationToken);
    Task<SernitRascunhoDetalheDto> SalvarAsync(
        Guid? id, SernitRascunhoRequest request, CancellationToken cancellationToken);
    Task ExcluirAsync(Guid id, CancellationToken cancellationToken);
    Task<SernitRascunhoDetalheDto> MarcarProntoAsync(Guid id, CancellationToken cancellationToken);
    Task<SernitRascunhoAnexoDto> AnexarAsync(
        Guid id, string nomeArquivo, string? contentType, byte[] conteudo, CancellationToken cancellationToken, string? titulo = null, string? descricao = null);

    /// <summary>Anexa um documento que o paciente (pelo CNS do rascunho) já tem no cadastro.</summary>
    Task<SernitRascunhoAnexoDto> AnexarDoAcervoAsync(Guid id, string chave, CancellationToken cancellationToken);

    /// <summary>Acervo (aceito) do paciente do rascunho; vazio se o CNS não é de paciente cadastrado.</summary>
    Task<IReadOnlyList<ItemAcervoDto>> ListarAcervoAsync(Guid id, CancellationToken cancellationToken);

    Task<ConteudoAcervo> ObterConteudoAcervoAsync(Guid id, string chave, CancellationToken cancellationToken);

    /// <summary>Conteúdo de um anexo do rascunho, para o visualizador.</summary>
    Task<MidiaConteudo> ObterConteudoAnexoAsync(Guid id, Guid anexoId, CancellationToken cancellationToken);

    Task RemoverAnexoAsync(Guid id, Guid anexoId, CancellationToken cancellationToken);
}

public sealed class SernitRascunhoService(
    SmsMaisDbContext db,
    IMidiasService midias,
    IUsuarioAtualAccessor usuarioAtual,
    IRascunhoLegadoGate legado,
    IDocumentosPacienteService? acervo = null,
    IPacientesService? pacientes = null) : ISernitRascunhoService
{
    private const int TamanhoMaximoAnexo = 10 * 1024 * 1024;

    public async Task<IReadOnlyList<SernitRascunhoListaDto>> ListarAsync(
        StatusRascunhoSernit? status, CancellationToken cancellationToken)
    {
        var q = db.SernitSolicitacaoRascunhos.AsNoTracking();
        if (status is { } s) q = q.Where(x => x.Status == s);

        return await q
            .OrderByDescending(x => x.AtualizadoEm ?? x.CriadoEm)
            .Select(x => new SernitRascunhoListaDto(
                x.Id, x.Status, x.Tipo, x.RecursoRotulo, x.PacienteNome, x.Cns, x.Hipotese,
                x.IdSernitGerado, x.CriadoPorNome, x.CriadoEm, x.AtualizadoEm, x.EnviadoEm,
                x.Anexos.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<SernitRascunhoDetalheDto> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        var r = await db.SernitSolicitacaoRascunhos
            .Include(x => x.Anexos)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Rascunho de solicitação do SERNIT", id);

        return ParaDetalhe(r);
    }

    public async Task<SernitRascunhoDetalheDto> SalvarAsync(
        Guid? id, SernitRascunhoRequest request, CancellationToken cancellationToken)
    {
        await legado.GarantirEscritaPermitidaAsync("SERNIT", cancellationToken);

        var r = id is { } existente
            ? await db.SernitSolicitacaoRascunhos
                  .Include(x => x.Anexos)
                  .FirstOrDefaultAsync(x => x.Id == existente, cancellationToken)
              ?? throw new NaoEncontradoException("Rascunho de solicitação do SERNIT", existente)
            : NovoRascunho();

        if (r.Status == StatusRascunhoSernit.Enviado)
        {
            throw new ConflitoException(
                "sernit.rascunho_ja_enviado",
                $"Esta solicitação já foi enviada ao SERNIT (nº {r.IdSernitGerado}) e não pode ser editada.");
        }

        r.Tipo = request.Tipo;
        r.RecursoValor = request.RecursoValor;
        r.RecursoRotulo = request.RecursoRotulo;
        r.Cns = Limpar(request.Cns);
        r.PacienteNome = Limpar(request.PacienteNome);
        r.Hipotese = Limpar(request.Hipotese);
        r.CamposJson = JsonSerializer.Serialize(request.Campos ?? []);
        r.AtualizadoEm = DateTime.UtcNow;

        if (r.Status == StatusRascunhoSernit.Falhou)
        {
            r.Status = StatusRascunhoSernit.Rascunho;
            r.MensagemErro = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ParaDetalhe(r);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken)
    {
        await legado.GarantirEscritaPermitidaAsync("SERNIT", cancellationToken);

        var r = await db.SernitSolicitacaoRascunhos
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Rascunho de solicitação do SERNIT", id);

        if (r.Status == StatusRascunhoSernit.Enviado)
        {
            throw new ConflitoException(
                "sernit.rascunho_ja_enviado",
                $"Esta solicitação já foi enviada ao SERNIT (nº {r.IdSernitGerado}) e não pode ser excluída.");
        }

        db.SernitSolicitacaoRascunhos.Remove(r);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SernitRascunhoDetalheDto> MarcarProntoAsync(Guid id, CancellationToken cancellationToken)
    {
        await legado.GarantirEscritaPermitidaAsync("SERNIT", cancellationToken);

        var r = await db.SernitSolicitacaoRascunhos
            .Include(x => x.Anexos)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Rascunho de solicitação do SERNIT", id);

        var faltando = await FaltandoAsync(r, cancellationToken);
        if (faltando.Count > 0)
        {
            throw new ValidacaoException(
                "sernit.rascunho_incompleto",
                $"Faltam campos obrigatórios: {string.Join(", ", faltando)}.");
        }

        r.Status = StatusRascunhoSernit.Pronto;
        r.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ParaDetalhe(r);
    }

    public async Task<SernitRascunhoAnexoDto> AnexarAsync(
        Guid id, string nomeArquivo, string? contentType, byte[] conteudo, CancellationToken cancellationToken, string? titulo = null, string? descricao = null)
    {
        await legado.GarantirEscritaPermitidaAsync("SERNIT", cancellationToken);

        var r = await db.SernitSolicitacaoRascunhos
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Rascunho de solicitação do SERNIT", id);

        if (conteudo.Length == 0)
        {
            throw new ValidacaoException("sernit.anexo_vazio", "O arquivo está vazio.");
        }

        if (conteudo.Length > TamanhoMaximoAnexo)
        {
            throw new ValidacaoException(
                "sernit.anexo_grande",
                $"O arquivo tem {conteudo.Length / 1024 / 1024} MB e o limite é "
                + $"{TamanhoMaximoAnexo / 1024 / 1024} MB.");
        }

        var midia = await midias.EnviarAsync(
            usuarioAtual.UsuarioId,
            nomeArquivo,
            contentType ?? "application/octet-stream",
            conteudo,
            categoria: "sernit-solicitacao",
            cancellationToken);

        var anexo = new SernitRascunhoAnexo
        {
            Id = Guid.NewGuid(),
            RascunhoId = r.Id,
            MidiaId = midia.Id,
            NomeArquivo = nomeArquivo,
            ContentType = contentType,
            Tamanho = conteudo.Length,
            Titulo = Texto(titulo, 200),
            Descricao = Texto(descricao, 2000),
            CriadoEm = DateTime.UtcNow,
        };

        db.SernitRascunhoAnexos.Add(anexo);
        r.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        // Anexou na solicitação = fica também no cadastro do paciente (quando o CNS é de alguém
        // cadastrado). A cópia é consequência: se falhar, o anexo do rascunho continua valendo.
        if (acervo is not null
            && await AcervoPorCns.ResolverPacienteAsync(pacientes, r.Cns, cancellationToken) is { } pacienteId)
        {
            await acervo.RegistrarCopiaAsync(new NovoDocumentoPaciente(
                pacienteId,
                anexo.Titulo ?? Path.GetFileNameWithoutExtension(nomeArquivo),
                anexo.Descricao,
                nomeArquivo,
                contentType ?? "application/octet-stream",
                conteudo,
                OrigemDocumentoPaciente.Solicitacao,
                $"sernit-solicitacao:{r.Id:D}",
                SituacaoDocumentoPaciente.Aceito), cancellationToken);
        }

        return ParaAnexoDto(anexo);
    }

    public async Task<SernitRascunhoAnexoDto> AnexarDoAcervoAsync(Guid id, string chave, CancellationToken cancellationToken)
    {
        await legado.GarantirEscritaPermitidaAsync("SERNIT", cancellationToken);

        var r = await db.SernitSolicitacaoRascunhos
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Rascunho de solicitação do SERNIT", id);

        var pacienteId = await AcervoPorCns.ExigirPacienteAsync(pacientes, r.Cns, cancellationToken);
        var conteudo = await ExigirAcervo().ObterConteudoPorChaveAsync(pacienteId, chave, cancellationToken);

        // O que o SMSMais produziu (laudo, imagens) pode passar do teto do upload manual; mesmo
        // assim o envio ao sistema externo tem limite — o teto vale aqui também.
        if (conteudo.Conteudo.Length > TamanhoMaximoAnexo)
        {
            throw new ValidacaoException(
                "sernit.anexo_grande",
                $"O documento tem {conteudo.Conteudo.Length / 1024 / 1024} MB e o limite é "
                + $"{TamanhoMaximoAnexo / 1024 / 1024} MB.");
        }

        var midia = await midias.EnviarAsync(
            usuarioAtual.UsuarioId,
            conteudo.NomeArquivo,
            conteudo.MimeType,
            conteudo.Conteudo,
            categoria: "sernit-solicitacao",
            cancellationToken);

        var anexo = new SernitRascunhoAnexo
        {
            Id = Guid.NewGuid(),
            RascunhoId = r.Id,
            MidiaId = midia.Id,
            NomeArquivo = conteudo.NomeArquivo,
            ContentType = conteudo.MimeType,
            Tamanho = conteudo.Conteudo.Length,
            Titulo = Texto(conteudo.Titulo, 200),
            Descricao = Texto(conteudo.Descricao, 2000),
            CriadoEm = DateTime.UtcNow,
        };

        db.SernitRascunhoAnexos.Add(anexo);
        r.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ParaAnexoDto(anexo);
    }

    public async Task<IReadOnlyList<ItemAcervoDto>> ListarAcervoAsync(Guid id, CancellationToken cancellationToken)
    {
        var cns = await db.SernitSolicitacaoRascunhos.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Cns })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NaoEncontradoException("Rascunho de solicitação do SERNIT", id);

        var pacienteId = await AcervoPorCns.ResolverPacienteAsync(pacientes, cns.Cns, cancellationToken);
        return pacienteId is { } p
            ? await ExigirAcervo().ListarAsync(p, incluirPendentes: false, cancellationToken)
            : [];
    }

    public async Task<ConteudoAcervo> ObterConteudoAcervoAsync(Guid id, string chave, CancellationToken cancellationToken)
    {
        var cns = await db.SernitSolicitacaoRascunhos.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => x.Cns)
            .FirstOrDefaultAsync(cancellationToken);
        var pacienteId = await AcervoPorCns.ExigirPacienteAsync(pacientes, cns, cancellationToken);
        return await ExigirAcervo().ObterConteudoPorChaveAsync(pacienteId, chave, cancellationToken);
    }

    public async Task<MidiaConteudo> ObterConteudoAnexoAsync(Guid id, Guid anexoId, CancellationToken cancellationToken)
    {
        var anexo = await db.SernitRascunhoAnexos.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == anexoId && x.RascunhoId == id, cancellationToken)
            ?? throw new NaoEncontradoException("Anexo do rascunho", anexoId);

        var c = await midias.ObterConteudoAsync(anexo.MidiaId, cancellationToken)
            ?? throw new NaoEncontradoException("Conteúdo do anexo", anexoId);
        return new MidiaConteudo(c.Conteudo, anexo.ContentType ?? c.MimeType, anexo.NomeArquivo);
    }

    private IDocumentosPacienteService ExigirAcervo() =>
        acervo ?? throw new InvalidOperationException("Acervo do paciente não registrado.");

    private static string? Texto(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var t = valor.Trim();
        return t.Length > maximo ? t[..maximo] : t;
    }

    private static SernitRascunhoAnexoDto ParaAnexoDto(SernitRascunhoAnexo a) => new(
        a.Id, a.MidiaId, a.NomeArquivo, a.Titulo, a.Descricao, a.ContentType, a.Tamanho, a.EnviadoEm, a.CriadoEm);

    public async Task RemoverAnexoAsync(Guid id, Guid anexoId, CancellationToken cancellationToken)
    {
        await legado.GarantirEscritaPermitidaAsync("SERNIT", cancellationToken);

        var anexo = await db.SernitRascunhoAnexos
            .FirstOrDefaultAsync(x => x.Id == anexoId && x.RascunhoId == id, cancellationToken)
            ?? throw new NaoEncontradoException("Anexo do rascunho", anexoId);

        if (anexo.EnviadoEm is not null)
        {
            throw new ConflitoException(
                "sernit.anexo_ja_enviado",
                "Este anexo já foi enviado ao SERNIT e não pode ser removido daqui.");
        }

        db.SernitRascunhoAnexos.Remove(anexo);
        await db.SaveChangesAsync(cancellationToken);
    }

    private SernitSolicitacaoRascunho NovoRascunho()
    {
        var r = new SernitSolicitacaoRascunho
        {
            Id = Guid.NewGuid(),
            Status = StatusRascunhoSernit.Rascunho,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.SernitSolicitacaoRascunhos.Add(r);
        return r;
    }

    private async Task<List<string>> FaltandoAsync(
        SernitSolicitacaoRascunho r, CancellationToken cancellationToken)
    {
        var faltando = new List<string>();
        if (r.Tipo is null) faltando.Add("Tipo");
        if (string.IsNullOrWhiteSpace(r.RecursoValor)) faltando.Add("Recurso");
        if (string.IsNullOrWhiteSpace(r.Cns)) faltando.Add("CNS do paciente");
        if (string.IsNullOrWhiteSpace(r.Hipotese)) faltando.Add("Hipótese");

        var campos = Ler(r.CamposJson);
        if (!campos.TryGetValue("form0:classificacao_risco", out var risco) || string.IsNullOrWhiteSpace(risco))
        {
            faltando.Add("Classificação de risco");
        }

        if (r.Tipo is { } tipo && !string.IsNullOrWhiteSpace(r.RecursoValor))
        {
            var obrigatorios = await db.SernitCatalogoCampos
                .AsNoTracking()
                .Where(c => c.Recurso!.Tipo == tipo && c.Recurso.Valor == r.RecursoValor && c.Obrigatorio)
                .Select(c => new { c.Campo, c.Rotulo })
                .ToListAsync(cancellationToken);

            faltando.AddRange(
                from c in obrigatorios
                where !campos.TryGetValue(c.Campo, out var v) || string.IsNullOrWhiteSpace(v)
                select c.Rotulo);
        }

        return faltando;
    }

    private static Dictionary<string, string> Ler(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static SernitRascunhoDetalheDto ParaDetalhe(SernitSolicitacaoRascunho r) => new(
        r.Id, r.Status, r.Tipo, r.RecursoValor, r.RecursoRotulo,
        r.Cns, r.PacienteNome, r.Hipotese,
        Ler(r.CamposJson), r.IdSernitGerado, r.MensagemErro, r.CriadoPorNome, r.CriadoEm,
        r.AtualizadoEm, r.EnviadoEm,
        [.. r.Anexos
            .OrderBy(a => a.CriadoEm)
            .Select(ParaAnexoDto)]);

    private static string? Limpar(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
