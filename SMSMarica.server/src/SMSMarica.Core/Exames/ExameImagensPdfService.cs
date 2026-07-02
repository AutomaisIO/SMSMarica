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
}

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
        var sol = await db.SolicitacoesExame.AsNoTracking()
            .Include(s => s.TipoExame)
            .Include(s => s.Unidade)
            .FirstOrDefaultAsync(s => s.Id == solicitacaoExameId && s.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), solicitacaoExameId);

        if (string.IsNullOrWhiteSpace(sol.StudyInstanceUID))
            throw new ConflitoException("exame.sem_imagens", "Este exame ainda não tem imagens disponíveis.");

        var chave = $"imagens-exame/{sol.PacienteId}/{sol.StudyInstanceUID}.pdf";

        var cache = await armazenamento.LerAsync(chave, cancellationToken);
        if (cache is { Length: > 0 })
            return cache;

        var imagens = await imagensReader.ObterImagensAsync(sol.StudyInstanceUID, MaxImagens, cancellationToken);
        if (imagens.Count == 0)
            throw new ConflitoException("exame.sem_imagens", "Este exame ainda não tem imagens disponíveis no PACS.");

        var paciente = await pacientes.ObterPorIdAsync(sol.PacienteId, cancellationToken);

        var capa = new ExameImagensCapa(
            PacienteNome: paciente.NomeCompleto,
            PacienteCpf: paciente.Cpf,
            PacienteCns: paciente.Cns,
            PacienteNascimento: paciente.DataNascimento,
            ExameNome: sol.TipoExame?.Nome ?? "Exame de imagem",
            // RealizadoEm é UTC → converte p/ Brasília na exibição (mesma regra dos outros PDFs).
            RealizadoEm: FusoBrasilia.ParaExibicao(sol.RealizadoEm),
            Unidade: sol.Unidade?.Nome,
            Descricao: PrimeiroNaoVazio(sol.Justificativa, sol.Observacoes),
            Anamnese: Resumir(sol.Observacoes, sol.Justificativa));

        var pdf = new ExameImagensPdfDocument(capa, imagens, ExameRecursos.Logo).Gerar();

        await armazenamento.SalvarAsync(chave, pdf, cancellationToken);
        return pdf;
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
