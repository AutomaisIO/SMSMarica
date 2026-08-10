using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Midias;
using SMSMarica.Core.Ser.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Ser;

namespace SMSMarica.Core.Ser;

/// <summary>
/// Pedidos do SER montados e guardados na NOSSA base, com os anexos, esperando autorização para
/// serem enviados.
///
/// <para><b>O que isso resolve:</b> antes, preencher o formulário e sair da tela perdia tudo —
/// não dava para preparar hoje e disparar amanhã, nem montar um lote. Agora o pedido existe aqui
/// como objeto, com status próprio, e o envio é um passo separado e explícito.</para>
///
/// <para><b>Nada aqui fala com o SER.</b> É tudo local, de propósito: o SER só entra quando o
/// envio for ligado — e ligar é decisão explícita, não efeito colateral de salvar.</para>
/// </summary>
public interface ISerRascunhoService
{
    Task<IReadOnlyList<SerRascunhoListaDto>> ListarAsync(
        StatusRascunhoSer? status, CancellationToken cancellationToken);

    Task<SerRascunhoDetalheDto> ObterAsync(Guid id, CancellationToken cancellationToken);

    Task<SerRascunhoDetalheDto> SalvarAsync(
        Guid? id, SerRascunhoRequest request, CancellationToken cancellationToken);

    Task ExcluirAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Marca como pronto para envio. Recusa se faltar campo obrigatório — deixar um
    /// pedido incompleto na fila de envio só adiaria a recusa do SER.</summary>
    Task<SerRascunhoDetalheDto> MarcarProntoAsync(Guid id, CancellationToken cancellationToken);

    Task<SerRascunhoAnexoDto> AnexarAsync(
        Guid id, string nomeArquivo, string? contentType, byte[] conteudo,
        CancellationToken cancellationToken);

    Task RemoverAnexoAsync(Guid id, Guid anexoId, CancellationToken cancellationToken);
}

public sealed class SerRascunhoService(
    SmsMaricaDbContext db,
    IMidiasService midias,
    IUsuarioAtualAccessor usuarioAtual) : ISerRascunhoService
{
    /// <summary>10 MB. O SER não publica limite; este é o nosso, para um anexo absurdo não virar
    /// uma linha gigante no banco e um envio que nunca termina.</summary>
    private const int TamanhoMaximoAnexo = 10 * 1024 * 1024;

    public async Task<IReadOnlyList<SerRascunhoListaDto>> ListarAsync(
        StatusRascunhoSer? status, CancellationToken cancellationToken)
    {
        var q = db.SerSolicitacaoRascunhos.AsNoTracking();
        if (status is { } s) q = q.Where(x => x.Status == s);

        return await q
            .OrderByDescending(x => x.AtualizadoEm ?? x.CriadoEm)
            .Select(x => new SerRascunhoListaDto(
                x.Id, x.Status, x.Tipo, x.RecursoRotulo, x.PacienteNome, x.Cns, x.Hipotese,
                x.IdSerGerado, x.CriadoPorNome, x.CriadoEm, x.AtualizadoEm, x.EnviadoEm,
                x.Anexos.Count))
            .ToListAsync(cancellationToken);
    }

    public async Task<SerRascunhoDetalheDto> ObterAsync(Guid id, CancellationToken cancellationToken)
    {
        var r = await db.SerSolicitacaoRascunhos
            .Include(x => x.Anexos)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Rascunho de solicitação do SER", id);

        return ParaDetalhe(r);
    }

    public async Task<SerRascunhoDetalheDto> SalvarAsync(
        Guid? id, SerRascunhoRequest request, CancellationToken cancellationToken)
    {
        var r = id is { } existente
            ? await db.SerSolicitacaoRascunhos
                  .Include(x => x.Anexos)
                  .FirstOrDefaultAsync(x => x.Id == existente, cancellationToken)
              ?? throw new NaoEncontradoException("Rascunho de solicitação do SER", existente)
            : NovoRascunho();

        if (r.Status == StatusRascunhoSer.Enviado)
        {
            // Já existe no SER: editar aqui daria a impressão de estar corrigindo lá.
            throw new ConflitoException(
                "ser.rascunho_ja_enviado",
                $"Esta solicitação já foi enviada ao SER (nº {r.IdSerGerado}) e não pode ser editada.");
        }

        r.Tipo = request.Tipo;
        r.RecursoValor = request.RecursoValor;
        r.RecursoRotulo = request.RecursoRotulo;
        r.Cns = Limpar(request.Cns);
        r.PacienteNome = Limpar(request.PacienteNome);
        r.Hipotese = Limpar(request.Hipotese);
        r.CamposJson = JsonSerializer.Serialize(request.Campos ?? []);
        r.AtualizadoEm = DateTime.UtcNow;

        // Editar um rascunho que tinha falhado volta para Rascunho: a mensagem de erro anterior
        // deixou de valer, e mantê-la como "Falhou" esconderia que houve correção.
        if (r.Status == StatusRascunhoSer.Falhou)
        {
            r.Status = StatusRascunhoSer.Rascunho;
            r.MensagemErro = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ParaDetalhe(r);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken)
    {
        var r = await db.SerSolicitacaoRascunhos
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Rascunho de solicitação do SER", id);

        if (r.Status == StatusRascunhoSer.Enviado)
        {
            // Apagar o registro do que foi enviado apagaria a prova de que enviamos.
            throw new ConflitoException(
                "ser.rascunho_ja_enviado",
                $"Esta solicitação já foi enviada ao SER (nº {r.IdSerGerado}) e não pode ser excluída.");
        }

        db.SerSolicitacaoRascunhos.Remove(r);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SerRascunhoDetalheDto> MarcarProntoAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var r = await db.SerSolicitacaoRascunhos
            .Include(x => x.Anexos)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Rascunho de solicitação do SER", id);

        var faltando = await FaltandoAsync(r, cancellationToken);
        if (faltando.Count > 0)
        {
            throw new ValidacaoException(
                "ser.rascunho_incompleto",
                $"Faltam campos obrigatórios: {string.Join(", ", faltando)}.");
        }

        r.Status = StatusRascunhoSer.Pronto;
        r.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ParaDetalhe(r);
    }

    // ------------------------------------------------------------------ anexos

    public async Task<SerRascunhoAnexoDto> AnexarAsync(
        Guid id, string nomeArquivo, string? contentType, byte[] conteudo,
        CancellationToken cancellationToken)
    {
        var r = await db.SerSolicitacaoRascunhos
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException("Rascunho de solicitação do SER", id);

        if (conteudo.Length == 0)
        {
            throw new ValidacaoException("ser.anexo_vazio", "O arquivo está vazio.");
        }

        if (conteudo.Length > TamanhoMaximoAnexo)
        {
            throw new ValidacaoException(
                "ser.anexo_grande",
                $"O arquivo tem {conteudo.Length / 1024 / 1024} MB e o limite é "
                + $"{TamanhoMaximoAnexo / 1024 / 1024} MB.");
        }

        // O binário vai para a tabela genérica de mídias (dedup por SHA-256), o mesmo lugar dos
        // anexos de ticket. Aqui fica só a referência.
        var midia = await midias.EnviarAsync(
            usuarioAtual.UsuarioId,
            nomeArquivo,
            contentType ?? "application/octet-stream",
            conteudo,
            categoria: "ser-solicitacao",
            cancellationToken);

        var anexo = new SerRascunhoAnexo
        {
            Id = Guid.NewGuid(),
            RascunhoId = r.Id,
            MidiaId = midia.Id,
            NomeArquivo = nomeArquivo,
            ContentType = contentType,
            Tamanho = conteudo.Length,
            CriadoEm = DateTime.UtcNow,
        };

        db.SerRascunhoAnexos.Add(anexo);
        r.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new SerRascunhoAnexoDto(
            anexo.Id, anexo.MidiaId, anexo.NomeArquivo, anexo.ContentType, anexo.Tamanho,
            anexo.EnviadoEm, anexo.CriadoEm);
    }

    public async Task RemoverAnexoAsync(Guid id, Guid anexoId, CancellationToken cancellationToken)
    {
        var anexo = await db.SerRascunhoAnexos
            .FirstOrDefaultAsync(x => x.Id == anexoId && x.RascunhoId == id, cancellationToken)
            ?? throw new NaoEncontradoException("Anexo do rascunho", anexoId);

        if (anexo.EnviadoEm is not null)
        {
            // Já está no SER: tirar daqui não tira de lá, e daria a falsa impressão de ter tirado.
            throw new ConflitoException(
                "ser.anexo_ja_enviado",
                "Este anexo já foi enviado ao SER e não pode ser removido daqui.");
        }

        // A mídia NÃO é apagada: ela é compartilhada por hash e pode estar em uso por outro
        // registro. Quem cuida de mídia órfã é a limpeza de mídias, não este serviço.
        db.SerRascunhoAnexos.Remove(anexo);
        await db.SaveChangesAsync(cancellationToken);
    }

    // ------------------------------------------------------------------ interno

    private SerSolicitacaoRascunho NovoRascunho()
    {
        var r = new SerSolicitacaoRascunho
        {
            Id = Guid.NewGuid(),
            Status = StatusRascunhoSer.Rascunho,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.SerSolicitacaoRascunhos.Add(r);
        return r;
    }

    /// <summary>
    /// Campos obrigatórios ainda vazios — os do bloco fixo mais os que o CATÁLOGO diz que aquele
    /// recurso exige. Validar contra o catálogo é o que faz a checagem valer para oncologia e
    /// para uma consulta simples sem regra duplicada em lugar nenhum.
    /// </summary>
    private async Task<List<string>> FaltandoAsync(
        SerSolicitacaoRascunho r, CancellationToken cancellationToken)
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
            var obrigatorios = await db.SerCatalogoCampos
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

    private static SerRascunhoDetalheDto ParaDetalhe(SerSolicitacaoRascunho r) => new(
        r.Id, r.Status, r.Tipo, r.RecursoValor, r.RecursoRotulo, r.Cns, r.PacienteNome, r.Hipotese,
        Ler(r.CamposJson), r.IdSerGerado, r.MensagemErro, r.CriadoPorNome, r.CriadoEm,
        r.AtualizadoEm, r.EnviadoEm,
        [.. r.Anexos
            .OrderBy(a => a.CriadoEm)
            .Select(a => new SerRascunhoAnexoDto(
                a.Id, a.MidiaId, a.NomeArquivo, a.ContentType, a.Tamanho, a.EnviadoEm, a.CriadoEm))]);

    private static string? Limpar(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
