using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Laudos;
using SMSMarica.Core.Laudos.Pdf;
using SMSMarica.Core.Pacientes;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Exames;

/// <summary>
/// Gera o PDF do EXAME COMPLETO de uma solicitação: capa rica (dados do paciente,
/// exame, solicitante), seguida das imagens do PACS e, por último, do laudo (quando
/// finalizado). Capa+imagens são um QuestPDF; o laudo vem do renderer existente
/// (intacto) e é concatenado via <see cref="PdfMerge"/>.
/// </summary>
public interface IExameCompletoPdfService
{
    Task<byte[]> GerarAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default);
}

public sealed class ExameCompletoPdfService(
    SmsMaricaDbContext db,
    IExamePacsImagensReader imagensReader,
    IPacientesService pacientes,
    ILaudosService laudos,
    ILaudoPdfRenderer laudoPdf) : IExameCompletoPdfService
{
    /// <summary>Teto de imagens incluídas no PDF (trava de segurança).</summary>
    private const int MaxImagens = 300;

    /// <summary>Fuso de exibição (UTC-3) das datas/horas em UTC.</summary>
    private const int OffsetHoras = -3;

    public async Task<byte[]> GerarAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var sol = await db.SolicitacoesExame.AsNoTracking()
            .Include(s => s.TipoExame)
            .Include(s => s.Unidade)
            .FirstOrDefaultAsync(s => s.Id == solicitacaoExameId && s.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SolicitacaoExame), solicitacaoExameId);

        if (string.IsNullOrWhiteSpace(sol.StudyInstanceUID))
            throw new ConflitoException("exame.sem_imagens", "Este exame ainda não tem imagens disponíveis.");

        // Worklist: o StudyInstanceUID da solicitação é o real. Exames associados
        // (sem worklist) têm UID pré-gerado que não existe no PACS — nesses casos o
        // estudo REAL vem da associação ativa.
        var imagens = await imagensReader.ObterImagensAsync(sol.StudyInstanceUID, MaxImagens, cancellationToken);
        if (imagens.Count == 0)
        {
            var studyReal = await db.ExameAssociacoes.AsNoTracking()
                .Where(a => a.SolicitacaoExameId == sol.Id && a.ExcluidoEm == null)
                .OrderByDescending(a => a.CriadoEm)
                .Select(a => a.StudyInstanceUID)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(studyReal) && studyReal != sol.StudyInstanceUID)
                imagens = await imagensReader.ObterImagensAsync(studyReal, MaxImagens, cancellationToken);
        }

        var (nome, cpf, cns, nascimento) = await ResolverPacienteAsync(sol.PacienteId, cancellationToken);

        var laudo = await laudos.ObterPorStudyAsync(sol.StudyInstanceUID, cancellationToken);
        var incluiLaudo = laudo is { Status: StatusLaudo.Finalizado };

        var capa = new ExameCompletoCapa(
            PacienteNome: nome,
            PacienteCpf: cpf,
            PacienteCns: cns,
            PacienteNascimento: nascimento,
            ExameNome: sol.TipoExame?.Nome ?? "Exame de imagem",
            Modalidade: sol.TipoExame?.ModalidadeDicom.ToString(),
            Unidade: sol.Unidade?.Nome,
            Accession: sol.AccessionNumber,
            RealizadoEm: sol.RealizadoEm?.AddHours(OffsetHoras),
            SolicitadaEm: sol.CriadoEm.AddHours(OffsetHoras),
            SolicitanteNome: string.IsNullOrWhiteSpace(sol.SolicitanteNome) ? null : sol.SolicitanteNome,
            SolicitanteRegistro: MontarRegistroSolicitante(sol),
            IncluiLaudo: incluiLaudo,
            Justificativa: string.IsNullOrWhiteSpace(sol.Justificativa) ? null : sol.Justificativa,
            Observacoes: string.IsNullOrWhiteSpace(sol.Observacoes) ? null : sol.Observacoes);

        var capaImagens = new ExameCompletoPdfDocument(capa, imagens, ExameRecursos.Logo).Gerar();

        if (!incluiLaudo) return capaImagens;

        var laudoBytes = await laudoPdf.GerarAsync(laudo!.Id, ModoRodapeLaudo.FinalizadoNaoAssinado, cancellationToken);
        return PdfMerge.Concatenar(capaImagens, laudoBytes);
    }

    private async Task<(string Nome, string? Cpf, string? Cns, DateOnly? Nascimento)> ResolverPacienteAsync(
        Guid pacienteId, CancellationToken ct)
    {
        try
        {
            var p = await pacientes.ObterPorIdAsync(pacienteId, ct);
            return (p.NomeCompleto, p.Cpf, p.Cns, p.DataNascimento);
        }
        catch (NaoEncontradoException)
        {
            return ("—", null, null, null);
        }
    }

    private static string? MontarRegistroSolicitante(SolicitacaoExame sol)
    {
        if (string.IsNullOrWhiteSpace(sol.SolicitanteCrm)) return null;
        var conselho = string.IsNullOrWhiteSpace(sol.SolicitanteConselho)
            ? "CRM"
            : sol.SolicitanteConselho.Trim().ToUpperInvariant();
        var uf = string.IsNullOrWhiteSpace(sol.SolicitanteUfCrm) ? string.Empty : "/" + sol.SolicitanteUfCrm;
        return $"{conselho} {sol.SolicitanteCrm}{uf}";
    }
}
