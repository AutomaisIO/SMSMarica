using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Anamneses.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMarica.Core.Anamneses;

public sealed class AnamnesesService(
    SmsMaisDbContext db,
    IPacienteResolver pacienteResolver,
    IUsuarioAtualAccessor usuarioAtual) : IAnamnesesService
{
    public async Task<AnamneseContextoDto> ObterContextoAsync(
        Guid? solicitacaoExameId, string? accessionNumber, CancellationToken cancellationToken = default)
    {
        if (solicitacaoExameId is null && string.IsNullOrWhiteSpace(accessionNumber))
            throw new ValidacaoException("anamnese.sem_vinculo",
                "Informe solicitacaoExameId ou accessionNumber.");

        IQueryable<ExameImagem> query = db.ExamesImagem.AsNoTracking()
            .Include(s => s.TipoExame)
            .Include(s => s.Solicitacao)
            .Where(s => s.ExcluidoEm == null);

        query = solicitacaoExameId is not null
            ? query.Where(s => s.Id == solicitacaoExameId)
            : query.Where(s => s.AccessionNumber == accessionNumber!.Trim());

        var sol = await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem),
                solicitacaoExameId?.ToString() ?? accessionNumber!);

        var anamnese = await db.Anamneses.AsNoTracking()
            .Where(a => a.ExameImagemId == sol.Id && a.ExcluidoEm == null)
            .FirstOrDefaultAsync(cancellationToken);

        var paciente = await pacienteResolver.ResolverAsync(sol.Solicitacao!.PacienteId, cancellationToken);

        return new AnamneseContextoDto(
            sol.Id,
            sol.AccessionNumber,
            sol.TipoExame?.Nome ?? string.Empty,
            sol.TipoExame?.ModalidadeDicom.ToString() ?? string.Empty,
            sol.Solicitacao!.PacienteId,
            paciente?.Nome ?? string.Empty,
            paciente?.Cpf,
            paciente?.Cns,
            paciente?.DataNascimento,
            anamnese is null ? null : ParaDto(anamnese));
    }

    public async Task<AnamneseDto> SalvarAsync(
        Guid solicitacaoExameId, SalvarAnamneseDto dto, CancellationToken cancellationToken = default)
    {
        var solExiste = await db.ExamesImagem.AsNoTracking()
            .AnyAsync(s => s.Id == solicitacaoExameId && s.ExcluidoEm == null, cancellationToken);
        if (!solExiste)
            throw new NaoEncontradoException(nameof(ExameImagem), solicitacaoExameId);

        var agora = DateTime.UtcNow;
        var usuarioId = usuarioAtual.UsuarioId;
        var nomeUsuario = usuarioId is null
            ? null
            : await db.Usuarios.AsNoTracking()
                .Where(u => u.Id == usuarioId)
                .Select(u => u.NomeCompleto)
                .FirstOrDefaultAsync(cancellationToken);

        var anamnese = await db.Anamneses
            .FirstOrDefaultAsync(a => a.ExameImagemId == solicitacaoExameId && a.ExcluidoEm == null, cancellationToken);

        if (anamnese is null)
        {
            anamnese = new Anamnese
            {
                Id = Guid.CreateVersion7(),
                ExameImagemId = solicitacaoExameId,
                Tipo = dto.Tipo,
                Versao = dto.Versao,
                ConteudoJson = dto.ConteudoJson,
                ClassificacaoRisco = dto.ClassificacaoRisco,
                PreenchidoPorUsuarioId = usuarioId,
                PreenchidoPorNome = nomeUsuario,
                CriadoEm = agora,
                CriadoPor = usuarioId,
            };
            db.Anamneses.Add(anamnese);
        }
        else
        {
            anamnese.Tipo = dto.Tipo;
            anamnese.Versao = dto.Versao;
            anamnese.ConteudoJson = dto.ConteudoJson;
            anamnese.ClassificacaoRisco = dto.ClassificacaoRisco;
            anamnese.PreenchidoPorUsuarioId = usuarioId ?? anamnese.PreenchidoPorUsuarioId;
            anamnese.PreenchidoPorNome = nomeUsuario ?? anamnese.PreenchidoPorNome;
            anamnese.AtualizadoEm = agora;
            anamnese.AtualizadoPor = usuarioId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ParaDto(anamnese);
    }

    private static AnamneseDto ParaDto(Anamnese a) => new(
        a.Id,
        a.ExameImagemId,
        a.Tipo,
        a.Versao,
        a.ConteudoJson,
        a.ClassificacaoRisco,
        a.PreenchidoPorNome,
        a.CriadoEm,
        a.AtualizadoEm);
}
