using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Rastreamento;
using SMSMais.Core.Rastreamento.Dispositivos;
using SMSMais.Core.Translado.Geracao;
using SMSMais.Core.Translado.Lotacao;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Resgate;

/// <summary>
/// Resgate: buscar o paciente na unidade depois do atendimento e deixá-lo em casa (decisões de 19/10).
/// <list type="bullet">
/// <item>Quem desembarca na unidade entra na lista <b>"Pacientes disponíveis para resgate"</b>, com a hora
/// em que fica pronto pela previsão (chegada + tempo do procedimento, ou a hora que a central marcou).</item>
/// <item>O paciente (app/PWA), a central (painel) ou um motorista no local avisam que ele <b>já está esperando</b>:
/// ele sobe para o topo da lista.</item>
/// <item>Todos os motoristas veem a mesma lista e marcam quem vão buscar, mesmo que não o tenham levado; a
/// central também marca, para qualquer rota de hoje. Quem marcar primeiro fica com o paciente.</item>
/// <item>A marcação só passa se o paciente e os acompanhantes couberem no carro naquela hora
/// (<see cref="LotacaoDaRota"/>).</item>
/// </list>
/// A marcação é a alocação de resgate (<see cref="TipoParada.Retorno"/>) na rota de quem vai buscar.
/// </summary>
public interface IResgateService
{
    /// <summary>A lista de resgate. Com <paramref name="rotaId"/>, também os lugares livres agora no carro da
    /// rota, se cada um cabe e quem ela já marcou.</summary>
    Task<ResgatesDto> ListarAsync(Guid? rotaId, CancellationToken ct = default);

    /// <summary>A central marca quem a rota vai resgatar.</summary>
    Task MarcarAsync(Guid rotaId, IReadOnlyList<Guid> sessaoIds, CancellationToken ct = default);

    /// <summary>Tira o paciente do resgate da rota (antes de ele embarcar): ele volta para a lista.</summary>
    Task DesmarcarAsync(Guid rotaId, Guid sessaoId, CancellationToken ct = default);

    /// <summary>Avisa que o paciente já está no local esperando o resgate.</summary>
    Task AvisarNoLocalAsync(Guid sessaoId, CanalAvisoResgate canal, CancellationToken ct = default);

    Task<ResgatesDto> ListarDoTabletAsync(DispositivoVeiculo dispositivo, Guid rotaId, CancellationToken ct = default);
    Task MarcarDoTabletAsync(DispositivoVeiculo dispositivo, MarcarResgateRequest request, CancellationToken ct = default);
    Task DesmarcarDoTabletAsync(DispositivoVeiculo dispositivo, Guid rotaId, Guid sessaoId, CancellationToken ct = default);

    /// <summary>O resgate de hoje do paciente (nulo quando não há: ainda não foi à unidade, ou já voltou).</summary>
    Task<ResgateDoPacienteDto?> DoPacienteAsync(Guid pacienteId, CancellationToken ct = default);

    /// <summary>Resposta à pergunta "você já está disponível para retorno?": sim = está no local; não =
    /// ainda em atendimento, e a pergunta volta em <see cref="ResgateService.IntervaloDaPergunta"/>.</summary>
    Task ResponderPeloPacienteAsync(Guid pacienteId, Guid sessaoId, bool disponivel, CancellationToken ct = default);

    /// <summary>Envia a pergunta a quem tem a previsão estourada e ainda não disse que está disponível
    /// (uma vez por rodada). Devolve quantos foram perguntados.</summary>
    Task<int> PerguntarQuemPassouDaPrevisaoAsync(CancellationToken ct = default);
}

/// <summary>Por onde a pergunta "você já está disponível para retorno?" chega ao paciente fora do app
/// aberto (push). Sem canal ligado, ela aparece no app e no PWA quando o paciente abre a tela do
/// Transporte — com os mesmos botões.</summary>
public interface IEntregaPerguntaResgate
{
    /// <summary>Verdadeiro quando entregou (ex.: o push saiu para algum aparelho).</summary>
    Task<bool> EnviarAsync(Guid pacienteId, string primeiroNome, Guid sessaoId, CancellationToken ct = default);
}

/// <summary>Enquanto o push do app do cidadão (ADR-0070) não está ligado: a pergunta fica só no app/PWA.</summary>
public sealed class EntregaPerguntaResgateSoNoApp : IEntregaPerguntaResgate
{
    public Task<bool> EnviarAsync(Guid pacienteId, string primeiroNome, Guid sessaoId, CancellationToken ct = default) =>
        Task.FromResult(false);
}

public sealed class ResgateService(
    SmsMaisDbContext db,
    IPacienteResolver pacienteResolver,
    IRastreamentoNotificador notificador,
    IEntregaPerguntaResgate entrega,
    ILogger<ResgateService> logger) : IResgateService
{
    /// <summary>Quantos pacientes de uma vez numa marcação (o carro maior não leva mais que isso).</summary>
    private const int MaximoPorMarcacao = 30;

    /// <summary>Depois de "Não, ainda não terminei o procedimento", a pergunta volta neste tempo.</summary>
    public static readonly TimeSpan IntervaloDaPergunta = TimeSpan.FromMinutes(10);

    /// <summary>A pergunta só dá para hoje: previsão de mais de 12 h atrás é dado velho, não pergunta.</summary>
    private static readonly TimeSpan JanelaDaPergunta = TimeSpan.FromHours(12);

    public async Task<ResgatesDto> ListarAsync(Guid? rotaId, CancellationToken ct = default)
    {
        RotaDiaria? rota = null;
        if (rotaId is { } id)
        {
            rota = await db.Rotas.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct)
                ?? throw new NaoEncontradoException(nameof(RotaDiaria), id);
        }
        return await MontarAsync(rota, ct);
    }

    public async Task<ResgatesDto> ListarDoTabletAsync(DispositivoVeiculo dispositivo, Guid rotaId, CancellationToken ct = default) =>
        await MontarAsync(await RotaDoTabletAsync(dispositivo, rotaId, ct), ct);

    public Task MarcarAsync(Guid rotaId, IReadOnlyList<Guid> sessaoIds, CancellationToken ct = default) =>
        MarcarNaRotaAsync(rotaId, sessaoIds, ct);

    public async Task MarcarDoTabletAsync(DispositivoVeiculo dispositivo, MarcarResgateRequest request, CancellationToken ct = default)
    {
        var rota = await RotaDoTabletAsync(dispositivo, request.RotaId, ct);
        await MarcarNaRotaAsync(rota.Id, request.SessaoIds ?? [], ct);
    }

    public async Task DesmarcarDoTabletAsync(DispositivoVeiculo dispositivo, Guid rotaId, Guid sessaoId, CancellationToken ct = default)
    {
        var rota = await RotaDoTabletAsync(dispositivo, rotaId, ct);
        await DesmarcarAsync(rota.Id, sessaoId, ct);
    }

    public async Task DesmarcarAsync(Guid rotaId, Guid sessaoId, CancellationToken ct = default)
    {
        var alocacoes = await db.Alocacoes
            .Where(a => a.RotaDiariaId == rotaId && a.SessaoId == sessaoId && a.Parada == TipoParada.Retorno)
            .ToListAsync(ct);
        if (alocacoes.Count == 0)
            throw new NaoEncontradoException("Resgate", sessaoId);

        var s = await db.Sessoes.AsNoTracking().FirstAsync(x => x.Id == sessaoId, ct);
        if (s.HoraSaidaUnidade is not null)
        {
            throw new ConflitoException("resgate.ja_embarcou",
                "O paciente já embarcou no resgate; não dá para desmarcar.");
        }

        db.Alocacoes.RemoveRange(alocacoes);
        await db.SaveChangesAsync(ct);
        await notificador.RotaAtualizadaAsync(rotaId, ct);
    }

    public async Task AvisarNoLocalAsync(Guid sessaoId, CanalAvisoResgate canal, CancellationToken ct = default)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var s = await db.Sessoes.FirstOrDefaultAsync(x => x.Id == sessaoId, ct)
            ?? throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);
        if (!EstaNaUnidade(s, hoje))
        {
            throw new ConflitoException("resgate.fora_do_resgate",
                "Este paciente não está esperando resgate hoje (a ida não foi feita, ou ele já voltou).");
        }

        // O primeiro aviso vale: é desde quando ele espera.
        if (s.ProntoParaResgateEm is null)
        {
            s.ProntoParaResgateEm = DateTime.UtcNow;
            s.ProntoParaResgateCanal = canal;
            s.AindaEmAtendimentoEm = null;
            s.AtualizadoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<ResgateDoPacienteDto?> DoPacienteAsync(Guid pacienteId, CancellationToken ct = default)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var agora = AgoraEmBrasilia();
        var c = await Candidatos(hoje)
            .Where(x => x.PacienteId == pacienteId)
            .OrderByDescending(x => x.HoraChegadaUnidade)
            .FirstOrDefaultAsync(ct);
        if (c is null)
        {
            // Já embarcou no resgate e está a caminho de casa.
            var aCaminho = await (
                from s in db.Sessoes.AsNoTracking()
                join t in db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
                join u in db.UnidadesAtendimento.AsNoTracking() on t.UnidadeAtendimentoId equals u.Id
                join v in db.Veiculos.AsNoTracking() on s.VeiculoVoltaId equals (Guid?)v.Id into vs
                from v in vs.DefaultIfEmpty()
                where t.PacienteId == pacienteId && s.DataPrevista == hoje
                    && s.HoraSaidaUnidade != null && s.HoraChegadaResidencia == null
                select new ResgateDoPacienteDto(
                    s.Id, u.Nome, DisponibilidadeResgate.NoLocal, null, s.ProntoParaResgateEm, s.ProntoParaResgateCanal,
                    v == null ? null : v.Placa, v == null ? null : v.Modelo, true)).FirstOrDefaultAsync(ct);
            return aCaminho;
        }

        var carro = await (
            from a in db.Alocacoes.AsNoTracking()
            where a.SessaoId == c.SessaoId && a.Parada == TipoParada.Retorno && a.Tipo == TipoAlocacao.Paciente
            join r in db.Rotas.AsNoTracking() on a.RotaDiariaId equals r.Id
            where r.Status != StatusRota.Cancelada
            join v in db.Veiculos.AsNoTracking() on r.VeiculoId equals v.Id
            select new { v.Placa, v.Modelo }).FirstOrDefaultAsync(ct);
        var (disp, asHoras, _) = Disponibilidade(c, agora);
        var proxima = c.AindaEmAtendimentoEm is { } nao && c.ProntoParaResgateEm is null ? nao + IntervaloDaPergunta : (DateTime?)null;
        return new ResgateDoPacienteDto(
            c.SessaoId, c.UnidadeNome, disp, asHoras, c.ProntoParaResgateEm, c.ProntoParaResgateCanal,
            carro?.Placa, carro?.Modelo, false,
            PerguntaPendente: PerguntaPendente(c, DateTime.UtcNow),
            ProximaPerguntaAs: proxima > DateTime.UtcNow ? proxima : null);
    }

    public async Task ResponderPeloPacienteAsync(Guid pacienteId, Guid sessaoId, bool disponivel, CancellationToken ct = default)
    {
        await GarantirDoPacienteAsync(pacienteId, sessaoId, ct);
        if (disponivel)
        {
            await AvisarNoLocalAsync(sessaoId, CanalAvisoResgate.Paciente, ct);
            return;
        }

        var hoje = FusoBrasilia.HojeEmBrasilia();
        var s = await db.Sessoes.FirstAsync(x => x.Id == sessaoId, ct);
        if (!EstaNaUnidade(s, hoje))
        {
            throw new ConflitoException("resgate.fora_do_resgate",
                "Este paciente não está esperando resgate hoje (a ida não foi feita, ou ele já voltou).");
        }
        // "Ainda não terminei" depois de ter dito que sim (ou de alguém ter avisado) desfaz o aviso.
        s.ProntoParaResgateEm = null;
        s.ProntoParaResgateCanal = null;
        s.AindaEmAtendimentoEm = DateTime.UtcNow;
        s.AtualizadoEm = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<int> PerguntarQuemPassouDaPrevisaoAsync(CancellationToken ct = default)
    {
        var agoraUtc = DateTime.UtcNow;
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var pendentes = (await Candidatos(hoje).Where(c => c.ProntoParaResgateEm == null).ToListAsync(ct))
            .Where(c => PerguntaPendente(c, agoraUtc) && !JaPerguntadoNestaRodada(c, agoraUtc))
            .ToList();
        if (pendentes.Count == 0) return 0;

        var nomes = await NomesAsync(pendentes.Select(c => c.PacienteId), ct);
        var perguntados = 0;
        foreach (var c in pendentes)
        {
            var nome = nomes.TryGetValue(c.PacienteId, out var r) ? PrimeiroNome(r.Nome) : string.Empty;
            try
            {
                if (await entrega.EnviarAsync(c.PacienteId, nome, c.SessaoId, ct)) perguntados++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Resgate: pergunta de disponibilidade da sessão {Sessao} não enviada.", c.SessaoId);
            }
            // Marca a rodada mesmo sem canal: a pergunta segue aparecendo no app, sem reenviar a cada minuto.
            await db.Sessoes.Where(s => s.Id == c.SessaoId)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.PerguntaResgateEnviadaEm, agoraUtc), ct);
        }
        return perguntados;
    }

    /// <summary>Hora de perguntar: a previsão estourou e ninguém disse que ele está no local; depois de um
    /// "não", só 10 minutos depois.</summary>
    private static bool PerguntaPendente(Candidato c, DateTime agoraUtc)
    {
        if (c.ProntoParaResgateEm is not null) return false;
        if (c.AindaEmAtendimentoEm is { } nao) return agoraUtc >= nao + IntervaloDaPergunta;
        return PrevistoEmUtc(c) is { } previsto && agoraUtc >= previsto && agoraUtc - previsto <= JanelaDaPergunta;
    }

    /// <summary>A rodada começa quando a previsão estoura ou 10 minutos depois do último "não".</summary>
    private static bool JaPerguntadoNestaRodada(Candidato c, DateTime agoraUtc)
    {
        var inicio = c.AindaEmAtendimentoEm is { } nao ? nao + IntervaloDaPergunta : PrevistoEmUtc(c);
        return c.PerguntaResgateEnviadaEm is { } enviada && inicio is { } i && enviada >= i;
    }

    /// <summary>A previsão de hoje (hora de Brasília) como instante UTC.</summary>
    private static DateTime? PrevistoEmUtc(Candidato c)
    {
        var previsto = RotaTabletService.RetornoPrevisto(c.HoraPrevistaRetorno, c.HoraChegadaUnidade, c.TempoMedioMinutos);
        if (previsto is not { } p) return null;
        var hoje = FusoBrasilia.HojeEmBrasilia();
        return FusoBrasilia.DeBrasiliaParaUtc(hoje.ToDateTime(p));
    }

    private static string PrimeiroNome(string? nome)
    {
        var p = (nome ?? string.Empty).Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return p.Length == 0 ? string.Empty : char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant();
    }

    /// <summary>Sessão de outra pessoa responde como inexistente (não revela que existe).</summary>
    private async Task GarantirDoPacienteAsync(Guid pacienteId, Guid sessaoId, CancellationToken ct)
    {
        var doPaciente = await (
            from s in db.Sessoes.AsNoTracking()
            join t in db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            where s.Id == sessaoId && t.PacienteId == pacienteId
            select s.Id).AnyAsync(ct);
        if (!doPaciente) throw new NaoEncontradoException(nameof(SessaoDeTratamento), sessaoId);
    }

    // ------------------------------------------------------------------------------------- marcação

    private async Task MarcarNaRotaAsync(Guid rotaId, IReadOnlyList<Guid> sessaoIds, CancellationToken ct)
    {
        var ids = sessaoIds.Distinct().ToList();
        if (ids.Count == 0) throw new ValidacaoException("sessaoIds", "Escolha quem vai ser resgatado.");
        if (ids.Count > MaximoPorMarcacao)
            throw new ValidacaoException("sessaoIds", $"Marque no máximo {MaximoPorMarcacao} pacientes de uma vez.");

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // Trava a rota (os lugares do carro) e as sessões (quem marcar primeiro fica com o paciente),
            // sempre na mesma ordem: duas marcações ao mesmo tempo esperam uma pela outra.
            var idsOrdenados = ids.Order().ToArray();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM smsmarica.translado_rota_diaria WHERE id = {rotaId} FOR UPDATE", ct);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM smsmarica.sessao_de_tratamento WHERE id = ANY({idsOrdenados}) ORDER BY id FOR UPDATE", ct);

            var rota = await db.Rotas.FirstOrDefaultAsync(r => r.Id == rotaId, ct)
                ?? throw new NaoEncontradoException(nameof(RotaDiaria), rotaId);
            GarantirRotaAberta(rota);

            var hoje = FusoBrasilia.HojeEmBrasilia();
            var candidatos = await Candidatos(hoje).Where(c => ids.Contains(c.SessaoId)).ToListAsync(ct);
            var marcados = await MarcacoesAsync(ids, ct);
            foreach (var id in ids)
            {
                if (marcados.TryGetValue(id, out var outra))
                {
                    throw new ConflitoException("resgate.ja_marcado", outra.RotaId == rotaId
                        ? "Este paciente já está no resgate desta rota."
                        : $"Outro carro ({outra.Placa}) já marcou este paciente para resgate. Atualize a lista.");
                }
                if (candidatos.All(c => c.SessaoId != id))
                {
                    throw new ConflitoException("resgate.indisponivel",
                        "Este paciente não está mais na lista de resgate (já voltou, ou a ida não foi feita). Atualize a lista.");
                }
            }

            var lotacao = await LotacaoDaRota.AgoraAsync(db, rota, ct);
            var quem = candidatos.Select(Necessidade).ToList();
            var lugares = AcomodacaoNoVeiculo.Acomodar(lotacao.Veiculo, lotacao.Livres, quem, lotacao.ABordo)
                ?? throw new ConflitoException("resgate.sem_lugar",
                    AcomodacaoNoVeiculo.MotivoNaoCabe(lotacao.Veiculo, lotacao.Livres, quem, lotacao.ABordo));

            // Quem é buscado na mesma unidade que um resgate ainda não feito desta rota entra na mesma parada.
            var pendentes = await (
                from a in db.Alocacoes.AsNoTracking()
                where a.RotaDiariaId == rotaId && a.Parada == TipoParada.Retorno && a.Tipo == TipoAlocacao.Paciente
                join s in db.Sessoes.AsNoTracking() on a.SessaoId equals s.Id
                where s.HoraSaidaUnidade == null
                join t in db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
                select new { t.UnidadeAtendimentoId, a.OrdemParada }).ToListAsync(ct);
            var paradaDaUnidade = pendentes
                .Where(p => p.OrdemParada is not null)
                .GroupBy(p => p.UnidadeAtendimentoId)
                .ToDictionary(g => g.Key, g => g.Min(p => p.OrdemParada!.Value));
            var lote = (await db.Alocacoes.Where(a => a.RotaDiariaId == rotaId)
                .MaxAsync(a => (int?)a.OrdemParada, ct) ?? 0) + 1;

            var agora = DateTime.UtcNow;
            foreach (var c in candidatos)
            {
                var ordem = paradaDaUnidade.GetValueOrDefault(c.UnidadeId, lote);
                var lugar = lugares[c.SessaoId];
                db.Alocacoes.Add(Resgate(rotaId, c.SessaoId, lugar.Paciente.Id, TipoAlocacao.Paciente, ordem, agora));
                foreach (var a in lugar.Acompanhantes)
                    db.Alocacoes.Add(Resgate(rotaId, c.SessaoId, a.Id, TipoAlocacao.Acompanhante, ordem, agora));
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        await notificador.RotaAtualizadaAsync(rotaId, ct);
    }

    private static Alocacao Resgate(Guid rotaId, Guid sessaoId, Guid assentoId, TipoAlocacao tipo, int ordem, DateTime agora) => new()
    {
        Id = Guid.CreateVersion7(),
        RotaDiariaId = rotaId,
        SessaoId = sessaoId,
        AssentoId = assentoId,
        Tipo = tipo,
        Parada = TipoParada.Retorno,
        OrdemParada = ordem,
        CriadoEm = agora,
    };

    /// <summary>Tira do resgate da rota quem ainda não embarcou nele — volta para a lista. Usado quando o
    /// motorista não encontra o paciente no local, quando a rota é encerrada e quando é cancelada.</summary>
    internal static async Task LiberarAsync(SmsMaisDbContext db, Guid rotaId, Guid? sessaoId, CancellationToken ct)
    {
        var alocacoes = await (
            from a in db.Alocacoes
            where a.RotaDiariaId == rotaId && a.Parada == TipoParada.Retorno
                && (sessaoId == null || a.SessaoId == sessaoId)
            join s in db.Sessoes on a.SessaoId equals s.Id
            where s.HoraSaidaUnidade == null
            select a).ToListAsync(ct);
        db.Alocacoes.RemoveRange(alocacoes);
    }

    private static void GarantirRotaAberta(RotaDiaria rota)
    {
        if (rota.Status is StatusRota.Concluida or StatusRota.Cancelada)
        {
            throw new ConflitoException("resgate.rota_encerrada",
                "Esta rota já foi encerrada. O resgate precisa de uma rota planejada ou em andamento.");
        }
        var hoje = FusoBrasilia.HojeEmBrasilia();
        if (rota.Data != hoje && !(rota.Data == hoje.AddDays(-1) && rota.Status == StatusRota.EmAndamento))
        {
            throw new ConflitoException("resgate.rota_de_outro_dia", "Só dá para marcar resgate numa rota de hoje.");
        }
    }

    /// <summary>A rota do próprio veículo do tablet, de hoje (como a Rota do dia).</summary>
    private async Task<RotaDiaria> RotaDoTabletAsync(DispositivoVeiculo dispositivo, Guid rotaId, CancellationToken ct) =>
        await RotasDoVeiculo.DoDia(db, dispositivo.VeiculoId).AsNoTracking().FirstOrDefaultAsync(r => r.Id == rotaId, ct)
            ?? throw new NaoEncontradoException(nameof(RotaDiaria), rotaId);

    // ------------------------------------------------------------------------------------- lista

    private async Task<ResgatesDto> MontarAsync(RotaDiaria? rota, CancellationToken ct)
    {
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var agora = AgoraEmBrasilia();
        var agoraUtc = DateTime.UtcNow;
        var candidatos = await Candidatos(hoje).ToListAsync(ct);
        var ids = candidatos.Select(c => c.SessaoId).ToList();
        var marcados = await MarcacoesAsync(ids, ct);

        var naoEstava = await db.EventosViagem.AsNoTracking()
            .Where(e => e.Resgate && e.Tipo == TipoEventoViagem.Ausencia && e.Pessoa == PessoaViagem.Paciente
                && e.SessaoId != null && ids.Contains(e.SessaoId.Value))
            .GroupBy(e => e.SessaoId!.Value)
            .Select(g => new { SessaoId = g.Key, Em = g.Max(e => e.OcorridoEm) })
            .ToDictionaryAsync(x => x.SessaoId, x => x.Em, ct);

        var nomes = await NomesAsync(candidatos.Select(c => c.PacienteId), ct);
        string Nome(Candidato c) => nomes.TryGetValue(c.PacienteId, out var r) && !string.IsNullOrWhiteSpace(r.Nome) ? r.Nome : "Paciente";

        OcupacaoDoCarro? lotacao = null;
        (double Lat, double Lng)? origem = null;
        var aberta = rota is not null && rota.Status is not (StatusRota.Concluida or StatusRota.Cancelada);
        if (rota is not null)
        {
            if (aberta) lotacao = await LotacaoDaRota.AgoraAsync(db, rota, ct);
            origem = await PosicaoAsync(rota, ct);
        }

        var disponiveis = candidatos
            .Where(c => !marcados.ContainsKey(c.SessaoId))
            .Select(c =>
            {
                var (disp, asHoras, minutos) = Disponibilidade(c, agora);
                bool? cabe = null;
                string? motivo = null;
                if (lotacao is not null)
                {
                    var q = Necessidade(c);
                    cabe = AcomodacaoNoVeiculo.Acomodar(lotacao.Veiculo, lotacao.Livres, [q], lotacao.ABordo) is not null;
                    if (cabe == false) motivo = AcomodacaoNoVeiculo.MotivoNaoCabe(lotacao.Veiculo, lotacao.Livres, [q], lotacao.ABordo);
                }
                return new ResgateDisponivelDto(
                    c.SessaoId, Nome(c), c.UnidadeId, c.UnidadeNome, RotaTabletService.Formatar(c.UnidadeEndereco),
                    c.Latitude, c.Longitude, c.UnidadeExterna, Acompanhantes(c), c.Mobilidade, c.Isolamento,
                    disp, asHoras, minutos, c.ProntoParaResgateEm,
                    c.ProntoParaResgateEm is { } desde ? Math.Max(0, (int)(agoraUtc - desde).TotalMinutes) : null,
                    c.ProntoParaResgateCanal,
                    naoEstava.TryGetValue(c.SessaoId, out var em) ? em : null,
                    c.AindaEmAtendimentoEm,
                    c.VeiculoDaIda,
                    origem is { } o && c.Latitude is { } la && c.Longitude is { } lo ? (int)DistanciaMetros(o.Lat, o.Lng, la, lo) : null,
                    cabe, motivo);
            })
            .OrderBy(d => (int)d.Disponibilidade)
            .ThenBy(d => d.Disponibilidade == DisponibilidadeResgate.NoLocal ? d.NoLocalDesde : null)
            .ThenBy(d => d.DisponivelAs ?? TimeOnly.MaxValue)
            .ThenBy(d => d.DistanciaMetros ?? int.MaxValue)
            .ToList();

        var daRota = rota is null ? [] : candidatos
            .Where(c => marcados.TryGetValue(c.SessaoId, out var m) && m.RotaId == rota.Id)
            .Select(c =>
            {
                var (disp, asHoras, minutos) = Disponibilidade(c, agora);
                return new ResgateMarcadoDto(c.SessaoId, Nome(c), c.UnidadeId, c.UnidadeNome, Acompanhantes(c), disp, asHoras, minutos);
            })
            .ToList();

        return new ResgatesDto(
            disponiveis, daRota,
            lotacao?.Livres.Count,
            lotacao?.Livres.Count(a => a.Tipo == TipoAssento.Cadeirante),
            lotacao is null ? null : new CarroAgoraDto(
                lotacao.Livres.Count(a => a.Tipo == TipoAssento.Passageiro),
                lotacao.Livres.Count(a => a.Tipo == TipoAssento.Acompanhante),
                lotacao.Livres.Count(a => a.Tipo == TipoAssento.Cadeirante),
                lotacao.Veiculo.Tipo == TipoVeiculo.Ambulancia,
                lotacao.ABordo.Count,
                lotacao.ABordo.Any(q => q.Isolamento),
                lotacao.ABordo.Any(q => q.Mobilidade == MobilidadeTransporte.Maca)));
    }

    /// <summary>Quem já foi levado à unidade hoje e ainda não foi resgatado (marcado ou não).</summary>
    private IQueryable<Candidato> Candidatos(DateOnly hoje) =>
        from s in db.Sessoes.AsNoTracking()
        join t in db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
        join u in db.UnidadesAtendimento.AsNoTracking() on t.UnidadeAtendimentoId equals u.Id
        join tt in db.TiposTratamento.AsNoTracking() on t.TipoTratamentoId equals (Guid?)tt.Id into tts
        from tt in tts.DefaultIfEmpty()
        join vi in db.Veiculos.AsNoTracking() on s.VeiculoIdaId equals (Guid?)vi.Id into vis
        from vi in vis.DefaultIfEmpty()
        where s.DataPrevista == hoje
            && (s.Status == StatusSessao.AguardandoRetorno
                || (s.Status == StatusSessao.Realizada && s.HoraChegadaUnidade != null))
            && s.HoraSaidaUnidade == null && s.HoraChegadaResidencia == null
        select new Candidato
        {
            SessaoId = s.Id,
            PacienteId = t.PacienteId,
            UnidadeId = u.Id,
            UnidadeNome = u.Nome,
            UnidadeEndereco = u.Endereco,
            Latitude = u.Gps == null ? null : (double?)u.Gps!.Latitude,
            Longitude = u.Gps == null ? null : (double?)u.Gps!.Longitude,
            UnidadeExterna = u.Externa,
            AcompanhanteEsperado = s.AcompanhanteEsperado,
            AcompanhantesEscolhidos = s.Acompanhantes.Count,
            LimiteAcompanhantes = t.QuantidadeAcompanhantes,
            Mobilidade = t.Mobilidade,
            Isolamento = t.Isolamento,
            DificuldadeVeiculoAlto = t.DificuldadeVeiculoAlto,
            HoraPrevistaRetorno = s.HoraPrevistaRetorno,
            HoraChegadaUnidade = s.HoraChegadaUnidade,
            TempoMedioMinutos = tt == null ? null : tt.TempoMedioMinutos,
            ProntoParaResgateEm = s.ProntoParaResgateEm,
            ProntoParaResgateCanal = s.ProntoParaResgateCanal,
            AindaEmAtendimentoEm = s.AindaEmAtendimentoEm,
            PerguntaResgateEnviadaEm = s.PerguntaResgateEnviadaEm,
            VeiculoDaIda = vi == null ? null : vi.Placa,
        };

    /// <summary>Quem já tem resgate marcado (em rota não cancelada), com a rota e a placa do carro.</summary>
    private async Task<Dictionary<Guid, (Guid RotaId, string Placa)>> MarcacoesAsync(List<Guid> ids, CancellationToken ct)
    {
        var linhas = await (
            from a in db.Alocacoes.AsNoTracking()
            where a.Parada == TipoParada.Retorno && a.Tipo == TipoAlocacao.Paciente && ids.Contains(a.SessaoId)
            join r in db.Rotas.AsNoTracking() on a.RotaDiariaId equals r.Id
            where r.Status != StatusRota.Cancelada
            join v in db.Veiculos.AsNoTracking() on r.VeiculoId equals v.Id
            select new { a.SessaoId, RotaId = r.Id, v.Placa }).ToListAsync(ct);
        return linhas.GroupBy(l => l.SessaoId).ToDictionary(g => g.Key, g => (g.First().RotaId, g.First().Placa));
    }

    /// <summary>Onde o carro da rota está: a última posição de hoje do tablet; sem ela, a do motorista.</summary>
    private async Task<(double Lat, double Lng)?> PosicaoAsync(RotaDiaria rota, CancellationToken ct)
    {
        var inicio = FusoBrasilia.InicioDoDiaAtualEmUtc();
        var tablet = await db.PosicoesVeiculo.AsNoTracking()
            .Where(p => p.VeiculoId == rota.VeiculoId && p.CapturadoEm >= inicio)
            .OrderByDescending(p => p.CapturadoEm)
            .Select(p => new { p.Coordenada.Latitude, p.Coordenada.Longitude })
            .FirstOrDefaultAsync(ct);
        if (tablet is not null) return (tablet.Latitude, tablet.Longitude);
        var motorista = await db.PontosGps.AsNoTracking()
            .Where(p => p.MotoristaId == rota.MotoristaId && p.CapturadoEm >= inicio)
            .OrderByDescending(p => p.CapturadoEm)
            .Select(p => new { p.Coordenada.Latitude, p.Coordenada.Longitude })
            .FirstOrDefaultAsync(ct);
        return motorista is null ? null : (motorista.Latitude, motorista.Longitude);
    }

    private async Task<IReadOnlyDictionary<Guid, PacienteResumo>> NomesAsync(IEnumerable<Guid> ids, CancellationToken ct)
    {
        try
        {
            return await pacienteResolver.ResolverManyAsync(ids.Distinct(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Resgate: hub FHIR indisponível; lista sem os nomes.");
            return new Dictionary<Guid, PacienteResumo>();
        }
    }

    private static bool EstaNaUnidade(SessaoDeTratamento s, DateOnly hoje) =>
        s.DataPrevista == hoje
        && (s.Status == StatusSessao.AguardandoRetorno || (s.Status == StatusSessao.Realizada && s.HoraChegadaUnidade is not null))
        && s.HoraSaidaUnidade is null && s.HoraChegadaResidencia is null;

    private static NecessidadeViagem Necessidade(Candidato c) =>
        new(c.SessaoId, c.Mobilidade, c.Isolamento, c.DificuldadeVeiculoAlto, Acompanhantes(c));

    private static int Acompanhantes(Candidato c) =>
        AcomodacaoNoVeiculo.AcompanhantesDaViagem(c.AcompanhanteEsperado, c.AcompanhantesEscolhidos, c.LimiteAcompanhantes);

    /// <summary>Avisaram que está no local → disponível agora. Senão, pela previsão: a hora que a central
    /// marcou, ou a chegada à unidade mais o tempo médio do procedimento.</summary>
    private static (DisponibilidadeResgate Disponibilidade, TimeOnly? As, int? EmMinutos) Disponibilidade(Candidato c, TimeOnly agora)
    {
        var previsto = RotaTabletService.RetornoPrevisto(c.HoraPrevistaRetorno, c.HoraChegadaUnidade, c.TempoMedioMinutos);
        if (c.ProntoParaResgateEm is not null) return (DisponibilidadeResgate.NoLocal, previsto, null);
        // Disse que ainda não terminou: a próxima pergunta, em 10 minutos, é a nova previsão.
        if (c.AindaEmAtendimentoEm is { } nao)
        {
            var proxima = TimeOnly.FromDateTime(FusoBrasilia.ParaExibicao(nao + IntervaloDaPergunta));
            if (proxima > agora) return (DisponibilidadeResgate.EmAtendimento, proxima, (int)Math.Ceiling((proxima - agora).TotalMinutes));
            return (DisponibilidadeResgate.PelaPrevisao, proxima, 0);
        }
        if (previsto is not { } p) return (DisponibilidadeResgate.SemPrevisao, null, null);
        if (p <= agora) return (DisponibilidadeResgate.PelaPrevisao, p, 0);
        return (DisponibilidadeResgate.EmAtendimento, p, (int)Math.Ceiling((p - agora).TotalMinutes));
    }

    private static TimeOnly AgoraEmBrasilia() => TimeOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));

    internal static double DistanciaMetros(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6_371_000; // raio da Terra (m)
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
               * Math.Sin(dLon / 2) * Math.Sin(dLon / 2));
        return r * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>Classe com <c>init</c> (e não record posicional): o EF só compõe Where/OrderBy depois do
    /// Select quando a projeção é por propriedades.</summary>
    private sealed class Candidato
    {
        public Guid SessaoId { get; init; }
        public Guid PacienteId { get; init; }
        public Guid UnidadeId { get; init; }
        public string UnidadeNome { get; init; } = string.Empty;
        public Endereco? UnidadeEndereco { get; init; }
        public double? Latitude { get; init; }
        public double? Longitude { get; init; }
        public bool UnidadeExterna { get; init; }
        public bool? AcompanhanteEsperado { get; init; }
        public int AcompanhantesEscolhidos { get; init; }
        public int LimiteAcompanhantes { get; init; }
        public MobilidadeTransporte Mobilidade { get; init; }
        public bool Isolamento { get; init; }
        public bool DificuldadeVeiculoAlto { get; init; }
        public TimeOnly? HoraPrevistaRetorno { get; init; }
        public TimeOnly? HoraChegadaUnidade { get; init; }
        public int? TempoMedioMinutos { get; init; }
        public DateTime? ProntoParaResgateEm { get; init; }
        public CanalAvisoResgate? ProntoParaResgateCanal { get; init; }
        public DateTime? AindaEmAtendimentoEm { get; init; }
        public DateTime? PerguntaResgateEnviadaEm { get; init; }
        public string? VeiculoDaIda { get; init; }
    }
}
