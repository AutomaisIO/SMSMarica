using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMSMarica.Core.Anexos.Dtos;
using SMSMarica.Core.Armazenamento;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMarica.Core.Anexos;

public sealed class AnexosService(
    SmsMaisDbContext db,
    IArmazenamentoArquivos armazenamento,
    IPacienteResolver pacienteResolver,
    IUsuarioAtualAccessor usuarioAtual,
    IOptions<AnexosOptions> options) : IAnexosService
{
    /// <summary>25 MB — teto do PDF montado no PWA (digitalização de várias páginas).</summary>
    private const long TamanhoMaximoBytes = 25 * 1024 * 1024;

    private const string MimePdf = "application/pdf";
    private const string OrigemPwa = "pwa-scanner";

    private readonly AnexosOptions _opcoes = options.Value;

    public async Task<CriarTokenRespostaDto> CriarTokenAsync(
        Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var sol = await db.ExamesImagem.AsNoTracking().Include(s => s.Solicitacao)
            .FirstOrDefaultAsync(s => s.Id == solicitacaoExameId && s.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), solicitacaoExameId);

        var paciente = await pacienteResolver.ResolverAsync(sol.Solicitacao!.PacienteId, cancellationToken);
        var pacienteNome = paciente?.Nome ?? string.Empty;

        var agora = DateTime.UtcNow;
        var token = GerarToken();

        var entidade = new AnexoUploadToken
        {
            Id = Guid.CreateVersion7(),
            Token = token,
            ExameImagemId = sol.Id,
            PatientId = sol.Solicitacao!.PacienteId,
            PacienteNome = pacienteNome,
            CriadoPor = usuarioAtual.UsuarioId,
            CriadoEm = agora,
            ExpiraEm = agora.AddMinutes(_opcoes.TokenTtlMinutos),
        };

        db.AnexoUploadTokens.Add(entidade);
        await db.SaveChangesAsync(cancellationToken);

        var url = $"{_opcoes.PwaBaseUrl.TrimEnd('/')}/?t={token}";
        return new CriarTokenRespostaDto(
            token, url, entidade.ExpiraEm, sol.Id, new AnexoTokenPacienteDto(sol.Solicitacao!.PacienteId, pacienteNome));
    }

    public async Task<ValidarTokenRespostaDto> ValidarTokenAsync(
        string token, CancellationToken cancellationToken = default)
    {
        var entidade = await CarregarTokenValidoAsync(token, rastrear: false, cancellationToken);

        var resumo = await db.ExamesImagem.AsNoTracking()
            .Where(s => s.Id == entidade.ExameImagemId)
            .Select(s => s.TipoExame != null ? s.TipoExame.Nome : s.AccessionNumber)
            .FirstOrDefaultAsync(cancellationToken);

        return new ValidarTokenRespostaDto(
            true,
            entidade.ExpiraEm,
            new SessaoPacienteDto(entidade.PacienteNome),
            new SessaoSolicitacaoDto(entidade.ExameImagemId, resumo));
    }

    public async Task<AnexoUploadRespostaDto> ReceberUploadAsync(
        string token,
        string nome,
        string? descricao,
        int? paginas,
        byte[] conteudo,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        if (conteudo is null || conteudo.Length == 0)
        {
            throw new ValidacaoException("anexo.vazio", "Arquivo vazio.");
        }
        if (conteudo.Length > TamanhoMaximoBytes)
        {
            throw new ValidacaoException("anexo.muito_grande",
                $"Arquivo excede o limite de {TamanhoMaximoBytes / (1024 * 1024)} MB.");
        }
        if (!string.Equals(mimeType, MimePdf, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidacaoException("anexo.tipo_invalido", "Apenas arquivos PDF são aceitos.");
        }

        nome = (nome ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(nome))
        {
            throw new ValidacaoException("anexo.nome_obrigatorio", "Informe um nome para o documento.");
        }
        if (nome.Length > 200)
        {
            throw new ValidacaoException("anexo.nome_grande", "Nome deve ter no máximo 200 caracteres.");
        }
        descricao = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricao is { Length: > 2000 })
        {
            throw new ValidacaoException("anexo.descricao_grande", "Descrição deve ter no máximo 2000 caracteres.");
        }

        var tokenEntidade = await CarregarTokenValidoAsync(token, rastrear: true, cancellationToken);

        var agora = DateTime.UtcNow;
        var hash = Convert.ToHexString(SHA256.HashData(conteudo)).ToLowerInvariant();

        // Dedup: mesmo conteúdo na mesma solicitação (ex.: PWA reenviou) → reaproveita.
        var existente = await db.DocumentosExame.AsNoTracking()
            .FirstOrDefaultAsync(
                d => d.ExameImagemId == tokenEntidade.ExameImagemId
                     && d.HashSha256 == hash
                     && d.ExcluidoEm == null,
                cancellationToken);
        if (existente is not null)
        {
            tokenEntidade.UltimoUsoEm = agora;
            await db.SaveChangesAsync(cancellationToken);
            return new AnexoUploadRespostaDto(existente.Id, existente.Nome);
        }

        // Chave = {prefixo}/{uuid-paciente}/{uuid-documento}.pdf — pasta por paciente.
        var documentoId = Guid.CreateVersion7();
        var chave = armazenamento.MontarChaveDocumento(tokenEntidade.PatientId, documentoId, "pdf");
        await armazenamento.SalvarAsync(chave, conteudo, cancellationToken);

        var documento = new DocumentoExame
        {
            Id = documentoId,
            ExameImagemId = tokenEntidade.ExameImagemId,
            AnexoUploadTokenId = tokenEntidade.Id,
            Nome = nome,
            Descricao = descricao,
            MimeType = MimePdf,
            TamanhoBytes = conteudo.Length,
            HashSha256 = hash,
            ChaveArmazenamento = chave,
            Status = StatusDocumentoExame.Pendente,
            Origem = OrigemPwa,
            Paginas = paginas is > 0 ? paginas : null,
            CriadoEm = agora,
            CriadoPor = null, // upload anônimo (sem JWT do cidadão)
        };

        db.DocumentosExame.Add(documento);
        tokenEntidade.UltimoUsoEm = agora;
        await db.SaveChangesAsync(cancellationToken);

        return new AnexoUploadRespostaDto(documento.Id, documento.Nome);
    }

    public async Task<IReadOnlyList<AnexoExameDto>> ListarPorSolicitacaoAsync(
        Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var docs = await db.DocumentosExame.AsNoTracking()
            .Where(d => d.ExameImagemId == solicitacaoExameId && d.ExcluidoEm == null)
            .OrderByDescending(d => d.CriadoEm)
            .ToListAsync(cancellationToken);

        return [.. docs.Select(AnexosMapper.ParaDto)];
    }

    public async Task<AnexoExameDto> SalvarAsync(
        Guid id, SalvarAnexoDto dto, CancellationToken cancellationToken = default)
    {
        var documento = await db.DocumentosExame
            .FirstOrDefaultAsync(d => d.Id == id && d.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(DocumentoExame), id);

        if (!string.IsNullOrWhiteSpace(dto.Nome))
        {
            documento.Nome = dto.Nome.Trim();
        }
        if (dto.Descricao is not null)
        {
            documento.Descricao = string.IsNullOrWhiteSpace(dto.Descricao) ? null : dto.Descricao.Trim();
        }

        documento.Status = StatusDocumentoExame.Salvo;
        documento.AtualizadoEm = DateTime.UtcNow;
        documento.AtualizadoPor = usuarioAtual.UsuarioId;

        await db.SaveChangesAsync(cancellationToken);
        return AnexosMapper.ParaDto(documento);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var documento = await db.DocumentosExame
            .FirstOrDefaultAsync(d => d.Id == id && d.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(DocumentoExame), id);

        // Remove o binário do S3 (idempotente — no-op se já não existir). O soft-delete
        // preserva o registro para auditoria, mas o objeto sai do armazenamento.
        await armazenamento.ExcluirAsync(documento.ChaveArmazenamento, cancellationToken);

        documento.ExcluidoEm = DateTime.UtcNow;
        documento.ExcluidoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<AnexoConteudo?> ObterConteudoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var documento = await db.DocumentosExame.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id && d.ExcluidoEm == null, cancellationToken);
        if (documento is null) return null;

        var bytes = await armazenamento.LerAsync(documento.ChaveArmazenamento, cancellationToken);
        if (bytes is null) return null;

        return new AnexoConteudo(bytes, documento.MimeType, MontarNomeArquivo(documento.Nome));
    }

    public async Task<IReadOnlyList<AnexoExameDto>> ListarPorPacienteAsync(
        Guid pacienteId, CancellationToken cancellationToken = default)
    {
        var docs = await (
            from d in db.DocumentosExame.AsNoTracking()
            join s in db.ExamesImagem.AsNoTracking() on d.ExameImagemId equals s.Id
            where s.Solicitacao!.PacienteId == pacienteId
                  && s.ExcluidoEm == null
                  && d.ExcluidoEm == null
                  && d.Status == StatusDocumentoExame.Salvo
            orderby d.CriadoEm descending
            select d).ToListAsync(cancellationToken);

        return [.. docs.Select(AnexosMapper.ParaDto)];
    }

    // ---- Helpers ----

    private async Task<AnexoUploadToken> CarregarTokenValidoAsync(
        string token, bool rastrear, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new NaoEncontradoException(nameof(AnexoUploadToken), "(vazio)");
        }

        var query = db.AnexoUploadTokens.AsQueryable();
        if (!rastrear) query = query.AsNoTracking();

        var entidade = await query.FirstOrDefaultAsync(t => t.Token == token, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(AnexoUploadToken), token);

        if (entidade.RevogadoEm is not null)
        {
            throw new NaoEncontradoException(nameof(AnexoUploadToken), token);
        }
        if (entidade.ExpiraEm <= DateTime.UtcNow)
        {
            throw new NaoEncontradoException(nameof(AnexoUploadToken), token);
        }

        return entidade;
    }

    /// <summary>Token aleatório URL-safe (base64url de 32 bytes; 43 chars sem padding).</summary>
    private static string GerarToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string MontarNomeArquivo(string nome)
    {
        var limpo = new string([.. nome.Where(c => !Path.GetInvalidFileNameChars().Contains(c))]).Trim();
        if (string.IsNullOrEmpty(limpo)) limpo = "documento";
        return limpo.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? limpo : $"{limpo}.pdf";
    }
}
