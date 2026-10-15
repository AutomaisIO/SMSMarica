using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Identidade;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Rastreamento.Ocorrencias;

// Botão REPORT do tablet do veículo → central no painel. Contrato: docs/modulos/tfd/deslocamento-tablet.md.

public sealed record RegistrarOcorrenciaRequest(
    Guid IdLocal,
    TipoOcorrenciaVeiculo Tipo,
    string? Descricao,
    double? Latitude,
    double? Longitude,
    double? PrecisaoM,
    Guid? RotaId,
    DateTime OcorridaEm);

public sealed record OcorrenciaRegistradaDto(Guid Id, DateTime RecebidaEm);

public sealed record OcorrenciaVeiculoDto(
    Guid Id,
    Guid VeiculoId,
    string VeiculoPlaca,
    string VeiculoModelo,
    TipoOcorrenciaVeiculo Tipo,
    string? Descricao,
    double? Latitude,
    double? Longitude,
    double? PrecisaoM,
    Guid? RotaId,
    DateTime OcorridaEm,
    DateTime RecebidaEm,
    SituacaoOcorrenciaVeiculo Situacao,
    DateTime? AtendidaEm,
    string? AtendidaPorNome,
    DateTime? EncerradaEm,
    string? EncerradaPorNome,
    string? ObservacaoCentral);

public sealed record EncerrarOcorrenciaRequest(string? Observacao);

public interface IOcorrenciaVeiculoService
{
    /// <summary>Registra o REPORT do tablet. Reenvio com o mesmo <c>IdLocal</c> devolve a mesma
    /// ocorrência (a fila offline do tablet pode mandar duas vezes).</summary>
    Task<OcorrenciaRegistradaDto> RegistrarAsync(
        DispositivoVeiculo dispositivo, RegistrarOcorrenciaRequest request, CancellationToken ct = default);

    /// <summary>Em aberto (Aberta + EmAtendimento) ou, com <paramref name="encerradas"/>, as
    /// encerradas dos últimos 30 dias. Mais recentes primeiro.</summary>
    Task<IReadOnlyList<OcorrenciaVeiculoDto>> ListarAsync(bool encerradas, CancellationToken ct = default);

    Task<OcorrenciaVeiculoDto> AtenderAsync(Guid id, CancellationToken ct = default);
    Task<OcorrenciaVeiculoDto> EncerrarAsync(Guid id, EncerrarOcorrenciaRequest request, CancellationToken ct = default);
}

public sealed class OcorrenciaVeiculoService(
    SmsMaisDbContext db,
    IUsuarioAtualAccessor usuarioAtual,
    IRastreamentoNotificador notificador,
    ILogger<OcorrenciaVeiculoService> logger) : IOcorrenciaVeiculoService
{
    private const int TamanhoDescricao = 500;
    private const int TamanhoObservacao = 1000;
    private const int MaximoEncerradas = 200;
    private static readonly TimeSpan JanelaEncerradas = TimeSpan.FromDays(30);
    // Relógio do tablet adiantado não pode jogar a ocorrência para o futuro.
    private static readonly TimeSpan ToleranciaRelogio = TimeSpan.FromMinutes(5);

    public async Task<OcorrenciaRegistradaDto> RegistrarAsync(
        DispositivoVeiculo dispositivo, RegistrarOcorrenciaRequest request, CancellationToken ct = default)
    {
        if (request.IdLocal == Guid.Empty)
            throw new ValidacaoException("idLocal", "Identificador da ocorrência ausente.");
        if (!Enum.IsDefined(request.Tipo))
            throw new ValidacaoException("tipo", "Tipo de ocorrência inválido.");

        var descricao = Cortar(request.Descricao, TamanhoDescricao);
        if (request.Tipo == TipoOcorrenciaVeiculo.Outro && descricao is null)
            throw new ValidacaoException("descricao", "Descreva o que aconteceu.");

        var existente = await db.OcorrenciasVeiculo.AsNoTracking()
            .FirstOrDefaultAsync(o => o.IdLocal == request.IdLocal, ct);
        if (existente is not null) return new OcorrenciaRegistradaDto(existente.Id, existente.RecebidaEm);

        var agora = DateTime.UtcNow;
        var ocorridaEm = request.OcorridaEm == default
            ? agora
            : request.OcorridaEm.Kind == DateTimeKind.Utc
                ? request.OcorridaEm
                : DateTime.SpecifyKind(request.OcorridaEm, DateTimeKind.Utc);
        if (ocorridaEm > agora + ToleranciaRelogio) ocorridaEm = agora;

        // Coordenada fora do mundo (ou 0,0 de GPS sem fix) vira "sem posição", não derruba o REPORT.
        var temPosicao = request.Latitude is >= -90 and <= 90 && request.Longitude is >= -180 and <= 180
            && !(request.Latitude == 0 && request.Longitude == 0);

        // Rota desconhecida (translado ainda de exemplo no app) não impede o registro.
        var rotaId = request.RotaId is { } r && await db.Rotas.AnyAsync(x => x.Id == r, ct) ? r : (Guid?)null;

        var ocorrencia = new OcorrenciaVeiculo
        {
            Id = Guid.NewGuid(),
            VeiculoId = dispositivo.VeiculoId,
            DispositivoId = dispositivo.Id,
            IdLocal = request.IdLocal,
            Tipo = request.Tipo,
            Descricao = descricao,
            Latitude = temPosicao ? request.Latitude : null,
            Longitude = temPosicao ? request.Longitude : null,
            PrecisaoM = temPosicao && request.PrecisaoM is >= 0 ? request.PrecisaoM : null,
            RotaId = rotaId,
            OcorridaEm = ocorridaEm,
            RecebidaEm = agora,
            Situacao = SituacaoOcorrenciaVeiculo.Aberta,
            CriadoEm = agora,
        };
        db.OcorrenciasVeiculo.Add(ocorrencia);
        dispositivo.UltimoContatoEm = agora;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Dois envios simultâneos do mesmo REPORT: o índice único segurou, devolve o que entrou.
            var corrida = await db.OcorrenciasVeiculo.AsNoTracking()
                .FirstOrDefaultAsync(o => o.IdLocal == request.IdLocal, ct);
            if (corrida is null) throw;
            return new OcorrenciaRegistradaDto(corrida.Id, corrida.RecebidaEm);
        }

        var placa = await db.Veiculos.AsNoTracking()
            .Where(v => v.Id == dispositivo.VeiculoId).Select(v => v.Placa).FirstOrDefaultAsync(ct) ?? string.Empty;
        await NotificarAsync(() => notificador.OcorrenciaRegistradaAsync(
            ocorrencia.Id, ocorrencia.VeiculoId, placa, ocorrencia.Tipo, ct));

        return new OcorrenciaRegistradaDto(ocorrencia.Id, ocorrencia.RecebidaEm);
    }

    public async Task<IReadOnlyList<OcorrenciaVeiculoDto>> ListarAsync(bool encerradas, CancellationToken ct = default)
    {
        var consulta = db.OcorrenciasVeiculo.AsNoTracking();
        if (encerradas)
        {
            var desde = DateTime.UtcNow - JanelaEncerradas;
            consulta = consulta.Where(o => o.Situacao == SituacaoOcorrenciaVeiculo.Encerrada && o.RecebidaEm >= desde);
        }
        else
        {
            consulta = consulta.Where(o => o.Situacao != SituacaoOcorrenciaVeiculo.Encerrada);
        }

        var lista = await consulta.OrderByDescending(o => o.RecebidaEm).Take(MaximoEncerradas).ToListAsync(ct);
        return await MapearAsync(lista, ct);
    }

    public async Task<OcorrenciaVeiculoDto> AtenderAsync(Guid id, CancellationToken ct = default)
    {
        var o = await ObterParaAlterarAsync(id, ct);
        if (o.Situacao == SituacaoOcorrenciaVeiculo.Encerrada)
            throw new ConflitoException("ocorrencia_encerrada", "Esta ocorrência já foi encerrada.");

        if (o.Situacao == SituacaoOcorrenciaVeiculo.Aberta)
        {
            var agora = DateTime.UtcNow;
            o.Situacao = SituacaoOcorrenciaVeiculo.EmAtendimento;
            o.AtendidaEm = agora;
            o.AtendidaPor = usuarioAtual.UsuarioId;
            o.AtualizadoEm = agora;
            await db.SaveChangesAsync(ct);
            await NotificarAsync(() => notificador.OcorrenciaAtualizadaAsync(o.Id, ct));
        }

        return (await MapearAsync([o], ct))[0];
    }

    public async Task<OcorrenciaVeiculoDto> EncerrarAsync(
        Guid id, EncerrarOcorrenciaRequest request, CancellationToken ct = default)
    {
        var o = await ObterParaAlterarAsync(id, ct);
        if (o.Situacao == SituacaoOcorrenciaVeiculo.Encerrada)
            throw new ConflitoException("ocorrencia_encerrada", "Esta ocorrência já foi encerrada.");

        var agora = DateTime.UtcNow;
        o.Situacao = SituacaoOcorrenciaVeiculo.Encerrada;
        o.EncerradaEm = agora;
        o.EncerradaPor = usuarioAtual.UsuarioId;
        o.ObservacaoCentral = Cortar(request.Observacao, TamanhoObservacao);
        o.AtualizadoEm = agora;
        await db.SaveChangesAsync(ct);
        await NotificarAsync(() => notificador.OcorrenciaAtualizadaAsync(o.Id, ct));

        return (await MapearAsync([o], ct))[0];
    }

    private async Task<OcorrenciaVeiculo> ObterParaAlterarAsync(Guid id, CancellationToken ct) =>
        await db.OcorrenciasVeiculo.FirstOrDefaultAsync(o => o.Id == id, ct)
        ?? throw new NaoEncontradoException("Ocorrência", id);

    private async Task<IReadOnlyList<OcorrenciaVeiculoDto>> MapearAsync(
        IReadOnlyList<OcorrenciaVeiculo> lista, CancellationToken ct)
    {
        if (lista.Count == 0) return [];

        var veiculoIds = lista.Select(o => o.VeiculoId).Distinct().ToList();
        var veiculos = await db.Veiculos.AsNoTracking()
            .Where(v => veiculoIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, v => (v.Placa, v.Modelo), ct);

        var usuarioIds = lista.SelectMany(o => new[] { o.AtendidaPor, o.EncerradaPor })
            .OfType<Guid>().Distinct().ToList();
        var nomes = usuarioIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Usuarios.AsNoTracking()
                .Where(u => usuarioIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.NomeCompleto, ct);

        return lista.Select(o =>
        {
            var (placa, modelo) = veiculos.GetValueOrDefault(o.VeiculoId, (string.Empty, string.Empty));
            return new OcorrenciaVeiculoDto(
                o.Id, o.VeiculoId, placa, modelo, o.Tipo, o.Descricao,
                o.Latitude, o.Longitude, o.PrecisaoM, o.RotaId,
                o.OcorridaEm, o.RecebidaEm, o.Situacao,
                o.AtendidaEm, o.AtendidaPor is { } a ? nomes.GetValueOrDefault(a) : null,
                o.EncerradaEm, o.EncerradaPor is { } e ? nomes.GetValueOrDefault(e) : null,
                o.ObservacaoCentral);
        }).ToList();
    }

    /// <summary>Tempo real é aviso, não registro: a ocorrência já está salva e aparece na lista mesmo
    /// se o SignalR falhar.</summary>
    private async Task NotificarAsync(Func<Task> enviar)
    {
        try
        {
            await enviar();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Falha ao avisar o painel sobre ocorrência de veículo");
        }
    }

    private static string? Cortar(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length <= max ? s.Trim() : s.Trim()[..max]);
}
