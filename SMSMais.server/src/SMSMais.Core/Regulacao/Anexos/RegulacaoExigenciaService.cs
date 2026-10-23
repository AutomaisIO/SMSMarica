using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.DocumentosPaciente;
using SMSMais.Core.Identidade;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Anexos;

public sealed record ArquivoExigenciaDto(
    Guid Id,
    string Nome,
    string? Titulo,
    string? Descricao,
    string ContentType,
    long Tamanho,
    int Versao,
    SituacaoArquivoExigencia Situacao,
    OrigemArquivoExigencia Origem,
    DateTime? EnviadoAoSistemaEm,
    DateTime CriadoEm);

public sealed record ExigenciaDto(
    Guid Id,
    Guid? RegraId,
    string Titulo,
    bool Obrigatoria,
    SituacaoExigenciaRegulacao Situacao,
    string? CriticaTexto,
    int Ordem,
    IReadOnlyList<ArquivoExigenciaDto> Arquivos);

public sealed record ConteudoArquivoDto(byte[] Conteudo, string ContentType, string Nome);

/// <summary>
/// Um arquivo já compactado, esperando quem anexou conferir se ficou legível. Ainda não está na
/// solicitação nem no armazenamento — só na memória do servidor, até <paramref name="ExpiraEm"/>.
/// </summary>
/// <param name="Token">Identifica a prévia: ver o conteúdo, aceitar e descartar usam ele.</param>
/// <param name="TamanhoOriginal">O tamanho do arquivo como chegou; <paramref name="Tamanho"/> é o que
/// vai ser guardado.</param>
public sealed record ArquivoPreparadoDto(
    Guid Token, string Nome, string ContentType, long TamanhoOriginal, long Tamanho, DateTime ExpiraEm);

public sealed record AceitarArquivoPreparadoRequest(string? Titulo, string? Descricao);

public interface IRegulacaoExigenciaService
{
    Task<IReadOnlyList<ExigenciaDto>> ListarAsync(Guid solicitacaoId, CancellationToken ct);

    /// <summary>Garante que a caixinha "Anexos gerais" exista — ela vale para toda solicitação.</summary>
    Task<ExigenciaDto> GarantirAnexosGeraisAsync(Guid solicitacaoId, CancellationToken ct);

    /// <summary>
    /// Garante a caixinha de uma regra documental (plano 03). Idempotente: reavaliar as regras
    /// não pode criar a mesma caixinha de novo — os anexos já postos ficariam órfãos numa
    /// caixinha antiga que ninguém mais vê.
    /// </summary>
    Task<ExigenciaDto> GarantirDaRegraAsync(
        Guid solicitacaoId, Guid regraId, string titulo, bool obrigatoria, CancellationToken ct);

    /// <param name="titulo">Nome do documento dado por quem anexou; sem ele, vale o nome do arquivo.</param>
    /// <remarks>O arquivo é compactado na entrada (<see cref="ICompactadorAnexo"/>) e também entra no
    /// acervo do paciente ("Exames anexados" do cadastro). A tela usa <see cref="PrepararAsync"/>, que
    /// mostra o resultado antes de guardar; este anexa direto.</remarks>
    Task<ArquivoExigenciaDto> AnexarAsync(
        Guid solicitacaoId, Guid exigenciaId, string nome, string contentType, byte[] conteudo,
        CancellationToken ct, string? titulo = null, string? descricao = null);

    /// <summary>
    /// Primeiro passo do anexo pela tela: valida, compacta e guarda o resultado <b>em memória</b> por
    /// <see cref="RegulacaoExigenciaService.ValidadePrevia"/>, para quem anexou conferir se ficou legível.
    /// Nada entra na solicitação, no armazenamento nem no cadastro do paciente até
    /// <see cref="AceitarPreparadoAsync"/>.
    /// </summary>
    Task<ArquivoPreparadoDto> PrepararAsync(
        Guid solicitacaoId, Guid exigenciaId, string nome, string contentType, byte[] conteudo, CancellationToken ct);

    /// <summary>O arquivo compactado, para a prévia. Só para quem o preparou.</summary>
    Task<ConteudoArquivoDto> ObterPreparadoAsync(Guid solicitacaoId, Guid token, CancellationToken ct);

    /// <summary>Quem anexou conferiu e aceitou: o arquivo compactado entra na caixinha (e no cadastro).</summary>
    Task<ArquivoExigenciaDto> AceitarPreparadoAsync(
        Guid solicitacaoId, Guid token, string? titulo, string? descricao, CancellationToken ct);

    /// <summary>Quem anexou recusou a prévia: o arquivo é esquecido, nada fica guardado.</summary>
    Task DescartarPreparadoAsync(Guid solicitacaoId, Guid token, CancellationToken ct);

    /// <summary>
    /// Anexa um documento que o paciente já tem no cadastro (chave <c>tipo:id</c> do acervo).
    /// Não passa pelas regras de upload — o arquivo já foi aceito quando entrou no cadastro.
    /// </summary>
    Task<ArquivoExigenciaDto> AnexarDoAcervoAsync(
        Guid solicitacaoId, Guid exigenciaId, string chave, CancellationToken ct);

    /// <summary>Acervo (aceito) do paciente da solicitação — o que dá para anexar sem novo upload.</summary>
    Task<IReadOnlyList<ItemAcervoDto>> ListarAcervoAsync(Guid solicitacaoId, CancellationToken ct);

    /// <summary>Conteúdo de um item do acervo do paciente da solicitação (para o visualizador).</summary>
    Task<ConteudoAcervo> ObterConteudoAcervoAsync(Guid solicitacaoId, string chave, CancellationToken ct);

    /// <summary>
    /// Anexa um PDF gerado pelo próprio SMSMais (exame/laudo do acervo), marcado
    /// <see cref="OrigemArquivoExigencia.ExameInterno"/>.
    ///
    /// <para>Existe separado de <see cref="AnexarAsync"/> porque <b>não passa pelas regras de
    /// upload</b>: o limite de MB e a lista de tipos valem para o que o operador manda do
    /// celular, não para um laudo que nós mesmos produzimos e assinamos.</para>
    /// </summary>
    Task<ArquivoExigenciaDto> AnexarInternoAsync(
        Guid solicitacaoId, Guid exigenciaId, string nome, string contentType, byte[] conteudo,
        CancellationToken ct);

    Task RemoverArquivoAsync(Guid solicitacaoId, Guid arquivoId, CancellationToken ct);

    Task<ConteudoArquivoDto> ObterConteudoAsync(Guid solicitacaoId, Guid arquivoId, CancellationToken ct);

    /// <summary>
    /// A unidade trocou o procedimento: as caixinhas das regras do procedimento ANTIGO deixam de
    /// ser obrigatórias. Não somem — os arquivos nelas continuam visíveis e na trilha —, mas uma
    /// regra que já não vale para o pedido não pode travar o envio para sempre.
    /// </summary>
    Task DispensarCaixinhasDeOutroProcedimentoAsync(Guid solicitacaoId, Guid procedimentoId, CancellationToken ct);

    /// <summary>
    /// A unidade trocou o paciente: os PDFs de exame/laudo que o SMSMais anexou eram do paciente
    /// ANTERIOR e saem (mesmo tratamento do "remover" da tela). Mandar o laudo de uma pessoa no
    /// pedido de outra é o erro que isto existe para impedir. Devolve quantos saíram.
    /// </summary>
    Task<int> DescartarExamesInternosAsync(Guid solicitacaoId, CancellationToken ct);
}

/// <inheritdoc cref="IRegulacaoExigenciaService"/>
public sealed class RegulacaoExigenciaService(
    SmsMaisDbContext db,
    IArquivoExigenciaStore store,
    IRegulacaoConfiguracaoService configuracao,
    IUsuarioAtualAccessor usuarioAtual,
    IRegulacaoEscopo escopoRegulacao,
    IRegulacaoEventoService eventos,
    IDocumentosPacienteService? acervo = null,
    ICompactadorAnexo? compactador = null,
    IMemoryCache? cache = null) : IRegulacaoExigenciaService
{
    private const string TituloAnexosGerais = "Anexos gerais";

    /// <summary>Quanto tempo a prévia espera a decisão. Passou, é anexar de novo.</summary>
    public static readonly TimeSpan ValidadePrevia = TimeSpan.FromMinutes(30);

    /// <summary>O que fica na memória entre a prévia e a decisão — amarrado à solicitação, à caixinha
    /// e a quem preparou.</summary>
    private sealed record Preparado(
        Guid SolicitacaoId, Guid ExigenciaId, Guid? UsuarioId, AnexoCompactado Arquivo, DateTime ExpiraEm);

    public async Task<IReadOnlyList<ExigenciaDto>> ListarAsync(Guid solicitacaoId, CancellationToken ct)
    {
        // Sem esta conferência, quem tinha o módulo listava (e baixava) os anexos de qualquer
        // solicitação do município pelo id — inclusive de rascunho alheio.
        await ExigirSolicitacaoAsync(solicitacaoId, ct, rastrear: false);

        var exigencias = await db.RegulacaoSolicitacaoExigencias.AsNoTracking()
            .Where(e => e.SolicitacaoId == solicitacaoId)
            .Include(e => e.Arquivos)
            .OrderBy(e => e.Ordem).ThenBy(e => e.Titulo)
            .ToListAsync(ct);

        return [.. exigencias.Select(Mapear)];
    }

    public async Task<ExigenciaDto> GarantirAnexosGeraisAsync(Guid solicitacaoId, CancellationToken ct)
    {
        await ExigirSolicitacaoAsync(solicitacaoId, ct);

        var existente = await db.RegulacaoSolicitacaoExigencias
            .Include(e => e.Arquivos)
            .FirstOrDefaultAsync(e => e.SolicitacaoId == solicitacaoId && e.RegraId == null, ct);
        if (existente is not null) return Mapear(existente);

        var nova = new RegulacaoSolicitacaoExigencia
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = solicitacaoId,
            RegraId = null,
            Titulo = TituloAnexosGerais,
            Obrigatoria = false,
            Situacao = SituacaoExigenciaRegulacao.Pendente,
            // 1000 para ficar sempre depois das caixinhas de regra, que numeram a partir de 1.
            Ordem = 1000,
        };
        db.RegulacaoSolicitacaoExigencias.Add(nova);
        await db.SaveChangesAsync(ct);
        return Mapear(nova);
    }

    public async Task<ExigenciaDto> GarantirDaRegraAsync(
        Guid solicitacaoId, Guid regraId, string titulo, bool obrigatoria, CancellationToken ct)
    {
        await ExigirSolicitacaoAsync(solicitacaoId, ct);

        var existente = await db.RegulacaoSolicitacaoExigencias
            .Include(e => e.Arquivos)
            .FirstOrDefaultAsync(e => e.SolicitacaoId == solicitacaoId && e.RegraId == regraId, ct);

        if (existente is not null)
        {
            // O rótulo e a obrigatoriedade acompanham a regra: o manual muda, a caixinha muda de
            // nome — mas os arquivos já anexados continuam onde estão.
            if (existente.Titulo != titulo || existente.Obrigatoria != obrigatoria)
            {
                existente.Titulo = titulo;
                existente.Obrigatoria = obrigatoria;
                await db.SaveChangesAsync(ct);
            }
            return Mapear(existente);
        }

        var proximaOrdem = await db.RegulacaoSolicitacaoExigencias
            .Where(e => e.SolicitacaoId == solicitacaoId && e.RegraId != null)
            .CountAsync(ct) + 1;

        var nova = new RegulacaoSolicitacaoExigencia
        {
            Id = Guid.CreateVersion7(),
            SolicitacaoId = solicitacaoId,
            RegraId = regraId,
            Titulo = titulo,
            Obrigatoria = obrigatoria,
            Situacao = SituacaoExigenciaRegulacao.Pendente,
            Ordem = proximaOrdem,
        };
        db.RegulacaoSolicitacaoExigencias.Add(nova);
        await db.SaveChangesAsync(ct);
        return Mapear(nova);
    }

    public async Task<ArquivoExigenciaDto> AnexarAsync(
        Guid solicitacaoId, Guid exigenciaId, string nome, string contentType, byte[] conteudo,
        CancellationToken ct, string? titulo = null, string? descricao = null)
    {
        var solicitacao = await ExigirSolicitacaoAsync(solicitacaoId, ct);
        var exigencia = await ExigirExigenciaAsync(solicitacaoId, exigenciaId, ct);
        var arquivo = await ValidarECompactarAsync(nome, contentType, conteudo, ct);
        return await GravarUploadAsync(solicitacao, exigencia, arquivo, titulo, descricao, ct);
    }

    public async Task<ArquivoPreparadoDto> PrepararAsync(
        Guid solicitacaoId, Guid exigenciaId, string nome, string contentType, byte[] conteudo, CancellationToken ct)
    {
        await ExigirSolicitacaoAsync(solicitacaoId, ct, rastrear: false);
        await ExigirExigenciaAsync(solicitacaoId, exigenciaId, ct);
        var arquivo = await ValidarECompactarAsync(nome, contentType, conteudo, ct);

        // Guid aleatório (v4), não o v7 das linhas: o token é a única coisa entre a prévia e quem
        // quisesse adivinhá-la, e o v7 começa pelo relógio.
        var token = Guid.NewGuid();
        var expiraEm = DateTime.UtcNow.Add(ValidadePrevia);
        ExigirCache().Set(
            ChavePrevia(token),
            new Preparado(solicitacaoId, exigenciaId, usuarioAtual.UsuarioId, arquivo, expiraEm),
            new DateTimeOffset(expiraEm));

        return new ArquivoPreparadoDto(
            token, arquivo.Nome, arquivo.ContentType, arquivo.TamanhoOriginal, arquivo.Conteudo.LongLength, expiraEm);
    }

    public async Task<ConteudoArquivoDto> ObterPreparadoAsync(Guid solicitacaoId, Guid token, CancellationToken ct)
    {
        var p = ExigirPreparado(solicitacaoId, token);
        await ExigirSolicitacaoAsync(solicitacaoId, ct, rastrear: false);
        return new ConteudoArquivoDto(p.Arquivo.Conteudo, p.Arquivo.ContentType, p.Arquivo.Nome);
    }

    public async Task<ArquivoExigenciaDto> AceitarPreparadoAsync(
        Guid solicitacaoId, Guid token, string? titulo, string? descricao, CancellationToken ct)
    {
        var p = TomarPreparado(solicitacaoId, token);
        try
        {
            var solicitacao = await ExigirSolicitacaoAsync(solicitacaoId, ct);
            var exigencia = await ExigirExigenciaAsync(solicitacaoId, p.ExigenciaId, ct);
            return await GravarUploadAsync(solicitacao, exigencia, p.Arquivo, titulo, descricao, ct);
        }
        catch
        {
            // Não gravou (armazenamento fora, por exemplo): a prévia volta, para tentar de novo sem
            // reenviar o arquivo.
            if (p.ExpiraEm > DateTime.UtcNow) ExigirCache().Set(ChavePrevia(token), p, new DateTimeOffset(p.ExpiraEm));
            throw;
        }
    }

    public Task DescartarPreparadoAsync(Guid solicitacaoId, Guid token, CancellationToken ct)
    {
        // Idempotente: descartar o que já expirou (ou já foi descartado) não é erro.
        var c = ExigirCache();
        if (c.TryGetValue(ChavePrevia(token), out Preparado? p) && p is not null && DeQuemPede(p, solicitacaoId))
        {
            c.Remove(ChavePrevia(token));
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Tipo e tamanho como a configuração do módulo manda — sobre o arquivo <b>como chegou</b>, antes
    /// de compactar —, e depois a compactação de entrada.
    /// </summary>
    private async Task<AnexoCompactado> ValidarECompactarAsync(
        string nome, string contentType, byte[] conteudo, CancellationToken ct)
    {
        var config = await configuracao.ObterEntidadeAsync(ct);

        var tipo = (contentType ?? string.Empty).Split(';')[0].Trim().ToLowerInvariant();
        if (!config.AnexoTiposPermitidos.Contains(tipo))
        {
            throw new ValidacaoException(
                "arquivo",
                $"Tipo de arquivo não aceito ({tipo}). Aceitos: {string.Join(", ", config.AnexoTiposPermitidos)}.");
        }

        var limiteBytes = (long)config.AnexoLimiteMb * 1024 * 1024;
        if (conteudo.LongLength > limiteBytes)
        {
            throw new ValidacaoException(
                "arquivo", $"Arquivo acima do limite de {config.AnexoLimiteMb} MB.");
        }
        if (conteudo.LongLength == 0)
        {
            throw new ValidacaoException("arquivo", "Arquivo vazio.");
        }

        var limpo = LimparNome(nome);
        return compactador is null
            ? new AnexoCompactado(limpo, tipo, conteudo, conteudo.LongLength)
            : await compactador.CompactarAsync(limpo, tipo, conteudo, ct);
    }

    /// <summary>Grava o arquivo (já compactado) na caixinha, versionando, e a cópia no cadastro do paciente.</summary>
    private async Task<ArquivoExigenciaDto> GravarUploadAsync(
        RegulacaoSolicitacao solicitacao, RegulacaoSolicitacaoExigencia exigencia, AnexoCompactado upload,
        string? titulo, string? descricao, CancellationToken ct)
    {
        var tipo = upload.ContentType;
        var conteudo = upload.Conteudo;

        var versaoAnterior = await db.RegulacaoExigenciaArquivos
            .Where(a => a.ExigenciaId == exigencia.Id)
            .OrderByDescending(a => a.Versao)
            .FirstOrDefaultAsync(ct);

        // "Anexos gerais" acumula: são documentos diferentes na mesma caixinha. Caixinha de regra
        // é UM documento pedido, então o novo arquivo é uma nova VERSÃO do mesmo — é isso que
        // deixa o histórico contar por que a solicitação foi criticada e refeita.
        var caixinhaDeRegra = exigencia.RegraId is not null;
        if (caixinhaDeRegra && versaoAnterior is { Situacao: SituacaoArquivoExigencia.Atual })
        {
            versaoAnterior.Situacao = SituacaoArquivoExigencia.Substituido;
        }

        var id = Guid.CreateVersion7();
        var extensao = ExtensaoDe(upload.Nome, tipo);
        var chave = store.MontarChave(solicitacao.PacienteId, id, extensao);

        // O arquivo vai ao armazenamento ANTES da linha: se o Spaces falhar, não fica registro
        // apontando para conteúdo que não existe.
        await store.SalvarAsync(chave, conteudo, ct);

        var arquivo = new RegulacaoExigenciaArquivo
        {
            Id = id,
            ExigenciaId = exigencia.Id,
            ChaveArmazenamento = chave,
            Nome = upload.Nome,
            Titulo = Texto(titulo, 200),
            Descricao = Texto(descricao, 2000),
            ContentType = tipo,
            Tamanho = conteudo.LongLength,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(conteudo)),
            Versao = (versaoAnterior?.Versao ?? 0) + 1,
            SubstituiArquivoId = caixinhaDeRegra ? versaoAnterior?.Id : null,
            Situacao = SituacaoArquivoExigencia.Atual,
            Origem = OrigemArquivoExigencia.Upload,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.RegulacaoExigenciaArquivos.Add(arquivo);

        if (exigencia.Situacao is SituacaoExigenciaRegulacao.Pendente or SituacaoExigenciaRegulacao.Criticada)
        {
            exigencia.Situacao = SituacaoExigenciaRegulacao.Atendida;
            exigencia.CriticaTexto = null;
        }

        await db.SaveChangesAsync(ct);

        // Anexou na solicitação = fica também no cadastro do paciente, perene. A caixinha guarda a
        // própria cópia (retirar o anexo daqui não pode apagar o documento do cadastro).
        if (acervo is not null)
        {
            await acervo.RegistrarCopiaAsync(new NovoDocumentoPaciente(
                solicitacao.PacienteId,
                arquivo.Titulo ?? Path.GetFileNameWithoutExtension(arquivo.Nome),
                arquivo.Descricao,
                arquivo.Nome,
                tipo,
                conteudo,
                OrigemDocumentoPaciente.Solicitacao,
                $"regulacao:{solicitacao.Id:D}",
                SituacaoDocumentoPaciente.Aceito), ct);
        }

        return Mapear(arquivo);
    }

    public async Task<ArquivoExigenciaDto> AnexarDoAcervoAsync(
        Guid solicitacaoId, Guid exigenciaId, string chave, CancellationToken ct)
    {
        var solicitacao = await ExigirSolicitacaoAsync(solicitacaoId, ct);
        var exigencia = await ExigirExigenciaAsync(solicitacaoId, exigenciaId, ct);
        var conteudo = await ExigirAcervo().ObterConteudoPorChaveAsync(solicitacao.PacienteId, chave, ct);

        var versaoAnterior = await db.RegulacaoExigenciaArquivos
            .Where(a => a.ExigenciaId == exigenciaId)
            .OrderByDescending(a => a.Versao)
            .FirstOrDefaultAsync(ct);

        var caixinhaDeRegra = exigencia.RegraId is not null;
        if (caixinhaDeRegra && versaoAnterior is { Situacao: SituacaoArquivoExigencia.Atual })
        {
            versaoAnterior.Situacao = SituacaoArquivoExigencia.Substituido;
        }

        var id = Guid.CreateVersion7();
        var chaveArmazenamento = store.MontarChave(
            solicitacao.PacienteId, id, DocumentosPacienteService.Extensao(conteudo.MimeType));
        await store.SalvarAsync(chaveArmazenamento, conteudo.Conteudo, ct);

        var arquivo = new RegulacaoExigenciaArquivo
        {
            Id = id,
            ExigenciaId = exigenciaId,
            ChaveArmazenamento = chaveArmazenamento,
            Nome = LimparNome(conteudo.NomeArquivo),
            Titulo = Texto(conteudo.Titulo, 200),
            Descricao = Texto(conteudo.Descricao, 2000),
            ContentType = conteudo.MimeType,
            Tamanho = conteudo.Conteudo.LongLength,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(conteudo.Conteudo)),
            Versao = (versaoAnterior?.Versao ?? 0) + 1,
            SubstituiArquivoId = caixinhaDeRegra ? versaoAnterior?.Id : null,
            Situacao = SituacaoArquivoExigencia.Atual,
            Origem = OrigemArquivoExigencia.Acervo,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.RegulacaoExigenciaArquivos.Add(arquivo);

        if (exigencia.Situacao is SituacaoExigenciaRegulacao.Pendente or SituacaoExigenciaRegulacao.Criticada)
        {
            exigencia.Situacao = SituacaoExigenciaRegulacao.Atendida;
            exigencia.CriticaTexto = null;
        }

        await db.SaveChangesAsync(ct);
        return Mapear(arquivo);
    }

    public async Task<IReadOnlyList<ItemAcervoDto>> ListarAcervoAsync(Guid solicitacaoId, CancellationToken ct)
    {
        var solicitacao = await ExigirSolicitacaoAsync(solicitacaoId, ct, rastrear: false);
        return await ExigirAcervo().ListarAsync(solicitacao.PacienteId, incluirPendentes: false, ct);
    }

    public async Task<ConteudoAcervo> ObterConteudoAcervoAsync(Guid solicitacaoId, string chave, CancellationToken ct)
    {
        var solicitacao = await ExigirSolicitacaoAsync(solicitacaoId, ct, rastrear: false);
        return await ExigirAcervo().ObterConteudoPorChaveAsync(solicitacao.PacienteId, chave, ct);
    }

    public async Task<ArquivoExigenciaDto> AnexarInternoAsync(
        Guid solicitacaoId, Guid exigenciaId, string nome, string contentType, byte[] conteudo,
        CancellationToken ct)
    {
        var solicitacao = await ExigirSolicitacaoAsync(solicitacaoId, ct);
        var exigencia = await ExigirExigenciaAsync(solicitacaoId, exigenciaId, ct);

        if (conteudo.LongLength == 0)
        {
            throw new ValidacaoException("arquivo", "O PDF gerado veio vazio.");
        }

        var versaoAnterior = await db.RegulacaoExigenciaArquivos
            .Where(a => a.ExigenciaId == exigenciaId)
            .OrderByDescending(a => a.Versao)
            .FirstOrDefaultAsync(ct);

        if (exigencia.RegraId is not null && versaoAnterior is { Situacao: SituacaoArquivoExigencia.Atual })
        {
            versaoAnterior.Situacao = SituacaoArquivoExigencia.Substituido;
        }

        var id = Guid.CreateVersion7();
        var chave = store.MontarChave(solicitacao.PacienteId, id, "pdf");
        await store.SalvarAsync(chave, conteudo, ct);

        var arquivo = new RegulacaoExigenciaArquivo
        {
            Id = id,
            ExigenciaId = exigenciaId,
            ChaveArmazenamento = chave,
            Nome = LimparNome(nome),
            ContentType = contentType,
            Tamanho = conteudo.LongLength,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(conteudo)),
            Versao = (versaoAnterior?.Versao ?? 0) + 1,
            SubstituiArquivoId = exigencia.RegraId is not null ? versaoAnterior?.Id : null,
            Situacao = SituacaoArquivoExigencia.Atual,
            Origem = OrigemArquivoExigencia.ExameInterno,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = usuarioAtual.UsuarioId,
        };
        db.RegulacaoExigenciaArquivos.Add(arquivo);
        await db.SaveChangesAsync(ct);

        return Mapear(arquivo);
    }

    public async Task RemoverArquivoAsync(Guid solicitacaoId, Guid arquivoId, CancellationToken ct)
    {
        await ExigirSolicitacaoAsync(solicitacaoId, ct);
        var arquivo = await ExigirArquivoAsync(solicitacaoId, arquivoId, ct);

        // Depois de ir para o sistema de regulação, o arquivo é prova do que foi enviado.
        if (arquivo.EnviadoAoSistemaEm is not null)
        {
            throw new ConflitoException(
                "regulacao.anexo.ja_enviado",
                "Este arquivo já foi enviado ao sistema de regulação e não pode ser removido. "
                + "Anexe uma versão nova no lugar.");
        }

        // Some da tela, permanece na trilha: `Removido`, não delete. O conteúdo no Spaces sai,
        // porque é dado de paciente e não há razão para guardá-lo depois de retirado.
        arquivo.Situacao = SituacaoArquivoExigencia.Removido;
        await store.ExcluirAsync(arquivo.ChaveArmazenamento, ct);

        var exigencia = await db.RegulacaoSolicitacaoExigencias
            .Include(e => e.Arquivos)
            .FirstAsync(e => e.Id == arquivo.ExigenciaId, ct);
        var sobrou = exigencia.Arquivos.Any(a =>
            a.Id != arquivo.Id && a.Situacao == SituacaoArquivoExigencia.Atual);
        if (!sobrou && exigencia.Situacao == SituacaoExigenciaRegulacao.Atendida)
        {
            exigencia.Situacao = SituacaoExigenciaRegulacao.Pendente;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<ConteudoArquivoDto> ObterConteudoAsync(
        Guid solicitacaoId, Guid arquivoId, CancellationToken ct)
    {
        await ExigirSolicitacaoAsync(solicitacaoId, ct);
        var arquivo = await ExigirArquivoAsync(solicitacaoId, arquivoId, ct);

        var conteudo = await store.LerAsync(arquivo.ChaveArmazenamento, ct)
            ?? throw new NaoEncontradoException("Conteúdo do anexo da regulação", arquivoId);

        await RegistrarAcessoAsync(solicitacaoId, arquivo, ct);

        return new ConteudoArquivoDto(conteudo, arquivo.ContentType, arquivo.Nome);
    }

    /// <summary>
    /// Deixa na trilha que ALGUÉM abriu/baixou este documento — é como a regulação sabe que a guia
    /// de agendamento foi retirada e por quem (e de qual unidade: o evento carrega a unidade ativa).
    /// Uma vez por (documento, usuário): reabrir o mesmo arquivo não vira ruído. Fluxo sem usuário
    /// autenticado (sistema) não tem o que registrar.
    /// </summary>
    private async Task RegistrarAcessoAsync(
        Guid solicitacaoId, RegulacaoExigenciaArquivo arquivo, CancellationToken ct)
    {
        var usuarioId = usuarioAtual.UsuarioId;
        if (usuarioId is null) return;

        // Dedup em memória, não no banco: DetalheJson é jsonb e um LIKE sobre jsonb estoura
        // (42883: operator does not exist: jsonb ~~ jsonb). Os acessos deste usuário a ESTA
        // solicitação são poucos (um por arquivo aberto), então carregá-los é barato.
        var chave = arquivo.Id.ToString();
        var detalhes = await db.RegulacaoEventos.AsNoTracking()
            .Where(e => e.SolicitacaoId == solicitacaoId
                && e.Tipo == TipoEventoRegulacao.DocumentoAcessado
                && e.UsuarioId == usuarioId
                && e.DetalheJson != null)
            .Select(e => e.DetalheJson)
            .ToListAsync(ct);
        if (detalhes.Any(d => d is not null && d.Contains(chave, StringComparison.Ordinal))) return;

        var papel = await escopoRegulacao.EhAgenteAsync(ct)
            ? PapelEventoRegulacao.Agente
            : PapelEventoRegulacao.Solicitante;

        await eventos.RegistrarAsync(
            solicitacaoId, TipoEventoRegulacao.DocumentoAcessado, papel, ct,
            detalhe: new { arquivoId = arquivo.Id, nome = arquivo.Nome });
        await db.SaveChangesAsync(ct);
    }

    public async Task DispensarCaixinhasDeOutroProcedimentoAsync(
        Guid solicitacaoId, Guid procedimentoId, CancellationToken ct)
    {
        var regrasDoProcedimento = db.RegulacaoRegras
            .Where(r => r.ProcedimentoId == procedimentoId)
            .Select(r => r.Id);

        var foraDoProcedimento = await db.RegulacaoSolicitacaoExigencias
            .Where(e => e.SolicitacaoId == solicitacaoId
                && e.RegraId != null
                && e.Obrigatoria
                && !regrasDoProcedimento.Contains(e.RegraId.Value))
            .ToListAsync(ct);
        if (foraDoProcedimento.Count == 0) return;

        foreach (var e in foraDoProcedimento) e.Obrigatoria = false;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> DescartarExamesInternosAsync(Guid solicitacaoId, CancellationToken ct)
    {
        var arquivos = await db.RegulacaoExigenciaArquivos
            .Include(a => a.Exigencia!).ThenInclude(e => e.Arquivos)
            .Where(a => a.Exigencia!.SolicitacaoId == solicitacaoId
                && a.Origem == OrigemArquivoExigencia.ExameInterno
                && a.Situacao == SituacaoArquivoExigencia.Atual
                // O que já foi ao sistema de regulação é prova do que foi mandado — não sai.
                && a.EnviadoAoSistemaEm == null)
            .ToListAsync(ct);
        if (arquivos.Count == 0) return 0;

        foreach (var arquivo in arquivos)
        {
            arquivo.Situacao = SituacaoArquivoExigencia.Removido;
            await store.ExcluirAsync(arquivo.ChaveArmazenamento, ct);

            var exigencia = arquivo.Exigencia!;
            exigencia.ExameInternoExameImagemId = null;
            exigencia.ExameInternoLaudoId = null;
            exigencia.ValidadoExamePor = null;
            exigencia.ValidadoExameEm = null;

            var sobrou = exigencia.Arquivos.Any(a => a.Situacao == SituacaoArquivoExigencia.Atual);
            if (!sobrou && exigencia.Situacao == SituacaoExigenciaRegulacao.Atendida)
            {
                exigencia.Situacao = SituacaoExigenciaRegulacao.Pendente;
            }
        }

        await db.SaveChangesAsync(ct);
        return arquivos.Count;
    }

    // ---------------------------------------------------------------- apoio

    /// <summary>
    /// Carrega a solicitação e confere o alcance de quem pede (unidade no escopo; rascunho só de
    /// quem o abriu). Fora do alcance é "não encontrado", como no detalhe.
    /// </summary>
    private async Task<RegulacaoSolicitacao> ExigirSolicitacaoAsync(
        Guid id, CancellationToken ct, bool rastrear = true)
    {
        var consulta = rastrear
            ? db.RegulacaoSolicitacoes.AsQueryable()
            : db.RegulacaoSolicitacoes.AsNoTracking();

        var s = await consulta.FirstOrDefaultAsync(x => x.Id == id && x.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Solicitação da regulação", id);

        await escopoRegulacao.ExigirAlcanceAsync(s, ct);
        return s;
    }

    private async Task<RegulacaoSolicitacaoExigencia> ExigirExigenciaAsync(
        Guid solicitacaoId, Guid exigenciaId, CancellationToken ct) =>
        await db.RegulacaoSolicitacaoExigencias
            .FirstOrDefaultAsync(e => e.Id == exigenciaId && e.SolicitacaoId == solicitacaoId, ct)
        ?? throw new NaoEncontradoException("Exigência da solicitação", exigenciaId);

    /// <summary>
    /// Busca o arquivo <b>amarrado à solicitação</b>. Buscar só por id do arquivo deixaria a rota
    /// servir anexo de outra solicitação para quem adivinhasse o GUID.
    /// </summary>
    private async Task<RegulacaoExigenciaArquivo> ExigirArquivoAsync(
        Guid solicitacaoId, Guid arquivoId, CancellationToken ct) =>
        await db.RegulacaoExigenciaArquivos
            .Include(a => a.Exigencia)
            .FirstOrDefaultAsync(a => a.Id == arquivoId && a.Exigencia!.SolicitacaoId == solicitacaoId, ct)
        ?? throw new NaoEncontradoException("Anexo da solicitação", arquivoId);

    private static readonly object TravaPrevias = new();

    private static string ChavePrevia(Guid token) => $"regulacao:anexo-previa:{token:N}";

    private bool DeQuemPede(Preparado p, Guid solicitacaoId) =>
        p.SolicitacaoId == solicitacaoId && p.UsuarioId == usuarioAtual.UsuarioId;

    /// <summary>
    /// A prévia desta solicitação, preparada por quem pede. A de outra pessoa (ou de outra
    /// solicitação) responde como expirada — não diz que existe.
    /// </summary>
    private Preparado ExigirPreparado(Guid solicitacaoId, Guid token)
    {
        if (ExigirCache().TryGetValue(ChavePrevia(token), out Preparado? p) && p is not null && DeQuemPede(p, solicitacaoId))
        {
            return p;
        }
        throw new ConflitoException(
            "regulacao.anexo.previa_expirada", "A prévia deste arquivo expirou. Anexe o arquivo de novo.");
    }

    /// <summary>
    /// Tira a prévia da memória ao mesmo tempo em que a lê: dois "Aceitar" seguidos não podem virar
    /// duas versões do mesmo arquivo.
    /// </summary>
    private Preparado TomarPreparado(Guid solicitacaoId, Guid token)
    {
        lock (TravaPrevias)
        {
            var p = ExigirPreparado(solicitacaoId, token);
            ExigirCache().Remove(ChavePrevia(token));
            return p;
        }
    }

    private IMemoryCache ExigirCache() =>
        cache ?? throw new InvalidOperationException("IMemoryCache não registrado: a prévia de anexo precisa dele.");

    private IDocumentosPacienteService ExigirAcervo() =>
        acervo ?? throw new InvalidOperationException("Acervo do paciente não registrado.");

    private static string? Texto(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var t = valor.Trim();
        return t.Length > maximo ? t[..maximo] : t;
    }

    private static string ExtensaoDe(string nome, string contentType)
    {
        var doNome = Path.GetExtension(nome ?? string.Empty).Trim('.').ToLowerInvariant();
        if (!string.IsNullOrEmpty(doNome) && doNome.Length <= 5) return doNome;

        return contentType switch
        {
            "image/jpeg" => "jpg",
            "image/png" => "png",
            "image/webp" => "webp",
            "application/pdf" => "pdf",
            _ => "bin",
        };
    }

    /// <summary>Guarda só o nome do arquivo: caminho do cliente não interessa e é vetor de bobagem.</summary>
    private static string LimparNome(string nome)
    {
        var so = Path.GetFileName((nome ?? string.Empty).Replace('\\', '/'));
        so = string.IsNullOrWhiteSpace(so) ? "arquivo" : so.Trim();
        return so.Length > 260 ? so[^260..] : so;
    }

    private static ExigenciaDto Mapear(RegulacaoSolicitacaoExigencia e) =>
        new(e.Id, e.RegraId, e.Titulo, e.Obrigatoria, e.Situacao, e.CriticaTexto, e.Ordem,
            [.. e.Arquivos
                .Where(a => a.Situacao != SituacaoArquivoExigencia.Removido)
                .OrderBy(a => a.Versao)
                .Select(Mapear)]);

    private static ArquivoExigenciaDto Mapear(RegulacaoExigenciaArquivo a) =>
        new(a.Id, a.Nome, a.Titulo, a.Descricao, a.ContentType, a.Tamanho, a.Versao, a.Situacao, a.Origem,
            a.EnviadoAoSistemaEm, a.CriadoEm);
}
