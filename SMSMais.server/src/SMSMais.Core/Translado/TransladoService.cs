using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Translado.Dtos;
using SMSMais.Core.Translado.Geracao;
using SMSMais.Core.Translado.Lotacao;
using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado;

public sealed class TransladoService(SmsMaisDbContext db, Pacientes.Fhir.IPacienteResolver pacienteResolver) : ITransladoService
{
    private readonly SmsMaisDbContext _db = db;
    private readonly Pacientes.Fhir.IPacienteResolver _pacienteResolver = pacienteResolver;

    public async Task<IReadOnlyList<RotaDiariaListItemDto>> ListarAsync(
        DateOnly? data,
        Guid? motoristaId,
        Guid? veiculoId,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Rotas.AsNoTracking()
            .Include(r => r.Veiculo)
            .Include(r => r.Motorista).ThenInclude(m => m!.Usuario)
            .AsQueryable();

        if (data is not null) query = query.Where(r => r.Data == data.Value);
        if (motoristaId is not null) query = query.Where(r => r.MotoristaId == motoristaId.Value);
        if (veiculoId is not null) query = query.Where(r => r.VeiculoId == veiculoId.Value);

        var rotas = await query
            .OrderByDescending(r => r.Data)
            .ThenBy(r => r.CriadoEm)
            .ToListAsync(cancellationToken);

        var ids = rotas.Select(r => r.Id).ToList();
        var contagem = await _db.Alocacoes.AsNoTracking()
            .Where(a => ids.Contains(a.RotaDiariaId))
            .GroupBy(a => a.RotaDiariaId)
            .Select(g => new { RotaId = g.Key, Total = g.Count() })
            .ToDictionaryAsync(x => x.RotaId, x => x.Total, cancellationToken);

        return [.. rotas.Select(r => TransladoMapper.ParaListItem(r, contagem.GetValueOrDefault(r.Id)))];
    }

    public async Task<RotaDiariaDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await _db.Rotas.AsNoTracking()
            .Include(x => x.Veiculo)
            .Include(x => x.Motorista).ThenInclude(m => m!.Usuario)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), id);

        var alocacoes = await CarregarAlocacoesAsync(id, cancellationToken);
        return TransladoMapper.ParaDto(r, alocacoes);
    }

    public async Task<Guid> CadastrarAsync(CadastrarRotaRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _db.Veiculos.AsNoTracking().AnyAsync(v => v.Id == request.VeiculoId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(Veiculo), request.VeiculoId);
        }

        if (!await _db.Motoristas.AsNoTracking().AnyAsync(m => m.Id == request.MotoristaId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(Motorista), request.MotoristaId);
        }

        var r = new RotaDiaria
        {
            Id = Guid.CreateVersion7(),
            Data = request.Data,
            VeiculoId = request.VeiculoId,
            MotoristaId = request.MotoristaId,
            Status = StatusRota.Planejada,
            CriadoEm = DateTime.UtcNow,
        };

        _db.Rotas.Add(r);
        await _db.SaveChangesAsync(cancellationToken);
        return r.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarRotaRequest request, CancellationToken cancellationToken = default)
    {
        var r = await _db.Rotas
            .Include(x => x.Alocacoes)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), id);

        if (r.Status != StatusRota.Planejada)
        {
            throw new ConflitoException(
                "rota.nao_editavel",
                "Só é possível alterar rota em estado Planejada.");
        }

        if (!await _db.Veiculos.AsNoTracking().AnyAsync(v => v.Id == request.VeiculoId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(Veiculo), request.VeiculoId);
        }

        if (!await _db.Motoristas.AsNoTracking().AnyAsync(m => m.Id == request.MotoristaId, cancellationToken))
        {
            throw new NaoEncontradoException(nameof(Motorista), request.MotoristaId);
        }

        // Trocar veículo invalida os assentos já alocados — remove tudo.
        if (request.VeiculoId != r.VeiculoId && r.Alocacoes.Count > 0)
        {
            _db.Alocacoes.RemoveRange(r.Alocacoes);
        }

        r.Data = request.Data;
        r.VeiculoId = request.VeiculoId;
        r.MotoristaId = request.MotoristaId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task IniciarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await _db.Rotas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), id);

        if (r.Status != StatusRota.Planejada)
        {
            throw new ConflitoException("rota.estado_invalido", $"Não é possível iniciar rota no estado {r.Status}.");
        }

        r.Status = StatusRota.EmAndamento;
        r.IniciadaEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ConcluirAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await _db.Rotas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), id);

        if (r.Status != StatusRota.EmAndamento)
        {
            throw new ConflitoException("rota.estado_invalido", $"Não é possível concluir rota no estado {r.Status}.");
        }

        r.Status = StatusRota.Concluida;
        r.ConcluidaEm = DateTime.UtcNow;
        // Como no tablet: quem a rota ia resgatar e não buscou volta para a lista de resgate.
        await Resgate.ResgateService.LiberarAsync(_db, id, null, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var r = await _db.Rotas.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), id);

        if (r.Status is StatusRota.Concluida or StatusRota.Cancelada)
        {
            throw new ConflitoException("rota.estado_invalido", $"Rota já está em estado {r.Status}.");
        }

        r.Status = StatusRota.Cancelada;
        // Quem esta rota ia resgatar e ainda não buscou volta para a lista de resgate.
        await Resgate.ResgateService.LiberarAsync(_db, id, null, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SessaoElegivelDto>> ListarSessoesElegiveisAsync(
        Guid rotaId, CancellationToken cancellationToken = default)
    {
        var rota = await _db.Rotas.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rotaId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), rotaId);

        var dataRota = rota.Data;

        // Sessões pendentes/confirmadas com DataPrevista ≤ dataRota,
        // cujo tratamento está ativo, e que NÃO estão em nenhuma Alocacao
        // de rota não-cancelada.
        var query =
            from s in _db.Sessoes.AsNoTracking()
            join t in _db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            join u in _db.UnidadesAtendimento.AsNoTracking() on t.UnidadeAtendimentoId equals u.Id
            where t.Ativo
                && (s.Status == StatusSessao.Pendente || s.Status == StatusSessao.Confirmada)
                && s.DataPrevista <= dataRota
                && !_db.Alocacoes.AsNoTracking()
                    .Any(a => a.SessaoId == s.Id
                        && _db.Rotas.Any(r => r.Id == a.RotaDiariaId && r.Status != StatusRota.Cancelada))
            orderby s.DataPrevista, s.HoraPrevistaBusca
            select new
            {
                s.Id,
                s.TratamentoId,
                PacienteId = t.PacienteId,
                // Nome do paciente vive no hub FHIR — resolver via API. TODO.
                PacienteNome = "",
                UnidadeId = u.Id,
                UnidadeNome = u.Nome,
                s.DataPrevista,
                s.HoraPrevistaBusca,
                s.Status,
                t.Mobilidade,
                t.DificuldadeVeiculoAlto,
                t.Isolamento,
                t.UsaOxigenio,
                t.NecessitaAjuda,
                t.AjudaDescricao,
                AcompanhantesEscolhidos = s.Acompanhantes.Count,
                s.AcompanhanteEsperado,
                t.QuantidadeAcompanhantes,
            };

        var lista = await query.ToListAsync(cancellationToken);

        // Necessidades e acompanhantes vão só para quem monta a rota ler — o gerador ainda não usa.
        var dtos = lista.Select(x => new SessaoElegivelDto(
            x.Id, x.TratamentoId,
            x.PacienteId, x.PacienteNome,
            x.UnidadeId, x.UnidadeNome,
            x.DataPrevista, x.HoraPrevistaBusca,
            x.Status,
            x.DataPrevista < dataRota,
            new NecessidadesDto(x.Mobilidade, x.DificuldadeVeiculoAlto, x.Isolamento, x.UsaOxigenio, x.NecessitaAjuda, x.AjudaDescricao),
            x.AcompanhantesEscolhidos > 0 ? x.AcompanhantesEscolhidos : (x.AcompanhanteEsperado == true ? 1 : 0),
            x.QuantidadeAcompanhantes)).ToList();

        // Resolve o nome do paciente pela key (PacienteId = id do Patient no hub FHIR).
        var nomes = await _pacienteResolver.ResolverManyAsync(dtos.Select(d => d.PacienteId), cancellationToken);
        return [.. dtos.Select(d => nomes.TryGetValue(d.PacienteId, out var r)
            ? d with { PacienteNome = r.Nome } : d)];
    }

    public async Task<Guid> CriarAlocacaoAsync(
        Guid rotaId, CriarAlocacaoRequest request, CancellationToken cancellationToken = default)
    {
        var rota = await _db.Rotas
            .FirstOrDefaultAsync(r => r.Id == rotaId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), rotaId);

        if (rota.Status != StatusRota.Planejada)
        {
            throw new ConflitoException(
                "rota.nao_editavel",
                "Só é possível alocar em rota no estado Planejada.");
        }

        var sessao = await _db.Sessoes
            .Include(s => s.Tratamento)
            .Include(s => s.Acompanhantes)
            .FirstOrDefaultAsync(s => s.Id == request.SessaoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), request.SessaoId);

        if (sessao.Tratamento is null || !sessao.Tratamento.Ativo)
        {
            throw new ConflitoException(
                "sessao.tratamento_inativo",
                "Sessão pertence a tratamento encerrado.");
        }

        if (sessao.Status is not (StatusSessao.Pendente or StatusSessao.Confirmada))
        {
            throw new ConflitoException(
                "sessao.estado_invalido",
                $"Sessão no estado {sessao.Status} não pode ser alocada.");
        }

        var jaAlocadaEmOutra = await _db.Alocacoes.AsNoTracking()
            .AnyAsync(a => a.SessaoId == sessao.Id
                && _db.Rotas.Any(r => r.Id == a.RotaDiariaId && r.Status != StatusRota.Cancelada),
                cancellationToken);
        if (jaAlocadaEmOutra)
        {
            throw new ConflitoException(
                "sessao.ja_alocada",
                "Sessão já está alocada em outra rota ativa.");
        }

        var assento = await _db.Assentos.AsNoTracking()
            .Include(a => a.Fileira)
            .FirstOrDefaultAsync(a => a.Id == request.AssentoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Assento), request.AssentoId);

        if (assento.Fileira is null || assento.Fileira.VeiculoId != rota.VeiculoId)
        {
            throw new ConflitoException(
                "assento.veiculo_incorreto",
                "Assento não pertence ao veículo da rota.");
        }

        if (assento.Excluido)
        {
            throw new ConflitoException("assento.excluido", "Assento removido — não pode ser alocado.");
        }

        if (assento.Bloqueado)
        {
            throw new ConflitoException("assento.bloqueado", "Assento bloqueado — não pode ser alocado.");
        }

        if (assento.Tipo == TipoAssento.Motorista)
        {
            throw new ConflitoException(
                "assento.motorista",
                "Assento de motorista não aceita alocação de paciente.");
        }

        // O paciente vai no assento escolhido e cada acompanhante leva outro lugar: a central nunca põe
        // no carro mais gente do que ele leva (decisão de 19/10).
        var lotacao = await LotacaoDaRota.DaIdaAsync(_db, rota, cancellationToken);
        if (lotacao.Livres.All(a => a.Id != assento.Id))
        {
            throw new ConflitoException("assento.ocupado", "Assento já está alocado nesta rota.");
        }

        var t = sessao.Tratamento;
        GarantirCondicao(t, assento, lotacao);
        var acompanhantes = AcomodacaoNoVeiculo.AcompanhantesDaViagem(
            sessao.AcompanhanteEsperado, sessao.Acompanhantes.Count, t.QuantidadeAcompanhantes);
        var restantes = lotacao.Livres.Where(a => a.Id != assento.Id).ToList();
        var assentosAcompanhantes = restantes.Where(a => a.Tipo == TipoAssento.Acompanhante)
            .Concat(restantes.Where(a => a.Tipo == TipoAssento.Passageiro))
            .Take(acompanhantes)
            .ToList();
        if (assentosAcompanhantes.Count < acompanhantes)
        {
            throw new ConflitoException("rota.sem_lugar_acompanhante", acompanhantes == 1
                ? "Não cabe: o paciente vai com 1 acompanhante e não sobra outro lugar livre neste carro."
                : $"Não cabe: o paciente vai com {acompanhantes} acompanhantes e só "
                    + (restantes.Count == 1 ? "sobra 1 lugar livre" : $"sobram {restantes.Count} lugares livres")
                    + " neste carro.");
        }

        // Remarcar sessão atrasada para o dia da rota — sai da fila de pendências.
        if (sessao.DataPrevista < rota.Data)
        {
            sessao.DataPrevista = rota.Data;
            sessao.AtualizadoEm = DateTime.UtcNow;
        }

        var alocacao = new Alocacao
        {
            Id = Guid.CreateVersion7(),
            RotaDiariaId = rotaId,
            SessaoId = sessao.Id,
            AssentoId = assento.Id,
            Tipo = request.Tipo,
            CriadoEm = DateTime.UtcNow,
        };

        _db.Alocacoes.Add(alocacao);
        _db.Alocacoes.AddRange(assentosAcompanhantes.Select(a => new Alocacao
        {
            Id = Guid.CreateVersion7(),
            RotaDiariaId = rotaId,
            SessaoId = sessao.Id,
            AssentoId = a.Id,
            Tipo = TipoAlocacao.Acompanhante,
            CriadoEm = alocacao.CriadoEm,
        }));
        await _db.SaveChangesAsync(cancellationToken);
        return alocacao.Id;
    }

    /// <summary>A condição do paciente na alocação feita à mão (a mesma regra da geração).</summary>
    private static void GarantirCondicao(Tratamento t, Assento assento, OcupacaoDoCarro lotacao)
    {
        if (t.Mobilidade == MobilidadeTransporte.CadeiranteVeiculoAdaptado && assento.Tipo != TipoAssento.Cadeirante)
        {
            throw new ConflitoException("assento.cadeirante",
                "Paciente cadeirante na própria cadeira vai no assento de cadeirante.");
        }
        if (t.Mobilidade == MobilidadeTransporte.Maca
            && (lotacao.Veiculo.Tipo != TipoVeiculo.Ambulancia || lotacao.ABordo.Any(q => q.Mobilidade == MobilidadeTransporte.Maca)))
        {
            throw new ConflitoException("veiculo.maca", lotacao.Veiculo.Tipo == TipoVeiculo.Ambulancia
                ? "A ambulância já leva um paciente de maca."
                : "Paciente de maca só vai de ambulância.");
        }
        if (lotacao.ABordo.Any(q => q.Isolamento) || (t.Isolamento && lotacao.ABordo.Count > 0))
        {
            throw new ConflitoException("rota.isolamento",
                "Paciente em isolamento viaja sozinho (só com os acompanhantes dele): não divide o carro.");
        }
    }

    public async Task RemoverAlocacaoAsync(
        Guid rotaId, Guid alocacaoId, CancellationToken cancellationToken = default)
    {
        var rota = await _db.Rotas.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == rotaId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), rotaId);

        if (rota.Status != StatusRota.Planejada)
        {
            throw new ConflitoException(
                "rota.nao_editavel",
                "Só é possível desalocar em rota no estado Planejada.");
        }

        var alocacao = await _db.Alocacoes
            .FirstOrDefaultAsync(a => a.Id == alocacaoId && a.RotaDiariaId == rotaId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Alocacao), alocacaoId);

        // Sai o paciente com os acompanhantes: tirar só o lugar de um acompanhante deixaria gente sem assento.
        var daSessao = await _db.Alocacoes
            .Where(a => a.RotaDiariaId == rotaId && a.SessaoId == alocacao.SessaoId && a.Parada == alocacao.Parada)
            .ToListAsync(cancellationToken);
        _db.Alocacoes.RemoveRange(daSessao);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AlocacaoDto>> CarregarAlocacoesAsync(
        Guid rotaId, CancellationToken cancellationToken)
    {
        var query =
            from a in _db.Alocacoes.AsNoTracking()
            join assento in _db.Assentos.AsNoTracking() on a.AssentoId equals assento.Id
            join fileira in _db.Fileiras.AsNoTracking() on assento.FileiraId equals fileira.Id
            join s in _db.Sessoes.AsNoTracking() on a.SessaoId equals s.Id
            join t in _db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            join u in _db.UnidadesAtendimento.AsNoTracking() on t.UnidadeAtendimentoId equals u.Id
            where a.RotaDiariaId == rotaId
            orderby fileira.Ordem, assento.Numero
            select new AlocacaoDto(
                a.Id,
                s.Id,
                t.Id,
                t.PacienteId,
                "",
                u.Id,
                u.Nome,
                s.HoraPrevistaBusca,
                assento.Id,
                fileira.Ordem,
                assento.Numero,
                a.Tipo,
                s.Status,
                s.ConfirmadaPorUsuarioId != null,
                a.Parada);

        var dtos = await query.ToListAsync(cancellationToken);

        // Acompanhante sem lugar (o paciente confirmou mais um pelo WhatsApp e o carro estava cheio):
        // aparece no paciente, para a central resolver.
        var sessaoIds = dtos.Select(d => d.SessaoId).Distinct().ToList();
        var esperados = await (
            from s in _db.Sessoes.AsNoTracking()
            where sessaoIds.Contains(s.Id)
            join t in _db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            select new { s.Id, s.AcompanhanteEsperado, Escolhidos = s.Acompanhantes.Count, t.QuantidadeAcompanhantes })
            .ToDictionaryAsync(x => x.Id, x => AcomodacaoNoVeiculo.AcompanhantesDaViagem(
                x.AcompanhanteEsperado, x.Escolhidos, x.QuantidadeAcompanhantes), cancellationToken);
        dtos = [.. dtos.Select(d =>
        {
            if (d.Tipo != TipoAlocacao.Paciente) return d;
            var comLugar = dtos.Count(x => x.SessaoId == d.SessaoId && x.Parada == d.Parada && x.Tipo == TipoAlocacao.Acompanhante);
            var faltam = esperados.GetValueOrDefault(d.SessaoId) - comLugar;
            return faltam > 0 ? d with { AcompanhantesSemLugar = faltam } : d;
        })];
        var nomes = await _pacienteResolver.ResolverManyAsync(dtos.Select(d => d.PacienteId), cancellationToken);
        return [.. dtos.Select(d => nomes.TryGetValue(d.PacienteId, out var r)
            ? d with { PacienteNome = r.Nome } : d)];
    }
}
