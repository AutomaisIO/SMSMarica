using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Agendamentos.Dtos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Core.Medicos;
using SMSMais.Data;
using SMSMais.Data.Entities.Agendamentos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Agendamentos;

/// <summary>
/// CRUD de agendas e suas disponibilidades. O médico é resolvido no hub FHIR
/// (<see cref="IMedicosService"/>) ao cadastrar, e seu nome é guardado como snapshot
/// para exibir a grade sem ir ao hub a cada render. Ver ADR-0012.
/// </summary>
public sealed class AgendaService(
    SmsMaisDbContext db,
    IMedicosService medicos,
    IUsuarioAtualAccessor usuarioAtual) : IAgendaService
{
    private readonly SmsMaisDbContext _db = db;
    private readonly IMedicosService _medicos = medicos;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task<IReadOnlyList<AgendaListItemDto>> ListarAsync(
        FinalidadeAgenda? finalidade, Guid? unidadeId, Guid? especialidadeId, Guid? medicoId, Guid? equipamentoId,
        bool incluirInativas, CancellationToken cancellationToken = default)
    {
        IQueryable<Agenda> query = _db.Agendas.AsNoTracking()
            .Include(a => a.Unidade)
            .Include(a => a.Especialidade)
            .Include(a => a.Equipamento)
            .Where(a => a.ExcluidoEm == null);

        if (!incluirInativas)
        {
            query = query.Where(a => a.Ativo);
        }

        if (finalidade.HasValue)
        {
            query = query.Where(a => a.Finalidade == finalidade);
        }

        if (unidadeId.HasValue)
        {
            query = query.Where(a => a.UnidadeId == unidadeId);
        }

        if (especialidadeId.HasValue)
        {
            query = query.Where(a => a.EspecialidadeId == especialidadeId);
        }

        if (medicoId.HasValue)
        {
            query = query.Where(a => a.MedicoId == medicoId);
        }

        if (equipamentoId.HasValue)
        {
            query = query.Where(a => a.EquipamentoId == equipamentoId);
        }

        var lista = await query
            .OrderBy(a => a.Finalidade)
            .ThenBy(a => a.MedicoNome)
            .ToListAsync(cancellationToken);

        return [.. lista.Select(AgendamentosMapper.ParaListItem)];
    }

    public async Task<AgendaDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var agenda = await _db.Agendas.AsNoTracking()
            .Include(a => a.Unidade)
            .Include(a => a.Especialidade)
            .Include(a => a.Equipamento)
            .Include(a => a.Recorrencias)
            .FirstOrDefaultAsync(a => a.Id == id && a.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Agenda), id);

        return AgendamentosMapper.ParaDto(agenda);
    }

    public async Task<Guid> CadastrarAsync(CadastrarAgendaRequest request, CancellationToken cancellationToken = default)
    {
        await ValidarUnidadeAsync(request.UnidadeId, cancellationToken);

        var agenda = new Agenda
        {
            Id = Guid.CreateVersion7(),
            Finalidade = request.Finalidade,
            UnidadeId = request.UnidadeId,
            DuracaoSlotMinutos = request.DuracaoSlotMinutos,
            VigenciaInicio = request.VigenciaInicio,
            VigenciaFim = request.VigenciaFim,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        if (request.Finalidade == FinalidadeAgenda.Consulta)
        {
            if (request.EspecialidadeId is not { } especialidadeId)
            {
                throw new ValidacaoException("agenda.especialidade", "Agenda de consulta exige uma especialidade.");
            }

            await ValidarEspecialidadeAsync(especialidadeId, cancellationToken);
            agenda.EspecialidadeId = especialidadeId;

            // A agenda de consulta é sempre DO PROFISSIONAL: a marcação parte da
            // especialidade e lista os médicos dela. (Pools soltos de especialidade
            // foram descontinuados; agendas legadas seguem funcionando.)
            if (request.MedicoId is not { } medicoId)
            {
                throw new ValidacaoException("agenda.medico",
                    "Agenda de consulta exige um médico — a agenda é do profissional.");
            }

            var medico = await _medicos.ObterPorIdAsync(medicoId, cancellationToken);
            agenda.MedicoId = medicoId;
            agenda.MedicoNome = medico.NomeCompleto;
        }
        else
        {
            if (request.EquipamentoId is not { } equipamentoId)
            {
                throw new ValidacaoException("agenda.equipamento", "Agenda de exame exige um equipamento.");
            }

            await ValidarEquipamentoAsync(equipamentoId, cancellationToken);
            agenda.EquipamentoId = equipamentoId;
        }

        // Grade semanal inicial (opcional): cria a agenda já completa, em uma operação.
        foreach (var r in request.Recorrencias ?? [])
        {
            if (r.HoraFim <= r.HoraInicio)
            {
                throw new ValidacaoException("recorrencia.horario",
                    $"Horário inválido na grade ({r.DiaSemana}): fim deve ser maior que o início.");
            }

            agenda.Recorrencias.Add(new DisponibilidadeRecorrente
            {
                Id = Guid.CreateVersion7(),
                AgendaId = agenda.Id,
                DiaSemana = r.DiaSemana,
                HoraInicio = r.HoraInicio,
                HoraFim = r.HoraFim,
                VigenciaInicio = r.VigenciaInicio,
                VigenciaFim = r.VigenciaFim,
                Ativo = true,
                CriadoEm = DateTime.UtcNow,
                CriadoPor = _usuarioAtual.UsuarioId,
            });
        }

        _db.Agendas.Add(agenda);
        await _db.SaveChangesAsync(cancellationToken);
        return agenda.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarAgendaRequest request, CancellationToken cancellationToken = default)
    {
        var agenda = await CarregarAsync(id, cancellationToken);

        agenda.DuracaoSlotMinutos = request.DuracaoSlotMinutos;
        agenda.VigenciaInicio = request.VigenciaInicio;
        agenda.VigenciaFim = request.VigenciaFim;
        agenda.Ativo = request.Ativo;
        Tocar(agenda);

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var agenda = await CarregarAsync(id, cancellationToken);

        var temFuturos = await _db.Agendamentos.AsNoTracking().AnyAsync(
            a => a.AgendaId == id
                && a.ExcluidoEm == null
                && a.Status != StatusAgendamento.Cancelado
                && a.Status != StatusAgendamento.Realizado
                && a.Status != StatusAgendamento.Faltou,
            cancellationToken);
        if (temFuturos)
        {
            throw new ConflitoException("agenda.em_uso",
                "Não é possível excluir: há agendamentos ativos nesta agenda. Cancele-os ou desative a agenda.");
        }

        var agora = DateTime.UtcNow;
        agenda.ExcluidoEm = agora;
        agenda.ExcluidoPor = _usuarioAtual.UsuarioId;
        agenda.AtualizadoEm = agora;
        agenda.AtualizadoPor = _usuarioAtual.UsuarioId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---- Recorrências ----

    public async Task<Guid> AdicionarRecorrenciaAsync(Guid agendaId, AdicionarRecorrenciaRequest request, CancellationToken cancellationToken = default)
    {
        var agenda = await CarregarAsync(agendaId, cancellationToken);

        if (request.HoraFim <= request.HoraInicio)
        {
            throw new ValidacaoException("recorrencia.horario", "Hora de fim deve ser maior que a de início.");
        }

        var recorrencia = new DisponibilidadeRecorrente
        {
            Id = Guid.CreateVersion7(),
            AgendaId = agenda.Id,
            DiaSemana = request.DiaSemana,
            HoraInicio = request.HoraInicio,
            HoraFim = request.HoraFim,
            VigenciaInicio = request.VigenciaInicio,
            VigenciaFim = request.VigenciaFim,
            Ativo = true,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.DisponibilidadesRecorrentes.Add(recorrencia);
        Tocar(agenda);
        await _db.SaveChangesAsync(cancellationToken);
        return recorrencia.Id;
    }

    public async Task RemoverRecorrenciaAsync(Guid agendaId, Guid recorrenciaId, CancellationToken cancellationToken = default)
    {
        var recorrencia = await _db.DisponibilidadesRecorrentes
            .FirstOrDefaultAsync(r => r.Id == recorrenciaId && r.AgendaId == agendaId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(DisponibilidadeRecorrente), recorrenciaId);

        _db.DisponibilidadesRecorrentes.Remove(recorrencia);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---- Avulsos / Bloqueios ----

    public async Task<DisponibilidadesAgendaDto> ListarDisponibilidadesAsync(
        Guid agendaId, DateOnly inicio, DateOnly fim, CancellationToken cancellationToken = default)
    {
        await GarantirAgendaExisteAsync(agendaId, cancellationToken);

        var deDateTime = inicio.ToDateTime(TimeOnly.MinValue);
        var ateDateTime = fim.ToDateTime(TimeOnly.MaxValue);

        var avulsos = await _db.DisponibilidadesAvulsas.AsNoTracking()
            .Where(d => d.AgendaId == agendaId && d.FimEm >= deDateTime && d.InicioEm <= ateDateTime)
            .OrderBy(d => d.InicioEm)
            .ToListAsync(cancellationToken);

        var bloqueios = await _db.BloqueiosAgenda.AsNoTracking()
            .Where(b => b.AgendaId == agendaId && b.FimEm >= deDateTime && b.InicioEm <= ateDateTime)
            .OrderBy(b => b.InicioEm)
            .ToListAsync(cancellationToken);

        return new DisponibilidadesAgendaDto(
            [.. avulsos.Select(AgendamentosMapper.ParaDto)],
            [.. bloqueios.Select(AgendamentosMapper.ParaDto)]);
    }

    public async Task<Guid> AdicionarAvulsoAsync(Guid agendaId, AdicionarAvulsoRequest request, CancellationToken cancellationToken = default)
    {
        var agenda = await CarregarAsync(agendaId, cancellationToken);

        if (request.FimEm <= request.InicioEm)
        {
            throw new ValidacaoException("avulso.horario", "Fim deve ser maior que o início.");
        }

        var avulso = new DisponibilidadeAvulsa
        {
            Id = Guid.CreateVersion7(),
            AgendaId = agenda.Id,
            InicioEm = ComoLocal(request.InicioEm),
            FimEm = ComoLocal(request.FimEm),
            Motivo = string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim(),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.DisponibilidadesAvulsas.Add(avulso);
        await _db.SaveChangesAsync(cancellationToken);
        return avulso.Id;
    }

    public async Task RemoverAvulsoAsync(Guid agendaId, Guid avulsoId, CancellationToken cancellationToken = default)
    {
        var avulso = await _db.DisponibilidadesAvulsas
            .FirstOrDefaultAsync(d => d.Id == avulsoId && d.AgendaId == agendaId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(DisponibilidadeAvulsa), avulsoId);

        _db.DisponibilidadesAvulsas.Remove(avulso);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Guid> AdicionarBloqueioAsync(Guid agendaId, AdicionarBloqueioRequest request, CancellationToken cancellationToken = default)
    {
        var agenda = await CarregarAsync(agendaId, cancellationToken);

        if (request.FimEm <= request.InicioEm)
        {
            throw new ValidacaoException("bloqueio.horario", "Fim deve ser maior que o início.");
        }

        var bloqueio = new BloqueioAgenda
        {
            Id = Guid.CreateVersion7(),
            AgendaId = agenda.Id,
            InicioEm = ComoLocal(request.InicioEm),
            FimEm = ComoLocal(request.FimEm),
            Motivo = request.Motivo.Trim(),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.BloqueiosAgenda.Add(bloqueio);
        await _db.SaveChangesAsync(cancellationToken);
        return bloqueio.Id;
    }

    public async Task RemoverBloqueioAsync(Guid agendaId, Guid bloqueioId, CancellationToken cancellationToken = default)
    {
        var bloqueio = await _db.BloqueiosAgenda
            .FirstOrDefaultAsync(b => b.Id == bloqueioId && b.AgendaId == agendaId, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(BloqueioAgenda), bloqueioId);

        _db.BloqueiosAgenda.Remove(bloqueio);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---- Helpers ----

    private async Task<Agenda> CarregarAsync(Guid id, CancellationToken ct) =>
        await _db.Agendas.FirstOrDefaultAsync(a => a.Id == id && a.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Agenda), id);

    private async Task GarantirAgendaExisteAsync(Guid id, CancellationToken ct)
    {
        var existe = await _db.Agendas.AsNoTracking().AnyAsync(a => a.Id == id && a.ExcluidoEm == null, ct);
        if (!existe)
        {
            throw new NaoEncontradoException(nameof(Agenda), id);
        }
    }

    private async Task ValidarUnidadeAsync(Guid unidadeId, CancellationToken ct)
    {
        var existe = await _db.Unidades.AsNoTracking().AnyAsync(u => u.Id == unidadeId, ct);
        if (!existe)
        {
            throw new NaoEncontradoException("Unidade", unidadeId);
        }
    }

    private async Task ValidarEspecialidadeAsync(Guid especialidadeId, CancellationToken ct)
    {
        var ativa = await _db.Especialidades.AsNoTracking()
            .AnyAsync(e => e.Id == especialidadeId && e.ExcluidoEm == null && e.Ativo, ct);
        if (!ativa)
        {
            throw new ValidacaoException("agenda.especialidade", "Especialidade inexistente ou inativa.");
        }
    }

    private async Task ValidarEquipamentoAsync(Guid equipamentoId, CancellationToken ct)
    {
        var ativo = await _db.Equipamentos.AsNoTracking()
            .AnyAsync(e => e.Id == equipamentoId && e.ExcluidoEm == null && e.Ativo, ct);
        if (!ativo)
        {
            throw new ValidacaoException("agenda.equipamento", "Equipamento inexistente ou inativo.");
        }
    }

    private void Tocar(Agenda agenda)
    {
        agenda.AtualizadoEm = DateTime.UtcNow;
        agenda.AtualizadoPor = _usuarioAtual.UsuarioId;
    }

    /// <summary>Trata o DateTime recebido como horário local (wall-clock), descartando o Kind para o banco sem fuso.</summary>
    private static DateTime ComoLocal(DateTime valor) =>
        DateTime.SpecifyKind(valor, DateTimeKind.Unspecified);
}
