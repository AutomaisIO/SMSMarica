using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
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
        Guid id, string nomeArquivo, string? contentType, byte[] conteudo, CancellationToken cancellationToken);
    Task RemoverAnexoAsync(Guid id, Guid anexoId, CancellationToken cancellationToken);
}

public sealed class SernitRascunhoService(
    SmsMaisDbContext db,
    IMidiasService midias,
    IUsuarioAtualAccessor usuarioAtual,
    IRascunhoLegadoGate legado) : ISernitRascunhoService
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
        Guid id, string nomeArquivo, string? contentType, byte[] conteudo, CancellationToken cancellationToken)
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
            CriadoEm = DateTime.UtcNow,
        };

        db.SernitRascunhoAnexos.Add(anexo);
        r.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new SernitRascunhoAnexoDto(
            anexo.Id, anexo.MidiaId, anexo.NomeArquivo, anexo.ContentType, anexo.Tamanho,
            anexo.EnviadoEm, anexo.CriadoEm);
    }

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
            .Select(a => new SernitRascunhoAnexoDto(
                a.Id, a.MidiaId, a.NomeArquivo, a.ContentType, a.Tamanho, a.EnviadoEm, a.CriadoEm))]);

    private static string? Limpar(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
