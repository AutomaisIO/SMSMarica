using System.Security.Cryptography;

using Microsoft.EntityFrameworkCore;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Anexos;

public sealed record ArquivoExigenciaDto(
    Guid Id,
    string Nome,
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

    Task<ArquivoExigenciaDto> AnexarAsync(
        Guid solicitacaoId, Guid exigenciaId, string nome, string contentType, byte[] conteudo,
        CancellationToken ct);

    Task RemoverArquivoAsync(Guid solicitacaoId, Guid arquivoId, CancellationToken ct);

    Task<ConteudoArquivoDto> ObterConteudoAsync(Guid solicitacaoId, Guid arquivoId, CancellationToken ct);
}

/// <inheritdoc cref="IRegulacaoExigenciaService"/>
public sealed class RegulacaoExigenciaService(
    SmsMaisDbContext db,
    IArquivoExigenciaStore store,
    IRegulacaoConfiguracaoService configuracao,
    IUsuarioAtualAccessor usuarioAtual) : IRegulacaoExigenciaService
{
    private const string TituloAnexosGerais = "Anexos gerais";

    public async Task<IReadOnlyList<ExigenciaDto>> ListarAsync(Guid solicitacaoId, CancellationToken ct)
    {
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
        CancellationToken ct)
    {
        var solicitacao = await ExigirSolicitacaoAsync(solicitacaoId, ct);
        var exigencia = await ExigirExigenciaAsync(solicitacaoId, exigenciaId, ct);

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

        var versaoAnterior = await db.RegulacaoExigenciaArquivos
            .Where(a => a.ExigenciaId == exigenciaId)
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
        var extensao = ExtensaoDe(nome, tipo);
        var chave = store.MontarChave(solicitacao.PacienteId, id, extensao);

        // O arquivo vai ao armazenamento ANTES da linha: se o Spaces falhar, não fica registro
        // apontando para conteúdo que não existe.
        await store.SalvarAsync(chave, conteudo, ct);

        var arquivo = new RegulacaoExigenciaArquivo
        {
            Id = id,
            ExigenciaId = exigenciaId,
            ChaveArmazenamento = chave,
            Nome = LimparNome(nome),
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

        return new ConteudoArquivoDto(conteudo, arquivo.ContentType, arquivo.Nome);
    }

    // ---------------------------------------------------------------- apoio

    private async Task<RegulacaoSolicitacao> ExigirSolicitacaoAsync(Guid id, CancellationToken ct) =>
        await db.RegulacaoSolicitacoes.FirstOrDefaultAsync(s => s.Id == id && s.ExcluidoEm == null, ct)
        ?? throw new NaoEncontradoException("Solicitação da regulação", id);

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
        new(a.Id, a.Nome, a.ContentType, a.Tamanho, a.Versao, a.Situacao, a.Origem,
            a.EnviadoAoSistemaEm, a.CriadoEm);
}
