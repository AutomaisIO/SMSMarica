using Microsoft.EntityFrameworkCore;
using SMSMarica.Core.Agendamentos.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Identidade;
using SMSMarica.Core.Pacientes;
using SMSMarica.Data;
using SMSMarica.Data.Entities.Agendamentos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Agendamentos;

/// <summary>
/// Calcula horários livres (via <see cref="CalculadoraSlots"/>) e gerencia o ciclo de
/// vida do agendamento. Marcar valida que o horário é um slot livre e não há double-booking;
/// o paciente é resolvido no hub FHIR (<see cref="IPacientesService"/>) e seu nome/CNS são
/// guardados como snapshot. Ver ADR-0012.
/// </summary>
public sealed class AgendamentoService(
    SmsMaricaDbContext db,
    IPacientesService pacientes,
    IUsuarioAtualAccessor usuarioAtual) : IAgendamentoService
{
    private readonly SmsMaricaDbContext _db = db;
    private readonly IPacientesService _pacientes = pacientes;
    private readonly IUsuarioAtualAccessor _usuarioAtual = usuarioAtual;

    public async Task<IReadOnlyList<SlotLivreDto>> CalcularHorariosLivresAsync(
        Guid agendaId, DateOnly inicio, DateOnly fim, CancellationToken cancellationToken = default)
    {
        if (fim < inicio)
        {
            throw new ValidacaoException("agenda.intervalo", "Data final não pode ser anterior à inicial.");
        }

        var agenda = await CarregarAgendaAsync(agendaId, cancellationToken);
        var slots = await CalcularAsync(agenda, inicio, fim, cancellationToken);
        return [.. slots.Select(s => new SlotLivreDto(s.Inicio, s.Fim))];
    }

    public async Task<IReadOnlyList<SlotEspecialidadeDto>> CalcularHorariosLivresPorEspecialidadeAsync(
        Guid especialidadeId, Guid? unidadeId, DateOnly inicio, DateOnly fim,
        CancellationToken cancellationToken = default)
    {
        if (fim < inicio)
        {
            throw new ValidacaoException("agenda.intervalo", "Data final não pode ser anterior à inicial.");
        }

        var query = _db.Agendas.AsNoTracking()
            .Include(a => a.Unidade)
            .Where(a => a.ExcluidoEm == null && a.Ativo
                && a.Finalidade == FinalidadeAgenda.Consulta
                && a.EspecialidadeId == especialidadeId);

        if (unidadeId.HasValue)
        {
            query = query.Where(a => a.UnidadeId == unidadeId);
        }

        var agendas = await query.ToListAsync(cancellationToken);

        var slots = new List<SlotEspecialidadeDto>();
        foreach (var agenda in agendas)
        {
            var janelas = await CalcularAsync(agenda, inicio, fim, cancellationToken);
            var unidadeNome = agenda.Unidade?.Nome ?? string.Empty;
            slots.AddRange(janelas.Select(j => new SlotEspecialidadeDto(
                agenda.Id, agenda.UnidadeId, unidadeNome,
                agenda.MedicoId, agenda.MedicoNome, j.Inicio, j.Fim)));
        }

        // Ordena por médico e horário: o fluxo de marcação agrupa por profissional.
        return [.. slots.OrderBy(s => s.MedicoNome).ThenBy(s => s.InicioEm)];
    }

    public async Task<IReadOnlyList<AgendamentoListItemDto>> ListarAsync(
        Guid agendaId, DateOnly inicio, DateOnly fim, CancellationToken cancellationToken = default)
    {
        var de = inicio.ToDateTime(TimeOnly.MinValue);
        var ate = fim.ToDateTime(TimeOnly.MaxValue);

        var lista = await _db.Agendamentos.AsNoTracking()
            .Where(a => a.AgendaId == agendaId && a.ExcluidoEm == null && a.InicioEm >= de && a.InicioEm <= ate)
            .OrderBy(a => a.InicioEm)
            .ToListAsync(cancellationToken);

        return [.. lista.Select(AgendamentosMapper.ParaListItem)];
    }

    public async Task<AgendamentoDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var agendamento = await _db.Agendamentos.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && a.ExcluidoEm == null, cancellationToken)
            ?? throw new NaoEncontradoException(nameof(Agendamento), id);

        return AgendamentosMapper.ParaDto(agendamento);
    }

    public async Task<Guid> AgendarAsync(AgendarRequest request, CancellationToken cancellationToken = default)
    {
        var agenda = await CarregarAgendaAsync(request.AgendaId, cancellationToken);
        if (!agenda.Ativo)
        {
            throw new ConflitoException("agenda.inativa", "Agenda inativa não aceita novos agendamentos.");
        }

        var inicioEm = DateTime.SpecifyKind(request.InicioEm, DateTimeKind.Unspecified);
        var fimEm = inicioEm.AddMinutes(agenda.DuracaoSlotMinutos);
        var dia = DateOnly.FromDateTime(inicioEm);

        // O horário pedido precisa ser exatamente um slot livre (cobre vigência, recorrência,
        // avulso, bloqueio e agendamentos já existentes).
        var livres = await CalcularAsync(agenda, dia, dia, cancellationToken);
        if (!livres.Any(s => s.Inicio == inicioEm && s.Fim == fimEm))
        {
            throw new ConflitoException("agendamento.slot_indisponivel",
                "Horário indisponível: não corresponde a um slot livre da agenda.");
        }

        // Guarda extra contra corrida: nenhum agendamento ativo pode se sobrepor.
        var conflito = await _db.Agendamentos.AsNoTracking().AnyAsync(
            a => a.AgendaId == agenda.Id
                && a.ExcluidoEm == null
                && a.Status != StatusAgendamento.Cancelado
                && a.InicioEm < fimEm && inicioEm < a.FimEm,
            cancellationToken);
        if (conflito)
        {
            throw new ConflitoException("agendamento.conflito", "Já existe um agendamento ativo neste horário.");
        }

        // Resolve o paciente no hub FHIR (lança NaoEncontrado se não existir) e guarda o snapshot.
        var paciente = await _pacientes.ObterPorIdAsync(request.PacienteId, cancellationToken);

        var agendamento = new Agendamento
        {
            Id = Guid.CreateVersion7(),
            AgendaId = agenda.Id,
            PacienteId = request.PacienteId,
            PacienteNome = paciente.NomeCompleto,
            PacienteCns = paciente.Cns,
            InicioEm = inicioEm,
            FimEm = fimEm,
            Status = StatusAgendamento.Agendado,
            TipoExameId = request.TipoExameId,
            Observacao = string.IsNullOrWhiteSpace(request.Observacao) ? null : request.Observacao.Trim(),
            CriadoEm = DateTime.UtcNow,
            CriadoPor = _usuarioAtual.UsuarioId,
        };

        _db.Agendamentos.Add(agendamento);
        await _db.SaveChangesAsync(cancellationToken);
        return agendamento.Id;
    }

    public async Task ConfirmarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await CarregarAgendamentoAsync(id, cancellationToken);
        ExigirStatus(a, "confirmar", StatusAgendamento.Agendado);
        a.Status = StatusAgendamento.Confirmado;
        a.ConfirmadoEm = DateTime.UtcNow;
        Tocar(a);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RealizarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await CarregarAgendamentoAsync(id, cancellationToken);
        ExigirStatus(a, "realizar", StatusAgendamento.Agendado, StatusAgendamento.Confirmado);
        a.Status = StatusAgendamento.Realizado;
        a.RealizadoEm = DateTime.UtcNow;
        Tocar(a);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RegistrarFaltaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var a = await CarregarAgendamentoAsync(id, cancellationToken);
        ExigirStatus(a, "registrar falta", StatusAgendamento.Agendado, StatusAgendamento.Confirmado);
        a.Status = StatusAgendamento.Faltou;
        Tocar(a);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelarAsync(Guid id, CancelarAgendamentoRequest request, CancellationToken cancellationToken = default)
    {
        var a = await CarregarAgendamentoAsync(id, cancellationToken);
        ExigirStatus(a, "cancelar", StatusAgendamento.Agendado, StatusAgendamento.Confirmado);
        a.Status = StatusAgendamento.Cancelado;
        a.CanceladoEm = DateTime.UtcNow;
        a.MotivoCancelamento = string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim();
        Tocar(a);
        await _db.SaveChangesAsync(cancellationToken);
    }

    // ---- Helpers ----

    private async Task<IReadOnlyList<JanelaSlot>> CalcularAsync(
        Agenda agenda, DateOnly inicio, DateOnly fim, CancellationToken cancellationToken)
    {
        var de = inicio.ToDateTime(TimeOnly.MinValue);
        var ate = fim.ToDateTime(TimeOnly.MaxValue);

        var recorrencias = await _db.DisponibilidadesRecorrentes.AsNoTracking()
            .Where(r => r.AgendaId == agenda.Id)
            .Select(r => new RegraRecorrenteSlot(r.DiaSemana, r.HoraInicio, r.HoraFim, r.VigenciaInicio, r.VigenciaFim, r.Ativo))
            .ToListAsync(cancellationToken);

        var avulsos = await _db.DisponibilidadesAvulsas.AsNoTracking()
            .Where(d => d.AgendaId == agenda.Id && d.FimEm >= de && d.InicioEm <= ate)
            .Select(d => new JanelaSlot(d.InicioEm, d.FimEm))
            .ToListAsync(cancellationToken);

        var bloqueios = await _db.BloqueiosAgenda.AsNoTracking()
            .Where(b => b.AgendaId == agenda.Id && b.FimEm >= de && b.InicioEm <= ate)
            .Select(b => new JanelaSlot(b.InicioEm, b.FimEm))
            .ToListAsync(cancellationToken);

        var ocupados = await _db.Agendamentos.AsNoTracking()
            .Where(a => a.AgendaId == agenda.Id
                && a.ExcluidoEm == null
                && a.Status != StatusAgendamento.Cancelado
                && a.FimEm >= de && a.InicioEm <= ate)
            .Select(a => new JanelaSlot(a.InicioEm, a.FimEm))
            .ToListAsync(cancellationToken);

        return CalculadoraSlots.Calcular(
            agenda.DuracaoSlotMinutos,
            agenda.VigenciaInicio,
            agenda.VigenciaFim,
            recorrencias,
            avulsos,
            bloqueios,
            ocupados,
            inicio,
            fim);
    }

    private async Task<Agenda> CarregarAgendaAsync(Guid id, CancellationToken ct) =>
        await _db.Agendas.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && a.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Agenda), id);

    private async Task<Agendamento> CarregarAgendamentoAsync(Guid id, CancellationToken ct) =>
        await _db.Agendamentos.FirstOrDefaultAsync(a => a.Id == id && a.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(Agendamento), id);

    private static void ExigirStatus(Agendamento a, string acao, params StatusAgendamento[] permitidos)
    {
        if (!permitidos.Contains(a.Status))
        {
            throw new ConflitoException("agendamento.estado_invalido",
                $"Não é possível {acao} um agendamento no estado '{a.Status}'.");
        }
    }

    private void Tocar(Agendamento a)
    {
        a.AtualizadoEm = DateTime.UtcNow;
        a.AtualizadoPor = _usuarioAtual.UsuarioId;
    }
}
