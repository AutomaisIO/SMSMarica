using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Faturamento;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Viagem;

/// <summary>O que o tablet manda a cada marcação da viagem. <paramref name="AcompanhanteOrdem"/> é a
/// posição do acompanhante na lista da sessão (1 ou 2), como o tablet recebeu. <paramref name="Resgate"/>:
/// marcação do resgate (embarque na unidade, chegada em casa).</summary>
public sealed record RegistrarEventoViagemRequest(
    Guid IdLocal,
    Guid RotaId,
    TipoEventoViagem Tipo,
    Guid? SessaoId,
    PessoaViagem? Pessoa,
    int? AcompanhanteOrdem,
    Guid? AcompanhanteId,
    Guid? MotoristaId,
    DateTime OcorridoEm,
    double? Latitude,
    double? Longitude,
    double? PrecisaoM,
    bool Resgate = false);

public sealed record EventoViagemRegistradoDto(Guid Id, bool Aplicado, string? MotivoNaoAplicado, DateTime RecebidoEm);

/// <summary>Um registro da viagem como o painel mostra, com os nomes resolvidos.</summary>
public sealed record EventoViagemDto(
    Guid Id,
    TipoEventoViagem Tipo,
    DateTime OcorridoEm,
    DateTime RecebidoEm,
    Guid? SessaoId,
    string? PacienteNome,
    PessoaViagem? Pessoa,
    string? AcompanhanteNome,
    string? MotoristaNome,
    double? Latitude,
    double? Longitude,
    bool Aplicado,
    string? MotivoNaoAplicado,
    bool Resgate = false);

public interface IViagemService
{
    /// <summary>Registra uma marcação do tablet e aplica na rota e na sessão. Idempotente pelo
    /// <c>IdLocal</c> (a fila sem rede reenvia). O desembarque do paciente marca a viagem como
    /// realizada e contabiliza o translado; a ausência na coleta, como não realizada.</summary>
    Task<EventoViagemRegistradoDto> RegistrarDoTabletAsync(
        DispositivoVeiculo dispositivo, RegistrarEventoViagemRequest request, CancellationToken ct = default);

    /// <summary>A viagem de uma rota, na ordem em que aconteceu.</summary>
    Task<IReadOnlyList<EventoViagemDto>> ListarDaRotaAsync(Guid rotaId, CancellationToken ct = default);
}

public sealed class ViagemService(
    SmsMaisDbContext db,
    IFaturamentoService faturamento,
    IPacienteResolver pacienteResolver,
    ILogger<ViagemService> logger) : IViagemService
{
    public const string MotivoAusencia = "Paciente não estava na coleta (registrado no tablet).";
    public const string ResgateDesmarcado =
        "O resgate deste paciente não está mais nesta rota (foi desmarcado ou ficou com outro carro).";

    public async Task<EventoViagemRegistradoDto> RegistrarDoTabletAsync(
        DispositivoVeiculo dispositivo, RegistrarEventoViagemRequest request, CancellationToken ct = default)
    {
        if (request.IdLocal == Guid.Empty)
            throw new ValidacaoException("idLocal", "Identificador do registro ausente.");
        if (!Enum.IsDefined(request.Tipo))
            throw new ValidacaoException("tipo", "Tipo de registro da viagem inválido.");

        var existente = await db.EventosViagem.AsNoTracking().FirstOrDefaultAsync(e => e.IdLocal == request.IdLocal, ct);
        if (existente is not null) return Registrado(existente);

        // Só a rota do próprio veículo: outro tablet não marca viagem alheia.
        var rota = await db.Rotas.FirstOrDefaultAsync(r => r.Id == request.RotaId && r.VeiculoId == dispositivo.VeiculoId, ct)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), request.RotaId);

        SessaoDeTratamento? sessao = null;
        PessoaViagem? pessoa = null;
        Guid? acompanhanteId = null;
        var semResgate = false;
        if (EhDePessoa(request.Tipo))
        {
            // O tablet pode marcar um resgate que já foi desmarcado (estava sem rede, ou a central tirou):
            // fica o registro, sem mudar a sessão. Quem nunca esteve na rota é erro.
            if (request.SessaoId is not { } sessaoId
                || (!await db.Alocacoes.AnyAsync(a => a.RotaDiariaId == rota.Id && a.SessaoId == sessaoId, ct)
                    && !(request.Resgate && await db.EventosViagem.AnyAsync(e => e.RotaId == rota.Id && e.SessaoId == sessaoId, ct))))
            {
                throw new ValidacaoException("sessaoId", "Este paciente não está nesta rota.");
            }
            // Acompanhante não muda a sessão: a marca dele fica como veio.
            semResgate = request.Resgate && (request.Pessoa ?? PessoaViagem.Paciente) == PessoaViagem.Paciente
                && !await db.Alocacoes.AnyAsync(
                a => a.RotaDiariaId == rota.Id && a.SessaoId == sessaoId && a.Parada == TipoParada.Retorno, ct);
            sessao = await db.Sessoes.FirstAsync(s => s.Id == sessaoId, ct);
            pessoa = request.Pessoa is { } p && Enum.IsDefined(p) ? p : PessoaViagem.Paciente;
            if (pessoa == PessoaViagem.Acompanhante && request.AcompanhanteId is { } a
                && await db.SessoesAcompanhantes.AnyAsync(x => x.SessaoId == sessaoId && x.AcompanhanteId == a, ct))
            {
                acompanhanteId = a;
            }
        }

        var agora = DateTime.UtcNow;
        var (lat, lon) = Posicao(request.Latitude, request.Longitude);
        var evento = new EventoViagem
        {
            Id = Guid.CreateVersion7(),
            IdLocal = request.IdLocal,
            RotaId = rota.Id,
            VeiculoId = dispositivo.VeiculoId,
            DispositivoId = dispositivo.Id,
            MotoristaId = request.MotoristaId is { } m && await db.Motoristas.AnyAsync(x => x.Id == m, ct) ? m : null,
            Tipo = request.Tipo,
            SessaoId = sessao?.Id,
            Pessoa = pessoa,
            AcompanhanteOrdem = pessoa == PessoaViagem.Acompanhante && request.AcompanhanteOrdem is >= 1 and <= 2
                ? request.AcompanhanteOrdem : null,
            AcompanhanteId = acompanhanteId,
            Resgate = request.Resgate && sessao is not null,
            Latitude = lat,
            Longitude = lon,
            PrecisaoM = lat is not null && request.PrecisaoM is >= 0 ? request.PrecisaoM : null,
            OcorridoEm = HoraDoTablet(request.OcorridoEm, agora),
            RecebidoEm = agora,
        };

        var efeito = semResgate ? Efeito.Nao(ResgateDesmarcado) : Aplicar(rota, sessao, evento, agora);
        evento.Aplicado = efeito.Aplicado;
        evento.MotivoNaoAplicado = efeito.Motivo;
        db.EventosViagem.Add(evento);

        // Quem não estava no local volta para a lista de resgate, e a rota encerrada devolve quem ela
        // marcou e não buscou.
        if (efeito.Liberar is { } liberar)
            await Resgate.ResgateService.LiberarAsync(db, rota.Id, liberar == Guid.Empty ? null : liberar, ct);
        dispositivo.UltimoContatoEm = agora;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Mesmo registro chegando duas vezes ao mesmo tempo: devolve o que ficou.
            db.ChangeTracker.Clear();
            var corrida = await db.EventosViagem.AsNoTracking().FirstOrDefaultAsync(e => e.IdLocal == request.IdLocal, ct);
            if (corrida is null) throw;
            return Registrado(corrida);
        }

        // Faturamento depois de gravar a viagem: falha nele não perde a marcação do tablet.
        if (sessao is not null && efeito.Faturamento != 0)
        {
            try
            {
                if (efeito.Faturamento > 0) await faturamento.ContabilizarAsync(sessao.Id, ct);
                else await faturamento.CancelarDaSessaoAsync(sessao.Id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Viagem: faturamento da sessão {Sessao} não atualizado.", sessao.Id);
            }
        }
        return Registrado(evento);
    }

    public async Task<IReadOnlyList<EventoViagemDto>> ListarDaRotaAsync(Guid rotaId, CancellationToken ct = default)
    {
        if (!await db.Rotas.AnyAsync(r => r.Id == rotaId, ct))
            throw new NaoEncontradoException(nameof(RotaDiaria), rotaId);

        var eventos = await db.EventosViagem.AsNoTracking()
            .Where(e => e.RotaId == rotaId)
            .OrderBy(e => e.OcorridoEm).ThenBy(e => e.RecebidoEm)
            .ToListAsync(ct);
        if (eventos.Count == 0) return [];

        var sessaoIds = eventos.Where(e => e.SessaoId != null).Select(e => e.SessaoId!.Value).Distinct().ToList();
        var pacienteDaSessao = await (
            from s in db.Sessoes.AsNoTracking()
            where sessaoIds.Contains(s.Id)
            join t in db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            select new { s.Id, t.PacienteId }).ToDictionaryAsync(x => x.Id, x => x.PacienteId, ct);
        var acompanhantes = (await (
                from sa in db.SessoesAcompanhantes.AsNoTracking()
                where sessaoIds.Contains(sa.SessaoId)
                join ac in db.Acompanhantes.AsNoTracking() on sa.AcompanhanteId equals ac.Id
                orderby ac.Nome
                select new { sa.SessaoId, ac.Id, ac.Nome }).ToListAsync(ct))
            .ToLookup(x => x.SessaoId);
        var motoristaIds = eventos.Where(e => e.MotoristaId != null).Select(e => e.MotoristaId!.Value).Distinct().ToList();
        var motoristas = await db.Motoristas.AsNoTracking()
            .Where(x => motoristaIds.Contains(x.Id))
            .Select(x => new { x.Id, Nome = x.Usuario.NomeCompleto })
            .ToDictionaryAsync(x => x.Id, x => x.Nome, ct);

        IReadOnlyDictionary<Guid, PacienteResumo> nomes;
        try
        {
            nomes = await pacienteResolver.ResolverManyAsync(pacienteDaSessao.Values.Distinct(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Viagem: hub FHIR indisponível; registros sem o nome do paciente.");
            nomes = new Dictionary<Guid, PacienteResumo>();
        }

        string? NomeAcompanhante(EventoViagem e)
        {
            if (e.Pessoa != PessoaViagem.Acompanhante || e.SessaoId is not { } s) return null;
            var lista = acompanhantes[s].ToList();
            var porId = e.AcompanhanteId is { } id ? lista.FirstOrDefault(x => x.Id == id) : null;
            if (porId is not null) return porId.Nome;
            return e.AcompanhanteOrdem is { } o && o <= lista.Count ? lista[o - 1].Nome : "Acompanhante";
        }

        return [.. eventos.Select(e => new EventoViagemDto(
            e.Id, e.Tipo, e.OcorridoEm, e.RecebidoEm, e.SessaoId,
            e.SessaoId is { } s && pacienteDaSessao.TryGetValue(s, out var pid) && nomes.TryGetValue(pid, out var r)
                ? r.Nome : null,
            e.Pessoa, NomeAcompanhante(e),
            e.MotoristaId is { } mid ? motoristas.GetValueOrDefault(mid) : null,
            e.Latitude, e.Longitude, e.Aplicado, e.MotivoNaoAplicado, e.Resgate))];
    }

    /// <summary>A última marcação de cada pessoa nas rotas, na ida e no resgate: paciente na ordem 0,
    /// acompanhantes 1 e 2, com a hora em que chegou ao servidor. É o que o tablet usa para retomar a
    /// viagem depois de reiniciar. Vale o que o tablet marcou, aplicado ou não (a correção do painel fica
    /// na sessão).</summary>
    internal static async Task<Dictionary<(Guid RotaId, Guid SessaoId, int Ordem, bool Resgate), (TipoEventoViagem Tipo, DateTime RecebidoEm)>> UltimasMarcacoesAsync(
        SmsMaisDbContext db, IReadOnlyCollection<Guid> rotaIds, CancellationToken ct)
    {
        var marcacoes = await db.EventosViagem.AsNoTracking()
            .Where(e => rotaIds.Contains(e.RotaId) && e.SessaoId != null
                && (e.Tipo == TipoEventoViagem.Embarque || e.Tipo == TipoEventoViagem.Ausencia
                    || e.Tipo == TipoEventoViagem.Desembarque))
            .OrderBy(e => e.OcorridoEm).ThenBy(e => e.RecebidoEm)
            .Select(e => new { e.RotaId, e.SessaoId, e.Pessoa, e.AcompanhanteOrdem, e.Tipo, e.Resgate, e.RecebidoEm })
            .ToListAsync(ct);

        var ultimas = new Dictionary<(Guid, Guid, int, bool), (TipoEventoViagem, DateTime)>();
        foreach (var e in marcacoes)
        {
            var ordem = e.Pessoa == PessoaViagem.Acompanhante ? e.AcompanhanteOrdem ?? 1 : 0;
            ultimas[(e.RotaId, e.SessaoId!.Value, ordem, e.Resgate)] = (e.Tipo, e.RecebidoEm);
        }
        return ultimas;
    }

    /// <summary><paramref name="Liberar"/>: devolver à lista de resgate quem a rota marcou e não buscou —
    /// a sessão (quem não estava no local) ou <see cref="Guid.Empty"/> (todos, na conclusão).</summary>
    private sealed record Efeito(bool Aplicado, string? Motivo, int Faturamento = 0, Guid? Liberar = null)
    {
        public static readonly Efeito Sim = new(true, null);
        public static Efeito Nao(string motivo) => new(false, motivo);
    }

    private static Efeito Aplicar(RotaDiaria rota, SessaoDeTratamento? sessao, EventoViagem e, DateTime agora)
    {
        if (rota.Status == StatusRota.Cancelada) return Efeito.Nao("A rota foi cancelada no painel.");

        switch (e.Tipo)
        {
            case TipoEventoViagem.Inicio:
                if (rota.Status == StatusRota.Concluida) return Efeito.Nao("A rota já estava concluída.");
                Iniciar(rota, e.OcorridoEm);
                return Efeito.Sim;
            case TipoEventoViagem.Conclusao:
                if (rota.Status == StatusRota.Concluida) return Efeito.Sim;
                Iniciar(rota, e.OcorridoEm);
                rota.Status = StatusRota.Concluida;
                rota.ConcluidaEm = e.OcorridoEm;
                return Efeito.Sim with { Liberar = Guid.Empty };
        }

        // Marcação de pessoa: a viagem começou, mesmo que o "início" tenha se perdido.
        Iniciar(rota, e.OcorridoEm);
        var s = sessao!;
        if (s.Status == StatusSessao.Cancelada) return Efeito.Nao("A sessão foi cancelada no painel.");
        if (s.ConfirmadaPorUsuarioId is not null)
            return Efeito.Nao("A central já corrigiu esta viagem no painel; vale o que está lá.");
        // Acompanhante não muda a sessão nem o faturamento (que conta o paciente a bordo): fica o registro.
        if (e.Pessoa == PessoaViagem.Acompanhante) return Efeito.Sim;

        // Hora cheia de minuto, como o painel mostra e confirma (HH:mm): com segundos e frações, o
        // campo de hora do painel aparece vazio e a confirmação apagaria a hora do tablet.
        var local = FusoBrasilia.ParaExibicao(e.OcorridoEm);
        var hora = new TimeOnly(local.Hour, local.Minute);
        s.AtualizadoEm = agora;
        if (e.Resgate) return AplicarResgate(rota, s, e, hora);

        s.MotoristaIdaId = e.MotoristaId ?? s.MotoristaIdaId ?? rota.MotoristaId;
        s.VeiculoIdaId = rota.VeiculoId;
        switch (e.Tipo)
        {
            case TipoEventoViagem.Embarque:
                s.HoraSaidaResidencia = hora;
                return Efeito.Sim;
            case TipoEventoViagem.Ausencia:
                s.Status = StatusSessao.NaoRealizada;
                s.RealizadaEm = e.OcorridoEm;
                s.MotivoNaoRealizacao = MotivoAusencia;
                return new Efeito(true, null, Faturamento: -1);
            default: // Desembarque: o translado foi feito.
                s.HoraChegadaUnidade = hora;
                s.Status = StatusSessao.Realizada;
                s.RealizadaEm = e.OcorridoEm;
                s.MotivoNaoRealizacao = null;
                return new Efeito(true, null, Faturamento: 1);
        }
    }

    /// <summary>Resgate: o translado já foi contado na ida. Embarque na unidade e chegada em casa preenchem
    /// a volta da sessão. Quem não estava no local quando o carro chegou volta para a lista de resgate, sem
    /// o aviso de "está esperando" (o motorista viu que não estava).</summary>
    private static Efeito AplicarResgate(RotaDiaria rota, SessaoDeTratamento s, EventoViagem e, TimeOnly hora)
    {
        if (s.Status is not (StatusSessao.Realizada or StatusSessao.AguardandoRetorno))
            return Efeito.Nao("A ida desta viagem não foi feita; o resgate não muda a sessão.");

        switch (e.Tipo)
        {
            case TipoEventoViagem.Ausencia:
                // Não estava: o aviso cai e a pergunta "já está disponível?" recomeça.
                s.ProntoParaResgateEm = null;
                s.ProntoParaResgateCanal = null;
                s.AindaEmAtendimentoEm = null;
                s.PerguntaResgateEnviadaEm = null;
                return Efeito.Sim with { Liberar = s.Id };
            case TipoEventoViagem.Embarque:
                s.HoraSaidaUnidade = hora;
                break;
            default: // Desembarque em casa
                s.HoraChegadaResidencia = hora;
                break;
        }
        s.MotoristaVoltaId = e.MotoristaId ?? s.MotoristaVoltaId ?? rota.MotoristaId;
        s.VeiculoVoltaId = rota.VeiculoId;
        s.Status = StatusSessao.Realizada;
        return Efeito.Sim;
    }

    private static void Iniciar(RotaDiaria rota, DateTime quando)
    {
        if (rota.Status != StatusRota.Planejada) return;
        rota.Status = StatusRota.EmAndamento;
        rota.IniciadaEm = quando;
    }

    private static bool EhDePessoa(TipoEventoViagem t) =>
        t is TipoEventoViagem.Embarque or TipoEventoViagem.Ausencia or TipoEventoViagem.Desembarque;

    private static EventoViagemRegistradoDto Registrado(EventoViagem e) =>
        new(e.Id, e.Aplicado, e.MotivoNaoAplicado, e.RecebidoEm);

    private static (double? Lat, double? Lon) Posicao(double? lat, double? lon) =>
        lat is >= -90 and <= 90 && lon is >= -180 and <= 180 && !(lat == 0 && lon == 0) ? (lat, lon) : (null, null);

    /// <summary>Relógio do tablet adiantado não joga o registro para o futuro.</summary>
    private static DateTime HoraDoTablet(DateTime informada, DateTime agora)
    {
        if (informada == default) return agora;
        var utc = informada.Kind == DateTimeKind.Utc ? informada : DateTime.SpecifyKind(informada, DateTimeKind.Utc);
        return utc > agora + TimeSpan.FromMinutes(5) ? agora : utc;
    }
}
