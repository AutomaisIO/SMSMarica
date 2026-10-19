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
/// Notas dos pacientes para o motorista do translado. Cada viagem tem duas: a da <b>ida</b> (pedida ao
/// desembarcar na unidade) e a do <b>resgate</b> (ao chegar em casa) — podem ser motoristas diferentes
/// (decisão de 19/10). O paciente avalia pelo app do cidadão (uma vez por trecho, até
/// <see cref="DiasParaAvaliar"/> dias depois); a equipe também pode registrar pelo painel. A avaliação
/// guarda quem dirigiu aquele trecho, em qual veículo e rota — é assim que o painel lê as notas por
/// motorista e por veículo, mesmo que a sessão seja corrigida depois.
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

        var trecho = Enum.IsDefined(request.Trecho) ? request.Trecho : TrechoViagem.Ida;
        var jaAvaliada = await _db.Avaliacoes.AsNoTracking()
            .AnyAsync(a => a.SessaoId == request.SessaoId && a.Trecho == trecho, cancellationToken);
        if (jaAvaliada)
        {
            throw new ConflitoException("avaliacao.ja_registrada", trecho == TrechoViagem.Resgate
                ? "O resgate desta viagem já foi avaliado."
                : "A ida desta viagem já foi avaliada.");
        }

        var a = await NovaAsync(sessao, trecho, request.Nota, request.Comentario, CanalAvaliacao.Painel, cancellationToken);
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
        var sessoes = await (
            from s in _db.Sessoes.AsNoTracking()
            join t in _db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            join u in _db.UnidadesAtendimento.AsNoTracking() on t.UnidadeAtendimentoId equals u.Id
            where t.PacienteId == pacienteId && s.Status == StatusSessao.Realizada && s.DataPrevista >= desde
            orderby s.DataPrevista descending, s.HoraChegadaUnidade descending
            select new
            {
                s.Id, s.DataPrevista, Destino = u.Nome, s.MotoristaIdaId, s.MotoristaVoltaId,
                s.HoraChegadaUnidade, s.HoraChegadaResidencia,
            }).Take(20).ToListAsync(cancellationToken);

        var ids = sessoes.Select(x => x.Id).ToList();
        var notas = await _db.Avaliacoes.AsNoTracking()
            .Where(a => ids.Contains(a.SessaoId))
            .Select(a => new { a.SessaoId, a.Trecho, a.Nota, a.Comentario })
            .ToListAsync(cancellationToken);
        var motoristaIds = sessoes
            .SelectMany(x => new[] { x.MotoristaIdaId, x.MotoristaVoltaId })
            .Where(x => x != null).Select(x => x!.Value).Distinct().ToList();
        var motoristas = await _db.Motoristas.AsNoTracking()
            .Where(m => motoristaIds.Contains(m.Id))
            .Select(m => new { m.Id, Nome = m.Usuario.NomeCompleto })
            .ToDictionaryAsync(m => m.Id, m => m.Nome, cancellationToken);

        var lista = new List<ViagemParaAvaliarDto>();
        foreach (var s in sessoes)
        {
            ViagemParaAvaliarDto Trecho(TrechoViagem trecho, Guid? motoristaId, TimeOnly? hora)
            {
                var n = notas.FirstOrDefault(a => a.SessaoId == s.Id && a.Trecho == trecho);
                var motorista = motoristaId is { } m ? motoristas.GetValueOrDefault(m) : null;
                return new ViagemParaAvaliarDto(
                    s.Id, s.DataPrevista, s.Destino, PrimeiroNome(motorista), n?.Nota, n?.Comentario, trecho, hora);
            }

            // O resgate (chegou em casa) vem antes da ida do mesmo dia: é o mais recente.
            if (s.HoraChegadaResidencia is not null && s.MotoristaVoltaId is not null)
                lista.Add(Trecho(TrechoViagem.Resgate, s.MotoristaVoltaId, s.HoraChegadaResidencia));
            lista.Add(Trecho(TrechoViagem.Ida, s.MotoristaIdaId, s.HoraChegadaUnidade));
        }
        return lista;
    }

    public async Task<Guid> AvaliarPeloPacienteAsync(
        Guid pacienteId, Guid sessaoId, AvaliarViagemRequest request, CancellationToken cancellationToken = default)
    {
        // Sessão de outro paciente responde como inexistente: o app não confirma que ela existe.
        var sessao = await _db.Sessoes.AsNoTracking().Include(s => s.Tratamento)
            .FirstOrDefaultAsync(s => s.Id == sessaoId && s.Tratamento!.PacienteId == pacienteId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);
        var trecho = Enum.IsDefined(request.Trecho) ? request.Trecho : TrechoViagem.Ida;

        if (request.Nota is < 1 or > 5)
            throw new ValidacaoException("nota", "Escolha uma nota de 1 a 5.");
        if (request.Comentario?.Trim().Length > TamanhoComentarioApp)
            throw new ValidacaoException("comentario", $"O comentário pode ter até {TamanhoComentarioApp} caracteres.");
        if (sessao.Status != StatusSessao.Realizada)
            throw new ConflitoException("avaliacao.viagem_nao_realizada", "Só dá para avaliar uma viagem que aconteceu.");
        if (trecho == TrechoViagem.Resgate && sessao.HoraChegadaResidencia is null)
            throw new ConflitoException("avaliacao.resgate_nao_feito", "O resgate se avalia depois de chegar em casa.");
        if (sessao.DataPrevista < FusoBrasilia.HojeEmBrasilia().AddDays(-DiasParaAvaliar))
            throw new ConflitoException("avaliacao.prazo", $"O prazo para avaliar esta viagem ({DiasParaAvaliar} dias) acabou.");
        if (await _db.Avaliacoes.AnyAsync(a => a.SessaoId == sessaoId && a.Trecho == trecho, cancellationToken))
            throw new ConflitoException("avaliacao.ja_registrada", "Você já avaliou este motorista nesta viagem.");

        var a = await NovaAsync(sessao, trecho, request.Nota, request.Comentario, CanalAvaliacao.AppCidadao, cancellationToken);
        _db.Avaliacoes.Add(a);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Dois toques no "Enviar" ao mesmo tempo: a primeira vale.
            throw new ConflitoException("avaliacao.ja_registrada", "Você já avaliou este motorista nesta viagem.");
        }
        return a.Id;
    }

    /// <summary>A avaliação com o trecho como ele foi: o motorista e o veículo daquele trecho e a rota em
    /// que ele aconteceu (a ida na rota da ida; o resgate na rota de quem buscou).</summary>
    private async Task<Avaliacao> NovaAsync(
        SessaoDeTratamento sessao, TrechoViagem trecho, int nota, string? comentario, CanalAvaliacao canal, CancellationToken ct)
    {
        var resgate = trecho == TrechoViagem.Resgate;
        var rotaId = await (
            from al in _db.Alocacoes.AsNoTracking()
            where al.SessaoId == sessao.Id && (resgate ? al.Parada == TipoParada.Retorno : al.Parada != TipoParada.Retorno)
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
            Trecho = trecho,
            MotoristaId = resgate ? sessao.MotoristaVoltaId : sessao.MotoristaIdaId,
            VeiculoId = resgate ? sessao.VeiculoVoltaId : sessao.VeiculoIdaId,
            RotaId = rotaId,
        };
    }

    private static string? PrimeiroNome(string? nome) =>
        string.IsNullOrWhiteSpace(nome) ? null : nome.Trim().Split(' ', 2)[0];
}
