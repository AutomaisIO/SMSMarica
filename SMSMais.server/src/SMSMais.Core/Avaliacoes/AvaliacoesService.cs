using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Avaliacoes.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Avaliacoes;

/// <summary>
/// Notas dos pacientes para o translado. O paciente avalia pelo app do cidadão a viagem que aconteceu
/// (uma vez, até <see cref="DiasParaAvaliar"/> dias depois); a equipe também pode registrar pelo painel.
/// A avaliação guarda quem levou, em qual veículo e rota — é assim que o painel lê as notas por motorista
/// e por veículo, mesmo que a sessão seja corrigida depois.
/// </summary>
public sealed class AvaliacoesService(SmsMaisDbContext db, IPacienteResolver pacienteResolver) : IAvaliacoesService
{
    public const int DiasParaAvaliar = 30;
    public const int TamanhoComentarioApp = 500;

    private readonly SmsMaisDbContext _db = db;

    public async Task<IReadOnlyList<AvaliacaoListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var linhas = await (
            from a in _db.Avaliacoes.AsNoTracking()
            join s in _db.Sessoes.AsNoTracking() on a.SessaoId equals s.Id
            join t in _db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            join m in _db.Motoristas.AsNoTracking() on a.MotoristaId equals (Guid?)m.Id into ms
            from m in ms.DefaultIfEmpty()
            join v in _db.Veiculos.AsNoTracking() on a.VeiculoId equals (Guid?)v.Id into vs
            from v in vs.DefaultIfEmpty()
            orderby a.CriadoEm descending
            select new
            {
                Avaliacao = a,
                s.DataPrevista,
                t.PacienteId,
                Motorista = m == null ? null : m.Usuario.NomeCompleto,
                Placa = v == null ? null : v.Placa,
            }).ToListAsync(cancellationToken);

        IReadOnlyDictionary<Guid, PacienteResumo> nomes;
        try
        {
            nomes = await pacienteResolver.ResolverManyAsync(linhas.Select(l => l.PacienteId).Distinct(), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            nomes = new Dictionary<Guid, PacienteResumo>(); // hub fora do ar: a lista sai sem o nome
        }

        return [.. linhas.Select(l => AvaliacoesMapper.ParaListItem(l.Avaliacao) with
        {
            DataViagem = l.DataPrevista,
            PacienteNome = nomes.TryGetValue(l.PacienteId, out var r) ? r.Nome : null,
            MotoristaNome = l.Motorista,
            VeiculoPlaca = l.Placa,
        })];
    }

    public async Task<AvaliacaoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await _db.Avaliacoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Avaliacao), id);
        return AvaliacoesMapper.ParaDto(a);
    }

    public async Task<Guid> RegistrarAsync(RegistrarAvaliacaoRequest request, CancellationToken cancellationToken = default)
    {
        var sessao = await _db.Sessoes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SessaoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), request.SessaoId);

        var jaAvaliada = await _db.Avaliacoes.AsNoTracking()
            .AnyAsync(a => a.SessaoId == request.SessaoId, cancellationToken);
        if (jaAvaliada)
        {
            throw new ConflitoException("avaliacao.ja_registrada", "Esta sessão já foi avaliada.");
        }

        var a = await NovaAsync(sessao, request.Nota, request.Comentario, CanalAvaliacao.Painel, cancellationToken);
        _db.Avaliacoes.Add(a);
        await _db.SaveChangesAsync(cancellationToken);
        return a.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarAvaliacaoRequest request, CancellationToken cancellationToken = default)
    {
        var a = await _db.Avaliacoes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Avaliacao), id);
        // A nota que o paciente deu é dele: a equipe pode excluir (um comentário ofensivo, por exemplo),
        // mas não reescrever.
        if (a.Canal == CanalAvaliacao.AppCidadao)
            throw new ConflitoException("avaliacao.do_paciente", "A avaliação dada pelo paciente no app não se edita.");

        a.Nota = request.Nota;
        a.Comentario = string.IsNullOrWhiteSpace(request.Comentario) ? null : request.Comentario.Trim();

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeletarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await _db.Avaliacoes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Avaliacao), id);

        _db.Avaliacoes.Remove(a);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ViagemParaAvaliarDto>> ViagensParaAvaliarAsync(
        Guid pacienteId, CancellationToken cancellationToken = default)
    {
        var desde = FusoBrasilia.HojeEmBrasilia().AddDays(-DiasParaAvaliar);
        var viagens = await (
            from s in _db.Sessoes.AsNoTracking()
            join t in _db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            join u in _db.UnidadesAtendimento.AsNoTracking() on t.UnidadeAtendimentoId equals u.Id
            join m in _db.Motoristas.AsNoTracking() on s.MotoristaIdaId equals (Guid?)m.Id into ms
            from m in ms.DefaultIfEmpty()
            join a in _db.Avaliacoes.AsNoTracking() on s.Id equals a.SessaoId into avs
            from a in avs.DefaultIfEmpty()
            where t.PacienteId == pacienteId && s.Status == StatusSessao.Realizada && s.DataPrevista >= desde
            orderby s.DataPrevista descending
            select new
            {
                s.Id, s.DataPrevista, Destino = u.Nome,
                Motorista = m == null ? null : m.Usuario.NomeCompleto,
                Nota = a == null ? (int?)null : a.Nota,
                Comentario = a == null ? null : a.Comentario,
            }).Take(20).ToListAsync(cancellationToken);

        return [.. viagens.Select(v => new ViagemParaAvaliarDto(
            v.Id, v.DataPrevista, v.Destino, PrimeiroNome(v.Motorista), v.Nota, v.Comentario))];
    }

    public async Task<Guid> AvaliarPeloPacienteAsync(
        Guid pacienteId, Guid sessaoId, AvaliarViagemRequest request, CancellationToken cancellationToken = default)
    {
        // Sessão de outro paciente responde como inexistente: o app não confirma que ela existe.
        var sessao = await _db.Sessoes.AsNoTracking().Include(s => s.Tratamento)
            .FirstOrDefaultAsync(s => s.Id == sessaoId && s.Tratamento!.PacienteId == pacienteId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);

        if (request.Nota is < 1 or > 5)
            throw new ValidacaoException("nota", "Escolha uma nota de 1 a 5.");
        if (request.Comentario?.Trim().Length > TamanhoComentarioApp)
            throw new ValidacaoException("comentario", $"O comentário pode ter até {TamanhoComentarioApp} caracteres.");
        if (sessao.Status != StatusSessao.Realizada)
            throw new ConflitoException("avaliacao.viagem_nao_realizada", "Só dá para avaliar uma viagem que aconteceu.");
        if (sessao.DataPrevista < FusoBrasilia.HojeEmBrasilia().AddDays(-DiasParaAvaliar))
            throw new ConflitoException("avaliacao.prazo", $"O prazo para avaliar esta viagem ({DiasParaAvaliar} dias) acabou.");
        if (await _db.Avaliacoes.AnyAsync(a => a.SessaoId == sessaoId, cancellationToken))
            throw new ConflitoException("avaliacao.ja_registrada", "Você já avaliou esta viagem.");

        var a = await NovaAsync(sessao, request.Nota, request.Comentario, CanalAvaliacao.AppCidadao, cancellationToken);
        _db.Avaliacoes.Add(a);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Dois toques no "Enviar" ao mesmo tempo: a primeira vale.
            throw new ConflitoException("avaliacao.ja_registrada", "Você já avaliou esta viagem.");
        }
        return a.Id;
    }

    /// <summary>A avaliação com a viagem como ela foi: motorista e veículo da ida e a rota em que a
    /// sessão estava (a não cancelada mais recente).</summary>
    private async Task<Avaliacao> NovaAsync(
        SessaoDeTratamento sessao, int nota, string? comentario, CanalAvaliacao canal, CancellationToken ct)
    {
        var rotaId = await (
            from al in _db.Alocacoes.AsNoTracking()
            where al.SessaoId == sessao.Id
            join r in _db.Rotas.AsNoTracking() on al.RotaDiariaId equals r.Id
            where r.Status != StatusRota.Cancelada
            orderby r.CriadoEm descending
            select (Guid?)r.Id).FirstOrDefaultAsync(ct);

        return new Avaliacao
        {
            Id = Guid.CreateVersion7(),
            SessaoId = sessao.Id,
            Nota = nota,
            Comentario = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim(),
            CriadoEm = DateTime.UtcNow,
            Canal = canal,
            MotoristaId = sessao.MotoristaIdaId,
            VeiculoId = sessao.VeiculoIdaId,
            RotaId = rotaId,
        };
    }

    private static string? PrimeiroNome(string? nome) =>
        string.IsNullOrWhiteSpace(nome) ? null : nome.Trim().Split(' ', 2)[0];
}
