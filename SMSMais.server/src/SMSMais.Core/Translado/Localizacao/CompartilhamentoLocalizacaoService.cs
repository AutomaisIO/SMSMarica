using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Auditoria;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Rastreamento.Dispositivos;
using SMSMais.Core.Translado.Resgate;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Localizacao;

/// <summary>
/// O paciente compartilha a localização do celular com o motorista enquanto espera um embarque de hoje
/// (ADR-0082). Só a última posição, só durante a espera, só para o tablet do carro da viagem; o painel vê
/// apenas o estado.
/// </summary>
public interface ICompartilhamentoLocalizacaoService
{
    /// <summary>O embarque que o paciente espera agora e se ele está compartilhando; nulo fora da espera.</summary>
    Task<LocalizacaoViagemDto?> DoPacienteAsync(Guid pacienteId, CancellationToken ct = default);

    /// <summary>O aceite do paciente para esta viagem: abre o compartilhamento (idempotente) e registra o
    /// aceite na auditoria. 409 <c>localizacao.fora_da_janela</c> fora da espera.</summary>
    Task<LocalizacaoViagemDto> IniciarAsync(
        Guid pacienteId, Guid cidadaoSessaoId, string canal, Guid sessaoId, CancellationToken ct = default);

    /// <summary>Uma posição nova. A espera acabou: encerra e devolve 409 <c>localizacao.fora_da_janela</c>
    /// (o app para sozinho). Sem compartilhamento aberto: 409 <c>localizacao.nao_iniciada</c>.</summary>
    Task AtualizarAsync(Guid pacienteId, Guid sessaoId, AtualizarLocalizacaoRequest request, CancellationToken ct = default);

    /// <summary>"Parar": encerra e apaga a posição. Sem nada aberto, não faz nada.</summary>
    Task PararAsync(Guid pacienteId, Guid sessaoId, CancellationToken ct = default);

    /// <summary>Quem está compartilhando entre os passageiros das rotas de hoje do veículo do tablet.</summary>
    Task<IReadOnlyList<LocalizacaoPassageiroDto>> DoTabletAsync(DispositivoVeiculo dispositivo, CancellationToken ct = default);

    /// <summary>Quem compartilha numa rota, para o painel — sem a coordenada.</summary>
    Task<IReadOnlyList<CompartilhamentoDaRotaDto>> DaRotaAsync(Guid rotaId, CancellationToken ct = default);

    /// <summary>Encerra quem saiu da espera sem um evento do tablet (cancelamento, outro dia, já em casa).</summary>
    Task<int> EncerrarForaDaEsperaAsync(CancellationToken ct = default);
}

public sealed class CompartilhamentoLocalizacaoService(
    SmsMaisDbContext db,
    IAuditoriaService auditoria) : ICompartilhamentoLocalizacaoService
{
    public const string EntidadeAuditoria = "CompartilhamentoLocalizacao";

    /// <summary>A espera da ida começa 2 h antes da busca prevista, ou quando a rota começa.</summary>
    public static readonly TimeSpan AntecedenciaIda = TimeSpan.FromHours(2);

    /// <summary>Envios mais próximos que isso são ignorados (o app manda a cada 15 s ou 20 m).</summary>
    private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromSeconds(10);

    /// <summary>Posição medida há mais tempo que isso não vale como "onde ele está".</summary>
    private static readonly TimeSpan PosicaoAntiga = TimeSpan.FromMinutes(5);

    private const double PrecisaoMaximaM = 5000;

    public async Task<LocalizacaoViagemDto?> DoPacienteAsync(Guid pacienteId, CancellationToken ct = default)
    {
        var espera = await EsperaAsync(pacienteId, null, ct);
        if (espera is null) return null;
        var atual = await db.CompartilhamentosLocalizacao.AsNoTracking()
            .FirstOrDefaultAsync(c => c.SessaoId == espera.SessaoId && c.Trecho == espera.Trecho, ct);
        return Dto(espera, atual);
    }

    public async Task<LocalizacaoViagemDto> IniciarAsync(
        Guid pacienteId, Guid cidadaoSessaoId, string canal, Guid sessaoId, CancellationToken ct = default)
    {
        var espera = await EsperaAsync(pacienteId, sessaoId, ct) ?? throw ForaDaEspera();
        var c = await db.CompartilhamentosLocalizacao
            .FirstOrDefaultAsync(x => x.SessaoId == espera.SessaoId && x.Trecho == espera.Trecho, ct);
        if (c is { EncerradoEm: null }) return Dto(espera, c);

        if (c is null)
        {
            c = new CompartilhamentoLocalizacao { Id = Guid.CreateVersion7(), SessaoId = espera.SessaoId, Trecho = espera.Trecho };
            db.CompartilhamentosLocalizacao.Add(c);
        }
        // Parou e voltou: reabre a mesma linha.
        c.CidadaoSessaoId = cidadaoSessaoId;
        c.Canal = canal;
        c.IniciadoEm = DateTime.UtcNow;
        c.EncerradoEm = null;
        c.MotivoEncerramento = null;
        LimparPosicao(c);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dois toques ao mesmo tempo: vale o que ficou.
            db.ChangeTracker.Clear();
            var corrida = await db.CompartilhamentosLocalizacao.AsNoTracking()
                .FirstOrDefaultAsync(x => x.SessaoId == espera.SessaoId && x.Trecho == espera.Trecho, ct);
            if (corrida is null) throw;
            return Dto(espera, corrida);
        }

        // A prova do aceite (LGPD, art. 8º): quem, quando, qual viagem e por qual canal — sem coordenada.
        await auditoria.RegistrarAsync(
            EntidadeAuditoria, espera.SessaoId.ToString(), "Iniciado", null,
            JsonSerializer.Serialize(new { trecho = espera.Trecho.ToString(), canal, sessaoApp = cidadaoSessaoId }), ct);
        return Dto(espera, c);
    }

    public async Task AtualizarAsync(Guid pacienteId, Guid sessaoId, AtualizarLocalizacaoRequest request, CancellationToken ct = default)
    {
        var agora = DateTime.UtcNow;
        var capturada = Validar(request, agora);

        var c = await DoPacienteAbertos(pacienteId, sessaoId).FirstOrDefaultAsync(ct)
            ?? throw new ConflitoException(
                "localizacao.nao_iniciada", "Toque em “Compartilhar” para começar a enviar a sua localização.");

        var espera = await EsperaAsync(pacienteId, sessaoId, ct);
        if (espera is null || espera.Trecho != c.Trecho)
        {
            Encerrar(c, MotivoFimCompartilhamento.ForaDaJanela, agora);
            await db.SaveChangesAsync(ct);
            throw ForaDaEspera();
        }

        // Rajada (app em dois aparelhos, reenvio): fica a posição anterior.
        if (c.RecebidaEm is { } ultima && agora - ultima < IntervaloMinimo) return;

        c.Latitude = request.Latitude;
        c.Longitude = request.Longitude;
        c.PrecisaoM = request.PrecisaoM;
        c.Simulada = request.Simulada;
        c.CapturadaEm = capturada;
        c.RecebidaEm = agora;
        await db.SaveChangesAsync(ct);
    }

    public async Task PararAsync(Guid pacienteId, Guid sessaoId, CancellationToken ct = default)
    {
        var abertos = await DoPacienteAbertos(pacienteId, sessaoId).ToListAsync(ct);
        if (abertos.Count == 0) return;

        var agora = DateTime.UtcNow;
        foreach (var c in abertos) Encerrar(c, MotivoFimCompartilhamento.PeloPaciente, agora);
        await db.SaveChangesAsync(ct);
        await auditoria.RegistrarAsync(
            EntidadeAuditoria, sessaoId.ToString(), "Encerrado", null,
            JsonSerializer.Serialize(new { motivo = MotivoFimCompartilhamento.PeloPaciente.ToString() }), ct);
    }

    public async Task<IReadOnlyList<LocalizacaoPassageiroDto>> DoTabletAsync(
        DispositivoVeiculo dispositivo, CancellationToken ct = default)
    {
        var rotas = RotasDoVeiculo.DoDia(db, dispositivo.VeiculoId).Select(r => r.Id);
        var paradas = await ParadasDoPacienteAsync(db.Alocacoes.AsNoTracking().Where(a => rotas.Contains(a.RotaDiariaId)), ct);
        var abertos = await AbertosDas(paradas, ct);
        return [.. abertos
            .Where(c => c.Latitude is not null && c.Longitude is not null && c.CapturadaEm is not null)
            .Select(c => new LocalizacaoPassageiroDto(
                c.SessaoId, c.Trecho, c.Latitude!.Value, c.Longitude!.Value, c.PrecisaoM, c.CapturadaEm!.Value, c.Simulada))];
    }

    public async Task<IReadOnlyList<CompartilhamentoDaRotaDto>> DaRotaAsync(Guid rotaId, CancellationToken ct = default)
    {
        var paradas = await ParadasDoPacienteAsync(db.Alocacoes.AsNoTracking().Where(a => a.RotaDiariaId == rotaId), ct);
        var abertos = await AbertosDas(paradas, ct);
        return [.. abertos.Select(c => new CompartilhamentoDaRotaDto(
            c.SessaoId, c.Trecho, c.IniciadoEm, c.CapturadaEm, c.PrecisaoM, c.Simulada))];
    }

    public async Task<int> EncerrarForaDaEsperaAsync(CancellationToken ct = default)
    {
        var abertos = await (
            from c in db.CompartilhamentosLocalizacao
            where c.EncerradoEm == null
            join s in db.Sessoes on c.SessaoId equals s.Id
            join t in db.Tratamentos on s.TratamentoId equals t.Id
            select new { Compartilhamento = c, t.PacienteId }).ToListAsync(ct);

        var agora = DateTime.UtcNow;
        var encerrados = 0;
        foreach (var x in abertos)
        {
            var espera = await EsperaAsync(x.PacienteId, x.Compartilhamento.SessaoId, ct);
            if (espera is not null && espera.Trecho == x.Compartilhamento.Trecho) continue;
            Encerrar(x.Compartilhamento, MotivoFimCompartilhamento.ForaDaJanela, agora);
            encerrados++;
        }
        if (encerrados > 0) await db.SaveChangesAsync(ct);
        return encerrados;
    }

    /// <summary>
    /// A marcação do tablet que tira o paciente da espera: embarcou (ou desceu, se o embarque se perdeu) ou
    /// não estava na coleta. Quem não estava no resgate volta para a lista e continua esperando.
    /// </summary>
    internal static (TrechoViagem Trecho, MotivoFimCompartilhamento Motivo)? FimNaViagem(EventoViagem e) =>
        (e.Resgate, e.Tipo) switch
        {
            (false, TipoEventoViagem.Embarque or TipoEventoViagem.Desembarque) => (TrechoViagem.Ida, MotivoFimCompartilhamento.Embarque),
            (false, TipoEventoViagem.Ausencia) => (TrechoViagem.Ida, MotivoFimCompartilhamento.Ausencia),
            (true, TipoEventoViagem.Embarque or TipoEventoViagem.Desembarque) => (TrechoViagem.Resgate, MotivoFimCompartilhamento.Embarque),
            _ => null,
        };

    /// <summary>Encerra no mesmo <c>SaveChanges</c> da marcação do tablet (ver <c>ViagemService</c>).</summary>
    internal static async Task EncerrarNaViagemAsync(
        SmsMaisDbContext db, Guid sessaoId, TrechoViagem trecho, MotivoFimCompartilhamento motivo, DateTime agora,
        CancellationToken ct)
    {
        var c = await db.CompartilhamentosLocalizacao
            .FirstOrDefaultAsync(x => x.SessaoId == sessaoId && x.Trecho == trecho && x.EncerradoEm == null, ct);
        if (c is not null) Encerrar(c, motivo, agora);
    }

    /// <summary>2 h antes da busca prevista; uma busca antes das 2h abre à meia-noite.</summary>
    internal static TimeOnly AberturaDaIda(TimeOnly busca) =>
        busca.ToTimeSpan() < AntecedenciaIda ? TimeOnly.MinValue : busca.Add(-AntecedenciaIda);

    // ------------------------------------------------------------------------------------------ espera

    private sealed record Espera(Guid SessaoId, TrechoViagem Trecho, string Unidade, TimeOnly? HoraBusca);

    /// <summary>
    /// O embarque que o paciente espera agora, hoje (fuso de Brasília):
    /// <list type="bullet">
    /// <item>o <b>resgate</b>, se ele já desceu na unidade e não embarcou na volta (a mesma regra da lista
    /// de resgate, <see cref="ResgateService.EstaNaUnidade"/>);</item>
    /// <item>a <b>ida</b>, se a sessão é coleta numa rota de hoje (planejada ou em andamento), não foi
    /// cancelada nem marcada como ausente, ainda não embarcou, e já é 2 h antes da busca ou a rota começou.</item>
    /// </list>
    /// </summary>
    private async Task<Espera?> EsperaAsync(Guid pacienteId, Guid? sessaoId, CancellationToken ct)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var agora = TimeOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));
        var sessoes = await (
            from s in db.Sessoes.AsNoTracking()
            join t in db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            join u in db.UnidadesAtendimento.AsNoTracking() on t.UnidadeAtendimentoId equals u.Id
            where t.PacienteId == pacienteId && s.DataPrevista == hoje && (sessaoId == null || s.Id == sessaoId)
            select new { Sessao = s, Unidade = u.Nome }).ToListAsync(ct);
        if (sessoes.Count == 0) return null;

        var resgate = sessoes.FirstOrDefault(x => ResgateService.EstaNaUnidade(x.Sessao, hoje));
        if (resgate is not null) return new Espera(resgate.Sessao.Id, TrechoViagem.Resgate, resgate.Unidade, null);

        var ids = sessoes.Select(x => x.Sessao.Id).ToList();
        var coletas = await db.Alocacoes.AsNoTracking()
            .Where(a => ids.Contains(a.SessaoId) && a.Tipo == TipoAlocacao.Paciente && a.Parada == TipoParada.Coleta
                && a.RotaDiaria!.Data == hoje
                && (a.RotaDiaria.Status == StatusRota.Planejada || a.RotaDiaria.Status == StatusRota.EmAndamento))
            .Select(a => new { a.SessaoId, EmAndamento = a.RotaDiaria!.Status == StatusRota.EmAndamento })
            .ToListAsync(ct);

        var ida = sessoes
            .Where(x => EsperaAIda(x.Sessao)
                && coletas.Any(c => c.SessaoId == x.Sessao.Id
                    && (c.EmAndamento || (x.Sessao.HoraPrevistaBusca is { } busca && agora >= AberturaDaIda(busca)))))
            .OrderBy(x => x.Sessao.HoraPrevistaBusca)
            .FirstOrDefault();
        return ida is null ? null : new Espera(ida.Sessao.Id, TrechoViagem.Ida, ida.Unidade, ida.Sessao.HoraPrevistaBusca);
    }

    private static bool EsperaAIda(SessaoDeTratamento s) =>
        (s.Status is StatusSessao.Pendente or StatusSessao.Confirmada) && s.HoraSaidaResidencia is null;

    // ------------------------------------------------------------------------------------------- apoio

    /// <summary>Os compartilhamentos abertos de uma sessão, só se ela for do paciente (posse pelo token).</summary>
    private IQueryable<CompartilhamentoLocalizacao> DoPacienteAbertos(Guid pacienteId, Guid sessaoId) =>
        from c in db.CompartilhamentosLocalizacao
        join s in db.Sessoes on c.SessaoId equals s.Id
        join t in db.Tratamentos on s.TratamentoId equals t.Id
        where c.SessaoId == sessaoId && t.PacienteId == pacienteId && c.EncerradoEm == null
        select c;

    /// <summary>As paradas de paciente das rotas, com o trecho: coleta é a ida; retorno, o resgate.</summary>
    private static async Task<List<Parada>> ParadasDoPacienteAsync(IQueryable<Alocacao> alocacoes, CancellationToken ct)
    {
        var linhas = await alocacoes
            .Where(a => a.Tipo == TipoAlocacao.Paciente && (a.Parada == TipoParada.Coleta || a.Parada == TipoParada.Retorno))
            .Select(a => new { a.SessaoId, a.Parada })
            .Distinct()
            .ToListAsync(ct);
        return [.. linhas
            .Select(l => new Parada(l.SessaoId, l.Parada == TipoParada.Retorno ? TrechoViagem.Resgate : TrechoViagem.Ida))
            .Distinct()];
    }

    private sealed record Parada(Guid SessaoId, TrechoViagem Trecho);

    /// <summary>Os abertos dessas paradas, no mesmo trecho (a coleta não mostra o compartilhamento do resgate).</summary>
    private async Task<List<CompartilhamentoLocalizacao>> AbertosDas(List<Parada> paradas, CancellationToken ct)
    {
        if (paradas.Count == 0) return [];
        var ids = paradas.Select(p => p.SessaoId).Distinct().ToList();
        var abertos = await db.CompartilhamentosLocalizacao.AsNoTracking()
            .Where(c => ids.Contains(c.SessaoId) && c.EncerradoEm == null)
            .ToListAsync(ct);
        return [.. abertos.Where(c => paradas.Contains(new Parada(c.SessaoId, c.Trecho)))];
    }

    private static DateTime Validar(AtualizarLocalizacaoRequest r, DateTime agora)
    {
        if (!double.IsFinite(r.Latitude) || r.Latitude is < -90 or > 90
            || !double.IsFinite(r.Longitude) || r.Longitude is < -180 or > 180
            || (r.Latitude == 0 && r.Longitude == 0))
        {
            throw new ValidacaoException("latitude", "Posição inválida.");
        }
        if (r.PrecisaoM is { } p && (!double.IsFinite(p) || p is < 0 or > PrecisaoMaximaM))
            throw new ValidacaoException("precisaoM", "Precisão da posição inválida.");

        var capturada = r.CapturadaEm.Kind == DateTimeKind.Local
            ? r.CapturadaEm.ToUniversalTime()
            : DateTime.SpecifyKind(r.CapturadaEm, DateTimeKind.Utc);
        if (capturada < agora - PosicaoAntiga)
            throw new ValidacaoException("capturadaEm", "Posição antiga: medida há mais de 5 minutos.");
        // Relógio do aparelho adiantado não põe a posição no futuro.
        return capturada > agora ? agora : capturada;
    }

    private static void Encerrar(CompartilhamentoLocalizacao c, MotivoFimCompartilhamento motivo, DateTime agora)
    {
        c.EncerradoEm = agora;
        c.MotivoEncerramento = motivo;
        LimparPosicao(c);
    }

    private static void LimparPosicao(CompartilhamentoLocalizacao c)
    {
        c.Latitude = null;
        c.Longitude = null;
        c.PrecisaoM = null;
        c.Simulada = false;
        c.CapturadaEm = null;
    }

    private static ConflitoException ForaDaEspera() => new(
        "localizacao.fora_da_janela",
        "Agora não há um embarque esperando por você. A localização é compartilhada só no dia da viagem, até o embarque.");

    private static LocalizacaoViagemDto Dto(Espera e, CompartilhamentoLocalizacao? c)
    {
        var compartilhando = c is { EncerradoEm: null };
        return new LocalizacaoViagemDto(
            e.SessaoId, e.Trecho, e.Unidade, e.HoraBusca, compartilhando,
            compartilhando ? c!.IniciadoEm : null,
            compartilhando ? c!.RecebidaEm : null);
    }
}
