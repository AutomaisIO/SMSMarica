using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Pacientes.Fhir;
using SMSMarica.Core.Tratamentos.Dtos;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Tratamentos;

public sealed class TratamentosService(SmsMaricaDbContext db, IPacienteResolver resolver, Faturamento.IFaturamentoService faturamento) : ITratamentosService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IPacienteResolver _resolver = resolver;

    // Resolve o nome do paciente (hub FHIR) e embute nos itens da listagem.
    private async Task<IReadOnlyList<TratamentoListItemDto>> EnriquecerAsync(
        List<TratamentoListItemDto> dtos, CancellationToken ct)
    {
        var nomes = await _resolver.ResolverManyAsync(dtos.Select(d => d.PacienteId), ct);
        return [.. dtos.Select(d => nomes.TryGetValue(d.PacienteId, out var r)
            ? d with { PacienteNome = r.Nome } : d)];
    }

    public async Task<IReadOnlyList<TratamentoListItemDto>> ListarAsync(CancellationToken cancellationToken = default)
    {
        var tratamentos = await QueryListarBase()
            .OrderByDescending(t => t.Ativo)
            .ThenByDescending(t => t.CriadoEm)
            .ToListAsync(cancellationToken);

        return await EnriquecerAsync([.. tratamentos.Select(ParaListItem)], cancellationToken);
    }

    public async Task<IReadOnlyList<TratamentoListItemDto>> ListarPorPacienteAsync(Guid pacienteId, CancellationToken cancellationToken = default)
    {
        var tratamentos = await QueryListarBase()
            .Where(t => t.PacienteId == pacienteId)
            .OrderByDescending(t => t.Ativo)
            .ThenByDescending(t => t.CriadoEm)
            .ToListAsync(cancellationToken);

        return await EnriquecerAsync([.. tratamentos.Select(ParaListItem)], cancellationToken);
    }

    public async Task<IReadOnlyList<TratamentoListItemDto>> ListarPorUnidadeAsync(Guid unidadeId, CancellationToken cancellationToken = default)
    {
        var tratamentos = await QueryListarBase()
            .Where(t => t.UnidadeId == unidadeId)
            .OrderByDescending(t => t.Ativo)
            .ThenByDescending(t => t.CriadoEm)
            .ToListAsync(cancellationToken);

        return await EnriquecerAsync([.. tratamentos.Select(ParaListItem)], cancellationToken);
    }

    public async Task<TratamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos.AsNoTracking()
            .Include(x => x.Unidade)
            .Include(x => x.TipoTratamento)
            .Include(x => x.Periodicidade)
            .Include(x => x.Sessoes)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), id);

        var sessaoIds = t.Sessoes.Select(s => s.Id).ToList();

        var alocacoesQuery =
            from a in _db.Alocacoes.AsNoTracking()
            join rota in _db.Rotas.AsNoTracking() on a.RotaDiariaId equals rota.Id
            join assento in _db.Assentos.AsNoTracking() on a.AssentoId equals assento.Id
            join fileira in _db.Fileiras.AsNoTracking() on assento.FileiraId equals fileira.Id
            where sessaoIds.Contains(a.SessaoId) && rota.Status != StatusRota.Cancelada
            select new TratamentosMapper.AlocacaoAtiva(
                a.SessaoId, rota.Id, rota.Data, fileira.Ordem, assento.Numero);

        var alocacoes = await alocacoesQuery.ToListAsync(cancellationToken);
        var alocacoesPorSessao = alocacoes.ToDictionary(a => a.SessaoId, a => a);

        var dto = TratamentosMapper.ParaDto(t, alocacoesPorSessao);
        var resumo = await _resolver.ResolverAsync(dto.PacienteId, cancellationToken);
        return resumo is null ? dto : dto with { PacienteNome = resumo.Nome };
    }

    public async Task<IReadOnlyList<TipoTratamentoDto>> ListarTiposAsync(CancellationToken cancellationToken = default)
    {
        var tipos = await _db.TiposTratamento.AsNoTracking()
            .Where(t => t.Ativo)
            .OrderBy(t => t.Nome)
            .ToListAsync(cancellationToken);
        return [.. tipos.Select(TratamentosMapper.ParaTipoDto)];
    }

    public IReadOnlyList<DateOnly> ExpandirPeriodicidade(ExpandirPeriodicidadeRequest request) =>
        ExpansorDePeriodicidade.Expandir(request);

    public async Task<Guid> CadastrarAsync(CadastrarTratamentoRequest request, CancellationToken cancellationToken = default)
    {
        // PacienteId referencia o hub FHIR — validação de existência fica a cargo do hub.
        if (!await _db.Unidades.AsNoTracking().AnyAsync(u => u.Id == request.UnidadeId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(Unidade), request.UnidadeId);
        }

        if (request.TipoTratamentoId is { } tipoId &&
            !await _db.TiposTratamento.AsNoTracking().AnyAsync(x => x.Id == tipoId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(TipoTratamento), tipoId);
        }

        // Datas a gravar: respeita o que o operador enviou (inclusive ajustes
        // manuais na prévia). Se vazio, reexpande do servidor para não deixar
        // cadastro sem sessões.
        var datas = NormalizarDatas(request.Datas);
        if (datas.Count == 0)
        {
            datas = [.. ExpansorDePeriodicidade.Expandir(new ExpandirPeriodicidadeRequest(
                request.Periodicidade.Tipo,
                request.Periodicidade.IntervaloDias,
                request.Periodicidade.DiasSemanaMascara,
                request.Periodicidade.DataInicio,
                request.Periodicidade.QuantidadeSessoes))];
        }

        if (datas.Count == 0)
        {
            throw new ValidacaoException("tratamento.sem_sessoes",
                "Nenhuma sessão foi gerada. Informe ao menos uma data.");
        }
        if (datas.Count > ExpansorDePeriodicidade.LimiteDeSessoes)
        {
            throw new ValidacaoException("tratamento.sessoes_demais",
                $"Limite de {ExpansorDePeriodicidade.LimiteDeSessoes} sessões por tratamento.");
        }

        var agora = DateTime.UtcNow;
        var tratamentoId = Guid.CreateVersion7();

        var tratamento = new Tratamento
        {
            Id = tratamentoId,
            PacienteId = request.PacienteId,
            UnidadeId = request.UnidadeId,
            TipoTratamentoId = request.TipoTratamentoId,
            Descricao = request.Descricao.Trim(),
            CodigoSusLiberacao = string.IsNullOrWhiteSpace(request.CodigoSusLiberacao) ? null : request.CodigoSusLiberacao.Trim(),
            Observacoes = string.IsNullOrWhiteSpace(request.Observacoes) ? null : request.Observacoes.Trim(),
            HoraPrevistaBusca = request.HoraPrevistaBusca,
            Ativo = true,
            CriadoEm = agora,
            Periodicidade = new Periodicidade
            {
                Id = Guid.CreateVersion7(),
                TratamentoId = tratamentoId,
                Tipo = request.Periodicidade.Tipo,
                IntervaloDias = request.Periodicidade.IntervaloDias,
                DiasSemanaMascara = request.Periodicidade.DiasSemanaMascara,
                DataInicio = request.Periodicidade.DataInicio,
                QuantidadeSessoes = datas.Count,
                CriadoEm = agora,
            },
            Sessoes = [.. datas.Select(data => new SessaoDeTratamento
            {
                Id = Guid.CreateVersion7(),
                TratamentoId = tratamentoId,
                DataPrevista = data,
                HoraPrevistaBusca = request.HoraPrevistaBusca,
                Status = StatusSessao.Pendente,
                CriadoEm = agora,
            })],
        };

        _db.Tratamentos.Add(tratamento);
        await _db.SaveChangesAsync(cancellationToken);
        return tratamento.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarTratamentoRequest request, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), id);

        if (!t.Ativo)
        {
            throw new ConflitoException("tratamento.encerrado", "Tratamento encerrado não pode ser alterado.");
        }

        if (request.TipoTratamentoId is { } tipoId &&
            !await _db.TiposTratamento.AsNoTracking().AnyAsync(x => x.Id == tipoId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(TipoTratamento), tipoId);
        }

        t.Descricao = request.Descricao.Trim();
        t.TipoTratamentoId = request.TipoTratamentoId;
        t.CodigoSusLiberacao = string.IsNullOrWhiteSpace(request.CodigoSusLiberacao) ? null : request.CodigoSusLiberacao.Trim();
        t.Observacoes = string.IsNullOrWhiteSpace(request.Observacoes) ? null : request.Observacoes.Trim();
        t.HoraPrevistaBusca = request.HoraPrevistaBusca;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task EncerrarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), id);

        if (!t.Ativo)
        {
            throw new ConflitoException("tratamento.ja_encerrado", "Tratamento já está encerrado.");
        }

        t.Ativo = false;
        t.EncerradoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AdicionarSessaoAsync(Guid tratamentoId, AdicionarSessaoRequest request, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos.FirstOrDefaultAsync(x => x.Id == tratamentoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), tratamentoId);

        if (!t.Ativo)
        {
            throw new ConflitoException("tratamento.encerrado", "Tratamento encerrado não aceita novas sessões.");
        }

        var sessao = new SessaoDeTratamento
        {
            Id = Guid.CreateVersion7(),
            TratamentoId = tratamentoId,
            DataPrevista = request.DataPrevista,
            HoraPrevistaBusca = request.HoraPrevistaBusca ?? t.HoraPrevistaBusca,
            HoraPrevistaRetorno = request.HoraPrevistaRetorno,
            Status = StatusSessao.Pendente,
            CriadoEm = DateTime.UtcNow,
        };

        _db.Sessoes.Add(sessao);
        await _db.SaveChangesAsync(cancellationToken);
        return sessao.Id;
    }

    public async Task AtualizarSessaoAsync(Guid tratamentoId, Guid sessaoId, AtualizarSessaoRequest request, CancellationToken cancellationToken = default)
    {
        var sessao = await _db.Sessoes.FirstOrDefaultAsync(s => s.Id == sessaoId && s.TratamentoId == tratamentoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);

        GarantirEditavel(sessao);

        sessao.DataPrevista = request.DataPrevista;
        sessao.HoraPrevistaBusca = request.HoraPrevistaBusca;
        sessao.HoraPrevistaRetorno = request.HoraPrevistaRetorno;
        sessao.Observacoes = string.IsNullOrWhiteSpace(request.Observacoes) ? null : request.Observacoes.Trim();
        sessao.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelarSessaoAsync(Guid tratamentoId, Guid sessaoId, CancellationToken cancellationToken = default)
    {
        var sessao = await _db.Sessoes.FirstOrDefaultAsync(s => s.Id == sessaoId && s.TratamentoId == tratamentoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);

        if (sessao.Status == StatusSessao.Realizada)
        {
            throw new ConflitoException("sessao.imutavel",
                "Sessão realizada não pode ser cancelada.");
        }

        sessao.Status = StatusSessao.Cancelada;
        sessao.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ConfirmarSessaoAsync(Guid tratamentoId, Guid sessaoId, ConfirmarSessaoRequest request, CancellationToken cancellationToken = default)
    {
        var sessao = await _db.Sessoes.FirstOrDefaultAsync(s => s.Id == sessaoId && s.TratamentoId == tratamentoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);

        if (sessao.Status == StatusSessao.Cancelada)
        {
            throw new ConflitoException("sessao.cancelada", "Sessão cancelada não pode ser confirmada.");
        }

        sessao.Status = request.Realizada ? StatusSessao.Realizada : StatusSessao.NaoRealizada;
        sessao.RealizadaEm = DateTime.UtcNow;
        sessao.NomeAcompanhante = Trim(request.NomeAcompanhante);
        sessao.ParentescoAcompanhante = Trim(request.ParentescoAcompanhante);
        sessao.MotoristaIdaId = request.MotoristaIdaId;
        sessao.VeiculoIdaId = request.VeiculoIdaId;
        sessao.HoraSaidaResidencia = request.HoraSaidaResidencia;
        sessao.HoraChegadaUnidade = request.HoraChegadaUnidade;
        sessao.MotoristaVoltaId = request.MotoristaVoltaId;
        sessao.VeiculoVoltaId = request.VeiculoVoltaId;
        sessao.HoraSaidaUnidade = request.HoraSaidaUnidade;
        sessao.HoraChegadaResidencia = request.HoraChegadaResidencia;
        sessao.MotivoNaoRealizacao = Trim(request.MotivoNaoRealizacao);
        sessao.Observacoes = Trim(request.Observacoes);
        sessao.AtualizadoEm = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        // Auto-contabiliza o faturamento TFD da sessão realizada (best-effort — não bloqueia a conclusão).
        if (request.Realizada)
        {
            try { await faturamento.ContabilizarAsync(sessaoId, cancellationToken); }
            catch (Exception) { /* faturamento é best-effort; falha aqui não impede confirmar a sessão */ }
        }
    }

    private static void GarantirEditavel(SessaoDeTratamento s)
    {
        // Regra: sessão realizada é imutável (datas e horários previstos
        // deixaram de fazer sentido). Cancelada também não edita.
        if (s.Status == StatusSessao.Realizada)
        {
            throw new ConflitoException("sessao.imutavel",
                "Sessão já realizada — alterações bloqueadas. Se for correção de dados da realização, use confirmar novamente.");
        }
    }

    private IQueryable<Tratamento> QueryListarBase() => _db.Tratamentos.AsNoTracking()
        .Include(t => t.Unidade)
        .Include(t => t.TipoTratamento)
        .Include(t => t.Sessoes);

    private static TratamentoListItemDto ParaListItem(Tratamento t)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var proxima = t.Sessoes
            .Where(s => s.Status != StatusSessao.Cancelada && s.DataPrevista >= hoje)
            .OrderBy(s => s.DataPrevista)
            .FirstOrDefault()?.DataPrevista;
        var realizadas = t.Sessoes.Count(s => s.Status == StatusSessao.Realizada);
        return new TratamentoListItemDto(
            t.Id,
            t.PacienteId,
            string.Empty,
            t.UnidadeId,
            t.Unidade?.Nome ?? string.Empty,
            t.TipoTratamento?.Nome,
            t.Descricao,
            proxima,
            t.Sessoes.Count,
            realizadas,
            t.Ativo);
    }

    private static List<DateOnly> NormalizarDatas(IReadOnlyList<DateOnly>? datas)
    {
        if (datas is null || datas.Count == 0) return [];
        return [.. datas.Distinct().OrderBy(d => d)];
    }

    private static string? Trim(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
