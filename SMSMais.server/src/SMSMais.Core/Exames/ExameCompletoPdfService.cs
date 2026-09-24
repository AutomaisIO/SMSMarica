using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Pacientes;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Exames;

/// <summary>
/// Gera o PDF do EXAME de uma solicitação: capa rica (dados do paciente, exame,
/// solicitante) seguida das imagens do PACS.
///
/// <para>
/// <b>O laudo NÃO entra aqui</b> (decisão de 24/09/2026). Antes ele era concatenado ao fim,
/// e na versão on-demand "sem assinatura" mesmo quando já havia o PDF assinado — o que fazia
/// circular uma cópia sem validade junto das imagens. O laudo é documento à parte: sai só
/// o oficial, e só depois de aprovado pelo médico (Solicitações → "Ver laudo", app do
/// cidadão e QR Code do rodapé).
/// </para>
/// </summary>
public interface IExameCompletoPdfService
{
    Task<byte[]> GerarAsync(Guid solicitacaoExameId, CancellationToken cancellationToken = default);
}

public sealed class ExameCompletoPdfService(
    Institucional.IInstituicaoService instituicaoService,
    Midias.IMidiasService midiasService,
    SmsMaisDbContext db,
    IExamePacsImagensReader imagensReader,
    IPacientesService pacientes) : IExameCompletoPdfService
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
            Justificativa: string.IsNullOrWhiteSpace(sol.Solicitacao!.Justificativa) ? null : sol.Solicitacao!.Justificativa,
            Observacoes: string.IsNullOrWhiteSpace(sol.Solicitacao!.Observacoes) ? null : sol.Solicitacao!.Observacoes);

        return new ExameCompletoPdfDocument(capa, imagens, await IdentidadeVisualPdf.ResolverAsync(instituicaoService, midiasService, cancellationToken)).Gerar();
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
