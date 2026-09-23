using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Anamneses.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Anamneses;

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
            anamnese is null ? null : ParaDto(anamnese),
            sol.SiscanProtocolo,
            sol.SiscanNumeroExame);
    }

    public async Task<AnamneseDto> SalvarAsync(
        Guid solicitacaoExameId, SalvarAnamneseDto dto, CancellationToken cancellationToken = default)
    {
        var exame = await db.ExamesImagem.AsNoTracking()
            .Where(s => s.Id == solicitacaoExameId && s.ExcluidoEm == null)
            .Select(s => new { s.SiscanProtocolo })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NaoEncontradoException(nameof(ExameImagem), solicitacaoExameId);

        // Anamnese já enviada ao SISCAN não se altera por aqui. As respostas viraram uma
        // requisição numa base federal; mudá-las do nosso lado criaria duas verdades para o mesmo
        // exame — a nossa e a do Ministério — sem ninguém saber qual vale. A correção é lá, na
        // requisição, que abre editável para quem a criou.
        //
        // A trava mora AQUI, e não só na tela: tela é conveniência, servidor é regra.
        if (exame.SiscanProtocolo is { Length: > 0 } protocolo)
        {
            throw new ConflitoException(
                "anamnese.enviada_ao_siscan",
                $"Esta anamnese já gerou a requisição {protocolo} no SISCAN e não pode mais ser "
                + "alterada aqui. Se algo estiver errado, corrija na própria requisição do SISCAN.");
        }

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
