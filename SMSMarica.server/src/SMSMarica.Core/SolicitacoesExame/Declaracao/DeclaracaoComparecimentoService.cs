using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Laudos.Configuracao;
using SMSMarica.Core.Laudos.Pdf;
using SMSMarica.Core.Midias;
using SMSMarica.Core.Worklist;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.SolicitacoesExame.Declaracao;

/// <summary>
/// Monta a declaração de comparecimento: resolve os dados (paciente, unidade, tipo
/// de exame, data/hora real do estudo) e renderiza o PDF reaproveitando a imagem do
/// cabeçalho do laudo. A hora do exame vem do PACS (StudyDate/StudyTime); se o PACS
/// não responder ou o estudo não tiver as tags, cai para <c>RealizadoEm</c>.
/// </summary>
public sealed partial class DeclaracaoComparecimentoService(
    ISolicitacoesExameService solicitacoes,
    ILaudoConfiguracaoService configuracao,
    IMidiasService midias,
    IConsultaStudyClient consultaStudy,
    IUsuarioAtualAccessor usuarioAtual,
    SmsMaricaDbContext db,
    IOptions<LaudosPdfOptions> options) : IDeclaracaoComparecimentoService
{
    private const string Cidade = "Maricá";
    private readonly LaudosPdfOptions _opt = options.Value;

    public async Task<byte[]> GerarAsync(Guid solicitacaoId, CancellationToken cancellationToken = default)
    {
        // Lança NaoEncontradoException quando não existe.
        var s = await solicitacoes.ObterPorIdAsync(solicitacaoId, cancellationToken);

        if (s.Status is not (StatusSolicitacaoExame.Realizada or StatusSolicitacaoExame.Laudada))
        {
            throw new ValidacaoException(
                "status",
                "A declaração de comparecimento só está disponível para exames já realizados.");
        }

        var dataHoraExame = await ResolverDataHoraExameAsync(s.StudyInstanceUID, s.RealizadoEm, s.CriadoEm, cancellationToken);
        var dataEmissao = DateTime.UtcNow.AddHours(_opt.OffsetHorasParaExibicao);
        var assinante = await ResolverAssinanteAsync(cancellationToken);
        var cabecalho = await ResolverImagemCabecalhoAsync(cancellationToken);

        var dados = new DeclaracaoComparecimentoDados(
            PacienteNome: string.IsNullOrWhiteSpace(s.PacienteNome) ? "—" : s.PacienteNome,
            UnidadeNome: string.IsNullOrWhiteSpace(s.UnidadeNome) ? "—" : s.UnidadeNome,
            TipoExameNome: string.IsNullOrWhiteSpace(s.TipoExameNome) ? "—" : s.TipoExameNome,
            DataHoraExame: dataHoraExame,
            Cidade: Cidade,
            DataEmissao: dataEmissao,
            AssinanteNome: assinante,
            CabecalhoImagem: cabecalho);

        return DeclaracaoComparecimentoPdf.Gerar(dados);
    }

    /// <summary>Hora real do estudo no PACS; fallback p/ RealizadoEm (UTC→local) e, em último caso, CriadoEm.</summary>
    private async Task<DateTime> ResolverDataHoraExameAsync(
        string studyInstanceUID, DateTime? realizadoEm, DateTime criadoEm, CancellationToken ct)
    {
        try
        {
            var dicom = await consultaStudy.ObterDataHoraEstudoAsync(studyInstanceUID, ct);
            if (dicom is { } dt) return dt;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // PACS indisponível — segue para o fallback local.
        }

        var utc = realizadoEm ?? criadoEm;
        return utc.AddHours(_opt.OffsetHorasParaExibicao);
    }

    private async Task<string> ResolverAssinanteAsync(CancellationToken ct)
    {
        if (usuarioAtual.UsuarioId is not Guid uid) return string.Empty;
        return await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == uid)
            .Select(u => u.NomeCompleto)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
    }

    /// <summary>
    /// Carrega a imagem do cabeçalho configurado do laudo: extrai o primeiro
    /// &lt;img src&gt; do HTML e resolve seus bytes (data URI ou /midias/{guid}).
    /// </summary>
    private async Task<byte[]?> ResolverImagemCabecalhoAsync(CancellationToken ct)
    {
        var config = await configuracao.ObterAsync(ct);
        var html = config.CabecalhoHtml;
        if (string.IsNullOrWhiteSpace(html)) return null;

        var m = RegexImgSrc().Match(html);
        if (!m.Success) return null;
        var src = m.Groups[1].Value;

        // 1) Data URI base64 embutido.
        if (src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var virgula = src.IndexOf(',');
            if (virgula > 0 && src.Contains(";base64,", StringComparison.OrdinalIgnoreCase))
            {
                try { return Convert.FromBase64String(src[(virgula + 1)..]); }
                catch { return null; }
            }
            return null;
        }

        // 2) Mídia local (/midias/{guid}).
        var idx = src.IndexOf("/midias/", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            var resto = src[(idx + "/midias/".Length)..];
            var fim = resto.IndexOfAny(['/', '?', '#']);
            if (fim >= 0) resto = resto[..fim];
            if (Guid.TryParse(resto, out var id))
            {
                var conteudo = await midias.ObterConteudoAsync(id, ct);
                return conteudo?.Conteudo;
            }
        }

        return null; // URL externa — não embute em PDF offline.
    }

    [GeneratedRegex("""<img[^>]*\ssrc\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase)]
    private static partial Regex RegexImgSrc();
}
