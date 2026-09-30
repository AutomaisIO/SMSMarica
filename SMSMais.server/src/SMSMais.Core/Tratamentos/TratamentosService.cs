using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Identidade;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Tratamentos.Dtos;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Tratamentos;

public sealed class TratamentosService(
    SmsMaisDbContext db,
    IPacienteResolver resolver,
    Faturamento.IFaturamentoService faturamento,
    IPacientesService pacientes,
    IUsuarioAtualAccessor usuarioAtual,
    ILogger<TratamentosService> logger) : ITratamentosService
{
    private readonly SmsMaisDbContext _db = db;
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

    public async Task<IReadOnlyList<TratamentoListItemDto>> ListarPorUnidadeAtendimentoAsync(Guid unidadeAtendimentoId, CancellationToken cancellationToken = default)
    {
        var tratamentos = await QueryListarBase()
            .Where(t => t.UnidadeAtendimentoId == unidadeAtendimentoId)
            .OrderByDescending(t => t.Ativo)
            .ThenByDescending(t => t.CriadoEm)
            .ToListAsync(cancellationToken);

        return await EnriquecerAsync([.. tratamentos.Select(ParaListItem)], cancellationToken);
    }

    public async Task<TratamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos.AsNoTracking()
            .Include(x => x.UnidadeAtendimento)
            .Include(x => x.TipoTratamento)
            .Include(x => x.Sessoes).ThenInclude(s => s.Acompanhantes).ThenInclude(a => a.Acompanhante)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), id);

        var sessaoIds = t.Sessoes.Select(s => s.Id).ToList();

        var alocacoesQuery =
            from a in _db.Alocacoes.AsNoTracking()
            join rota in _db.Rotas.AsNoTracking() on a.RotaDiariaId equals rota.Id
            join assento in _db.Assentos.AsNoTracking() on a.AssentoId equals assento.Id
            join fileira in _db.Fileiras.AsNoTracking() on assento.FileiraId equals fileira.Id
            where sessaoIds.Contains(a.SessaoId) && rota.Status != StatusRota.Cancelada
                  && a.Tipo == TipoAlocacao.Paciente
            select new TratamentosMapper.AlocacaoAtiva(
                a.SessaoId, rota.Id, rota.Data, fileira.Ordem, assento.Numero);

        var alocacoes = await alocacoesQuery.ToListAsync(cancellationToken);
        var alocacoesPorSessao = alocacoes
            .GroupBy(a => a.SessaoId)
            .ToDictionary(g => g.Key, g => g.First());

        string? liberadoPorNome = null;
        if (t.SegundoAcompanhanteLiberadoPor is { } liberadoPor)
        {
            liberadoPorNome = await _db.Usuarios.AsNoTracking()
                .Where(u => u.Id == liberadoPor)
                .Select(u => u.NomeCompleto)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var dto = TratamentosMapper.ParaDto(t, alocacoesPorSessao, liberadoPorNome);
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

    public PreviaAgendaDto PreverAgenda(AgendaRequest agenda)
    {
        if (agenda.Continuo)
        {
            var ate = AgendaDeSessoes.HorizonteContinuo(agenda.DataInicio, FusoBrasilia.HojeEmBrasilia());
            return new PreviaAgendaDto(AgendaDeSessoes.GerarAte(agenda.DataInicio, agenda.DiasSemanaMascara, ate), ate);
        }

        var datas = AgendaDeSessoes.GerarQuantidade(agenda.DataInicio, agenda.DiasSemanaMascara, agenda.QuantidadeSessoes ?? 0);
        return new PreviaAgendaDto(datas, datas.Count > 0 ? datas[^1] : null);
    }

    public async Task<Guid> CadastrarAsync(CadastrarTratamentoRequest request, CancellationToken cancellationToken = default)
    {
        // PacienteId referencia o hub FHIR — validação de existência fica a cargo do hub.
        await GarantirDestinoAtivoAsync(request.UnidadeAtendimentoId, cancellationToken);
        await GarantirTipoAsync(request.TipoTratamentoId, cancellationToken);

        var previa = PreverAgenda(request.Agenda);
        if (previa.Datas.Count == 0)
        {
            throw new ValidacaoException("tratamento.sem_sessoes",
                "A agenda não gerou nenhuma sessão. Confira os dias da semana e o número de sessões.");
        }

        var agora = DateTime.UtcNow;
        var tratamentoId = Guid.CreateVersion7();

        var tratamento = new Tratamento
        {
            Id = tratamentoId,
            PacienteId = request.PacienteId,
            UnidadeAtendimentoId = request.UnidadeAtendimentoId,
            TipoTratamentoId = request.TipoTratamentoId,
            Descricao = request.Descricao.Trim(),
            Observacoes = Trim(request.Observacoes),
            DataInicio = request.Agenda.DataInicio,
            DiasSemanaMascara = request.Agenda.DiasSemanaMascara,
            Continuo = request.Agenda.Continuo,
            QuantidadeSessoes = request.Agenda.Continuo ? null : request.Agenda.QuantidadeSessoes,
            SessoesGeradasAte = previa.GeradasAte,
            Ativo = true,
            CriadoEm = agora,
            Sessoes = [.. previa.Datas.Select(data => NovaSessao(tratamentoId, data, agora))],
        };
        AplicarNecessidades(tratamento, request.Necessidades);
        AplicarRegraAcompanhantes(tratamento, request.Acompanhantes, agora);

        _db.Tratamentos.Add(tratamento);
        await _db.SaveChangesAsync(cancellationToken);
        return tratamento.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarTratamentoRequest request, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), id);

        GarantirAtivo(t);
        if (request.TipoTratamentoId != t.TipoTratamentoId)
        {
            await GarantirTipoAsync(request.TipoTratamentoId, cancellationToken);
        }

        // Trocar o destino vale para as próximas rotas geradas; as já montadas não se refazem sozinhas.
        if (request.UnidadeAtendimentoId != t.UnidadeAtendimentoId)
        {
            await GarantirDestinoAtivoAsync(request.UnidadeAtendimentoId, cancellationToken);
            t.UnidadeAtendimentoId = request.UnidadeAtendimentoId;
        }

        // Baixar o limite não pode deixar viagem futura com mais gente do que o permitido.
        if (request.Acompanhantes.Quantidade < t.QuantidadeAcompanhantes)
        {
            var hoje = FusoBrasilia.HojeEmBrasilia();
            var excedidas = await _db.Sessoes.AsNoTracking()
                .Where(s => s.TratamentoId == id && s.DataPrevista >= hoje
                            && s.Status != StatusSessao.Realizada && s.Status != StatusSessao.Cancelada
                            && s.Acompanhantes.Count > request.Acompanhantes.Quantidade)
                .CountAsync(cancellationToken);
            if (excedidas > 0)
            {
                throw new ConflitoException("tratamento.acompanhantes_escolhidos",
                    $"Há {excedidas} viagem(ns) futura(s) com mais acompanhantes escolhidos do que o novo limite. " +
                    "Ajuste quem vai nessas viagens antes de baixar o limite.");
            }
        }

        t.Descricao = request.Descricao.Trim();
        t.TipoTratamentoId = request.TipoTratamentoId;
        t.Observacoes = Trim(request.Observacoes);
        AplicarNecessidades(t, request.Necessidades);
        AplicarRegraAcompanhantes(t, request.Acompanhantes, DateTime.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task AlterarAgendaAsync(Guid id, AgendaRequest agenda, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos
            .Include(x => x.Sessoes)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), id);

        GarantirAtivo(t);
        var hoje = FusoBrasilia.HojeEmBrasilia();
        if (agenda.DataInicio < hoje)
        {
            throw new ValidacaoException("tratamento.agenda_no_passado",
                "A nova agenda começa hoje ou depois — as viagens que já passaram não se refazem.");
        }

        var inicio = agenda.DataInicio;
        var alocadas = await SessoesAlocadasAsync(t.Sessoes.Select(s => s.Id), cancellationToken);

        // Saem as pendentes, ainda sem rota, do início novo em diante. O resto fica como está.
        var removidas = t.Sessoes
            .Where(s => s.DataPrevista >= inicio && s.Status == StatusSessao.Pendente && !alocadas.Contains(s.Id))
            .ToList();
        foreach (var s in removidas)
        {
            t.Sessoes.Remove(s);
            _db.Sessoes.Remove(s);
        }

        var ocupadas = t.Sessoes.Select(s => s.DataPrevista).ToHashSet();
        var agora = DateTime.UtcNow;
        List<DateOnly> novas;
        DateOnly? geradasAte;
        if (agenda.Continuo)
        {
            var ate = AgendaDeSessoes.HorizonteContinuo(inicio, hoje);
            novas = [.. AgendaDeSessoes.GerarAte(inicio, agenda.DiasSemanaMascara, ate).Where(d => !ocupadas.Contains(d))];
            geradasAte = ate;
        }
        else
        {
            // No modo N o total vale para o atendimento inteiro: as que ficaram já contam.
            var jaContam = t.Sessoes.Count(s => s.Status != StatusSessao.Cancelada);
            var faltam = Math.Max(0, (agenda.QuantidadeSessoes ?? 0) - jaContam);
            novas = [.. GerarSemRepetir(inicio, agenda.DiasSemanaMascara, faltam, ocupadas)];
            geradasAte = novas.Count > 0 ? novas[^1] : t.Sessoes.Select(s => (DateOnly?)s.DataPrevista).Max();
        }

        foreach (var data in novas)
        {
            // Primeiro no DbSet (Added); pela navegação sozinha, o EF tomaria por existente.
            var sessao = NovaSessao(t.Id, data, agora);
            _db.Sessoes.Add(sessao);
            t.Sessoes.Add(sessao);
        }

        t.DataInicio = inicio;
        t.DiasSemanaMascara = agenda.DiasSemanaMascara;
        t.Continuo = agenda.Continuo;
        t.QuantidadeSessoes = agenda.Continuo ? null : agenda.QuantidadeSessoes;
        t.SessoesGeradasAte = geradasAte;

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task EncerrarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos
            .Include(x => x.Sessoes)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), id);

        if (!t.Ativo)
        {
            throw new ConflitoException("tratamento.ja_encerrado", "Atendimento já está encerrado.");
        }

        // Encerrar cancela o que ainda não saiu do papel (sem rota); o que já está numa rota fica
        // para quem montou a rota decidir — cancelar por baixo deixaria um assento fantasma.
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var alocadas = await SessoesAlocadasAsync(t.Sessoes.Select(s => s.Id), cancellationToken);
        var agora = DateTime.UtcNow;
        foreach (var s in t.Sessoes.Where(s => s.DataPrevista >= hoje
                     && s.Status is StatusSessao.Pendente or StatusSessao.Confirmada
                     && !alocadas.Contains(s.Id)))
        {
            s.Status = StatusSessao.Cancelada;
            s.AtualizadoEm = agora;
        }

        t.Ativo = false;
        t.EncerradoEm = agora;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> RenovarContinuosAsync(DateOnly hoje, CancellationToken cancellationToken = default)
    {
        var continuos = await _db.Tratamentos
            .Include(x => x.Sessoes)
            .Where(x => x.Ativo && x.Continuo)
            .ToListAsync(cancellationToken);

        var renovados = 0;
        foreach (var t in continuos)
        {
            var ate = AgendaDeSessoes.HorizonteContinuo(t.DataInicio, hoje);
            if (t.SessoesGeradasAte is { } gerada && gerada >= ate) continue;

            if (await TemObitoOuIndisponivelAsync(t, cancellationToken)) continue;

            var desde = t.SessoesGeradasAte is { } g ? g.AddDays(1) : t.DataInicio;
            var ocupadas = t.Sessoes.Select(s => s.DataPrevista).ToHashSet();
            var agora = DateTime.UtcNow;
            foreach (var data in AgendaDeSessoes.GerarAte(desde, t.DiasSemanaMascara, ate).Where(d => !ocupadas.Contains(d)))
            {
                _db.Sessoes.Add(NovaSessao(t.Id, data, agora));
            }
            t.SessoesGeradasAte = ate;
            renovados++;
        }

        if (renovados > 0) await _db.SaveChangesAsync(cancellationToken);
        return renovados;
    }

    public async Task<IReadOnlyList<ViagemTransporteDto>> ListarProximasViagensAsync(
        Guid pacienteId, int limite = 30, CancellationToken cancellationToken = default)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var sessoes = await _db.Sessoes.AsNoTracking()
            .Include(s => s.Tratamento!).ThenInclude(t => t.UnidadeAtendimento)
            .Include(s => s.Tratamento!).ThenInclude(t => t.TipoTratamento)
            .Include(s => s.Acompanhantes).ThenInclude(a => a.Acompanhante)
            .Where(s => s.Tratamento!.PacienteId == pacienteId && s.Tratamento.Ativo
                        && s.DataPrevista >= hoje
                        && s.Status != StatusSessao.Cancelada)
            .OrderBy(s => s.DataPrevista)
            .Take(limite)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        return [.. sessoes.Select(s => new ViagemTransporteDto(
            s.Id,
            s.DataPrevista,
            s.Tratamento!.UnidadeAtendimento?.Nome ?? string.Empty,
            s.Tratamento.UnidadeAtendimento?.Endereco?.Cidade,
            s.Tratamento.TipoTratamento?.Nome,
            s.Status,
            s.HoraPrevistaBusca,
            [.. s.Acompanhantes.Where(a => a.Acompanhante is not null).Select(a => a.Acompanhante!.Nome).Order()],
            s.Tratamento.QuantidadeAcompanhantes))];
    }

    public async Task<Guid> AdicionarSessaoAsync(Guid tratamentoId, AdicionarSessaoRequest request, CancellationToken cancellationToken = default)
    {
        var t = await _db.Tratamentos.FirstOrDefaultAsync(x => x.Id == tratamentoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Tratamento), tratamentoId);

        if (!t.Ativo)
        {
            throw new ConflitoException("tratamento.encerrado", "Atendimento encerrado não aceita novas sessões.");
        }

        var sessao = new SessaoDeTratamento
        {
            Id = Guid.CreateVersion7(),
            TratamentoId = tratamentoId,
            DataPrevista = request.DataPrevista,
            HoraPrevistaBusca = request.HoraPrevistaBusca,
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
        sessao.Observacoes = Trim(request.Observacoes);
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
        var sessao = await _db.Sessoes
            .Include(s => s.Tratamento)
            .Include(s => s.Acompanhantes)
            .FirstOrDefaultAsync(s => s.Id == sessaoId && s.TratamentoId == tratamentoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);

        if (sessao.Status == StatusSessao.Cancelada)
        {
            throw new ConflitoException("sessao.cancelada", "Sessão cancelada não pode ser confirmada.");
        }

        if (request.AcompanhanteIds is { } ids)
        {
            await DefinirAcompanhantesAsync(sessao, ids, cancellationToken);
        }

        sessao.Status = request.Realizada ? StatusSessao.Realizada : StatusSessao.NaoRealizada;
        sessao.RealizadaEm = DateTime.UtcNow;
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

    public async Task DefinirAcompanhantesDaSessaoAsync(
        Guid tratamentoId, Guid sessaoId, DefinirAcompanhantesSessaoRequest request, CancellationToken cancellationToken = default)
    {
        var sessao = await _db.Sessoes
            .Include(s => s.Tratamento)
            .Include(s => s.Acompanhantes)
            .FirstOrDefaultAsync(s => s.Id == sessaoId && s.TratamentoId == tratamentoId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);

        if (sessao.Status == StatusSessao.Cancelada)
        {
            throw new ConflitoException("sessao.cancelada", "Sessão cancelada não recebe acompanhantes.");
        }

        await DefinirAcompanhantesAsync(sessao, request.AcompanhanteIds, cancellationToken);
        sessao.AtualizadoEm = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Troca quem vai na viagem. Só gente da lista do paciente e até o limite do atendimento. Marca
    /// também o "acompanhante esperado" da sessão — é o que o gerador de rotas lê para reservar lugar.
    /// </summary>
    private async Task DefinirAcompanhantesAsync(SessaoDeTratamento sessao, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var t = sessao.Tratamento!;
        var distintos = ids.Distinct().ToList();
        if (distintos.Count > t.QuantidadeAcompanhantes)
        {
            throw new ValidacaoException("sessao.acompanhantes_demais",
                t.QuantidadeAcompanhantes == 1
                    ? "Este atendimento permite 1 acompanhante por viagem. Para 2, é preciso liberar no atendimento."
                    : "Este atendimento permite no máximo 2 acompanhantes por viagem.");
        }

        if (distintos.Count > 0)
        {
            var validos = await _db.Acompanhantes.AsNoTracking()
                .Where(a => distintos.Contains(a.Id) && a.PacienteId == t.PacienteId && a.ExcluidoEm == null)
                .CountAsync(ct);
            if (validos != distintos.Count)
            {
                throw new ValidacaoException("sessao.acompanhante_invalido",
                    "Escolha acompanhantes da lista do paciente.");
            }
        }

        // Add/Remove explícitos no DbSet: filho novo com chave preenchida, só pela navegação, o EF
        // pode tomar por existente (UPDATE que não acha linha).
        foreach (var saiu in sessao.Acompanhantes.Where(x => !distintos.Contains(x.AcompanhanteId)).ToList())
        {
            sessao.Acompanhantes.Remove(saiu);
            _db.SessoesAcompanhantes.Remove(saiu);
        }
        foreach (var novo in distintos.Where(id => sessao.Acompanhantes.All(x => x.AcompanhanteId != id)).ToList())
        {
            var vinculo = new SessaoAcompanhante { SessaoId = sessao.Id, AcompanhanteId = novo };
            _db.SessoesAcompanhantes.Add(vinculo);
            sessao.Acompanhantes.Add(vinculo);
        }

        sessao.AcompanhanteEsperado = distintos.Count > 0;
        sessao.AcompanhanteConfirmadoEm = DateTime.UtcNow;
        sessao.AcompanhanteCanal = CanalConfirmacao.Manual;
    }

    /// <summary>Paciente com óbito não renova; hub fora do ar também não (tenta no próximo ciclo —
    /// o horizonte tem folga de um mês).</summary>
    private async Task<bool> TemObitoOuIndisponivelAsync(Tratamento t, CancellationToken ct)
    {
        try
        {
            var paciente = await pacientes.ObterPorIdAsync(t.PacienteId, ct);
            if (paciente?.DataObito is { } obito)
            {
                logger.LogInformation(
                    "Atendimento contínuo {Tratamento}: paciente com óbito em {Obito} — não renova.", t.Id, obito);
                return true;
            }
            return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Atendimento contínuo {Tratamento}: não foi possível conferir o paciente — renova no próximo ciclo.", t.Id);
            return true;
        }
    }

    private void AplicarRegraAcompanhantes(Tratamento t, RegraAcompanhantesRequest regra, DateTime agora)
    {
        if (regra.Quantidade is < 1 or > 2)
        {
            throw new ValidacaoException("tratamento.acompanhantes_limite",
                "O atendimento permite 1 acompanhante, ou 2 com liberação.");
        }

        if (regra.Quantidade == 2)
        {
            var justificativa = regra.JustificativaSegundo?.Trim();
            if (string.IsNullOrEmpty(justificativa))
            {
                throw new ValidacaoException("tratamento.segundo_acompanhante_sem_justificativa",
                    "O segundo acompanhante precisa de liberação: escreva a justificativa.");
            }
            // Liberação nova (ou justificativa trocada) carimba quem liberou agora.
            if (t.QuantidadeAcompanhantes != 2 || t.SegundoAcompanhanteJustificativa != justificativa)
            {
                t.SegundoAcompanhanteLiberadoPor = usuarioAtual.UsuarioId;
                t.SegundoAcompanhanteLiberadoEm = agora;
            }
            t.SegundoAcompanhanteJustificativa = justificativa;
        }
        else
        {
            t.SegundoAcompanhanteJustificativa = null;
            t.SegundoAcompanhanteLiberadoPor = null;
            t.SegundoAcompanhanteLiberadoEm = null;
        }
        t.QuantidadeAcompanhantes = regra.Quantidade;
    }

    private static void AplicarNecessidades(Tratamento t, NecessidadesRequest n)
    {
        t.Mobilidade = n.Mobilidade;
        t.DificuldadeVeiculoAlto = n.DificuldadeVeiculoAlto;
        t.Isolamento = n.Isolamento;
        t.UsaOxigenio = n.UsaOxigenio;
        t.NecessitaAjuda = n.NecessitaAjuda;
        t.AjudaDescricao = n.NecessitaAjuda ? Trim(n.AjudaDescricao) : null;
    }

    /// <summary>As <paramref name="quantidade"/> próximas datas nos dias marcados, pulando as que já têm sessão.</summary>
    private static IEnumerable<DateOnly> GerarSemRepetir(DateOnly inicio, int mascara, int quantidade, IReadOnlySet<DateOnly> ocupadas)
    {
        var geradas = 0;
        // Teto de segurança: 365 sessões num único dia da semana cabem em ~7 anos.
        for (var data = inicio; geradas < quantidade && data < inicio.AddYears(8); data = data.AddDays(1))
        {
            if (!AgendaDeSessoes.TemSessaoNoDia(mascara, data.DayOfWeek) || ocupadas.Contains(data)) continue;
            geradas++;
            yield return data;
        }
    }

    private static SessaoDeTratamento NovaSessao(Guid tratamentoId, DateOnly data, DateTime agora) => new()
    {
        Id = Guid.CreateVersion7(),
        TratamentoId = tratamentoId,
        DataPrevista = data,
        Status = StatusSessao.Pendente,
        CriadoEm = agora,
    };

    /// <summary>Sessões com lugar numa rota que não foi cancelada.</summary>
    private async Task<HashSet<Guid>> SessoesAlocadasAsync(IEnumerable<Guid> sessaoIds, CancellationToken ct)
    {
        var ids = sessaoIds.ToList();
        if (ids.Count == 0) return [];
        var alocadas = await (
            from a in _db.Alocacoes.AsNoTracking()
            join rota in _db.Rotas.AsNoTracking() on a.RotaDiariaId equals rota.Id
            where ids.Contains(a.SessaoId) && rota.Status != StatusRota.Cancelada
            select a.SessaoId).Distinct().ToListAsync(ct);
        return [.. alocadas];
    }

    private static void GarantirAtivo(Tratamento t)
    {
        if (!t.Ativo)
        {
            throw new ConflitoException("tratamento.encerrado", "Atendimento encerrado não pode ser alterado.");
        }
    }

    /// <summary>Tipo é obrigatório (o tempo médio vem dele) e precisa estar ativo.</summary>
    private async Task GarantirTipoAsync(Guid? tipoId, CancellationToken ct)
    {
        if (tipoId is not { } id)
        {
            throw new ValidacaoException("tratamento.tipo_obrigatorio",
                "Escolha o tipo de tratamento — é dele que vem o tempo médio.");
        }

        var ativo = await _db.TiposTratamento.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => (bool?)x.Ativo)
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException(nameof(TipoTratamento), id);

        if (!ativo)
        {
            throw new ConflitoException("tratamento.tipo_inativo",
                "O tipo de tratamento escolhido está desativado. Reative-o ou escolha outro.");
        }
    }

    /// <summary>Destino inexistente é 404; inativo é recusado — foi tirado das opções de propósito.</summary>
    private async Task GarantirDestinoAtivoAsync(Guid unidadeAtendimentoId, CancellationToken ct)
    {
        var ativo = await _db.UnidadesAtendimento.AsNoTracking()
            .Where(u => u.Id == unidadeAtendimentoId)
            .Select(u => (bool?)u.Ativo)
            .FirstOrDefaultAsync(ct)
            ?? throw new NaoEncontradoException(nameof(UnidadeAtendimento), unidadeAtendimentoId);

        if (!ativo)
        {
            throw new ConflitoException("tratamento.destino_inativo",
                "A unidade de atendimento escolhida está desativada. Reative-a ou escolha outro destino.");
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
        .Include(t => t.UnidadeAtendimento)
        .Include(t => t.TipoTratamento)
        .Include(t => t.Sessoes);

    private static TratamentoListItemDto ParaListItem(Tratamento t)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var proxima = t.Sessoes
            .Where(s => s.Status != StatusSessao.Cancelada && s.DataPrevista >= hoje)
            .OrderBy(s => s.DataPrevista)
            .FirstOrDefault()?.DataPrevista;
        var realizadas = t.Sessoes.Count(s => s.Status == StatusSessao.Realizada);
        return new TratamentoListItemDto(
            t.Id,
            t.PacienteId,
            string.Empty,
            t.UnidadeAtendimentoId,
            t.UnidadeAtendimento?.Nome ?? string.Empty,
            t.TipoTratamento?.Nome,
            t.Descricao,
            t.TipoTratamento?.TempoMedioMinutos,
            t.DiasSemanaMascara,
            t.Continuo,
            proxima,
            t.Sessoes.Count(s => s.Status != StatusSessao.Cancelada),
            realizadas,
            t.Ativo);
    }

    private static string? Trim(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
