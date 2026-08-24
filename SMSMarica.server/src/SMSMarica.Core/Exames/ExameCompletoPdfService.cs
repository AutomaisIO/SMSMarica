using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Common.Tempo;
using SMSMarica.Core.Laudos;
using SMSMarica.Core.Laudos.Pdf;
using SMSMarica.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

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
    SmsMaisDbContext db,
    IExamePacsImagensReader imagensReader,
    IPacientesService pacientes,
    ILaudosService laudos,
    ILaudoPdfRenderer laudoPdf) : IExameCompletoPdfService
{
    /// <summary>Teto de imagens incluídas no PDF (trava de segurança).</summary>
    private const int MaxImagens = 300;

    public async Task<byte[]> GerarAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default)
    {
        var sol = await db.ExamesImagem.AsNoTracking()
            .Include(s => s.TipoExame)
            .Include(s => s.Solicitacao!).ThenInclude(so => so.UnidadeExecutante)
            .Include(s => s.Solicitacao!).ThenInclude(so => so.UnidadeSolicitante)
            .FirstOrDefaultAsync(s => s.Id == solicitacaoExameId && s.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), solicitacaoExameId);

        if (string.IsNullOrWhiteSpace(sol.StudyInstanceUID))
            throw new ConflitoException("exame.sem_imagens", "Este exame ainda não tem imagens disponíveis.");

        // Worklist: o StudyInstanceUID da solicitação é o real. Exames associados
        // (sem worklist) têm UID pré-gerado que não existe no PACS — nesses casos o
        // estudo REAL vem da associação ativa.
        var imagens = await imagensReader.ObterImagensAsync(sol.StudyInstanceUID, MaxImagens, cancellationToken);
        if (imagens.Count == 0)
        {
            var studyReal = await db.ExameAssociacoes.AsNoTracking()
                .Where(a => a.ExameImagemId == sol.Id && a.ExcluidoEm == null)
                .OrderByDescending(a => a.CriadoEm)
                .Select(a => a.StudyInstanceUID)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(studyReal) && studyReal != sol.StudyInstanceUID)
                imagens = await imagensReader.ObterImagensAsync(studyReal, MaxImagens, cancellationToken);
        }

        var (nome, cpf, cns, nascimento) = await ResolverPacienteAsync(sol.Solicitacao!.PacienteId, cancellationToken);

        var laudo = await laudos.ObterPorStudyAsync(sol.StudyInstanceUID, cancellationToken);
        var incluiLaudo = laudo is { Status: StatusLaudo.Finalizado };

        var capa = new ExameCompletoCapa(
            PacienteNome: nome,
            PacienteCpf: cpf,
            PacienteCns: cns,
            PacienteNascimento: nascimento,
            ExameNome: sol.TipoExame?.Nome ?? "Exame de imagem",
            Modalidade: sol.TipoExame?.ModalidadeDicom.ToString(),
            Unidade: sol.Solicitacao!.UnidadeExecutante?.Nome,
            UnidadeSolicitante: sol.Solicitacao!.UnidadeSolicitante?.Nome,
            Accession: sol.AccessionNumber,
            RealizadoEm: FusoBrasilia.ParaExibicao(sol.RealizadoEm),
            SolicitadaEm: FusoBrasilia.ParaExibicao(sol.CriadoEm),
            SolicitanteNome: string.IsNullOrWhiteSpace(sol.Solicitacao!.SolicitanteNome) ? null : sol.Solicitacao!.SolicitanteNome,
            IncluiLaudo: incluiLaudo,
            Justificativa: string.IsNullOrWhiteSpace(sol.Solicitacao!.Justificativa) ? null : sol.Solicitacao!.Justificativa,
            Observacoes: string.IsNullOrWhiteSpace(sol.Solicitacao!.Observacoes) ? null : sol.Solicitacao!.Observacoes);

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

}
