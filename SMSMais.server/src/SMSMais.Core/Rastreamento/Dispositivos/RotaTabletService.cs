using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Geo;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Data;
using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Rastreamento.Dispositivos;

// Rota do dia para o tablet do veículo (pelo token do veículo, como o resto do tablet). Junta o que
// o painel cadastrou em Translados, Atendimentos, Unidades de Atendimento e Tipos de tratamento no
// formato da navegação: paradas na ordem, com ponto no mapa, passageiros, assentos e condição.

/// <summary>Uma rota do veículo no dia, pronta para a navegação do tablet.</summary>
public sealed record RotaTabletDto(
    Guid RotaId,
    DateOnly Data,
    StatusRota Status,
    Guid MotoristaId,
    string MotoristaNome,
    DateTime? IniciadaEm,
    int? DistanciaMetros,
    int? DuracaoSegundos,
    IReadOnlyList<ParadaTabletDto> Paradas);

/// <summary>Coleta (uma por paciente, na casa dele) ou destino (uma por unidade de atendimento).
/// <paramref name="Latitude"/>/<paramref name="Longitude"/> nulos = endereço sem ponto no mapa.</summary>
public sealed record ParadaTabletDto(
    string Id,
    int Ordem,
    TipoParada Tipo,
    string Rotulo,
    string? Endereco,
    double? Latitude,
    double? Longitude,
    TimeOnly? HorarioPrevisto,
    string? Observacoes,
    IReadOnlyList<PassageiroTabletDto> Passageiros);

/// <summary>Paciente da parada, com os acompanhantes dele. <paramref name="Confirmacao"/>:
/// Confirmado, Aguardando ou Cancelado.</summary>
public sealed record PassageiroTabletDto(
    Guid SessaoId,
    string Nome,
    string? Telefone,
    string Confirmacao,
    AssentoTabletDto? Assento,
    CondicaoTabletDto Condicao,
    string? TipoTratamento,
    int? TempoMedioMinutos,
    string? UnidadeAtendimento,
    IReadOnlyList<AcompanhanteTabletDto> Acompanhantes);

public sealed record AcompanhanteTabletDto(
    string Nome,
    ParentescoAcompanhante? Parentesco,
    string? Telefone,
    string Confirmacao,
    AssentoTabletDto? Assento);

public sealed record AssentoTabletDto(int Fileira, int Numero, TipoAssento Tipo);

/// <summary>Condição do paciente para o transporte (cadastro do Atendimento).</summary>
public sealed record CondicaoTabletDto(
    MobilidadeTransporte Mobilidade,
    bool DificuldadeVeiculoAlto,
    bool Isolamento,
    bool UsaOxigenio,
    bool NecessitaAjuda,
    string? AjudaDescricao);

public interface IRotaTabletService
{
    /// <summary>Rotas de hoje do veículo do tablet (e a de ontem ainda em andamento), mais cedo
    /// primeiro. Paciente sem cadastro no hub ou sem ponto no mapa não derruba a rota.</summary>
    Task<IReadOnlyList<RotaTabletDto>> RotasDoDiaAsync(DispositivoVeiculo dispositivo, CancellationToken ct = default);
}

public sealed class RotaTabletService(
    SmsMaisDbContext db,
    IPacienteResolver pacienteResolver,
    IPacientesService pacientes,
    IGeocodificadorService geo,
    ILogger<RotaTabletService> logger) : IRotaTabletService
{
    public const string Confirmado = "Confirmado";
    public const string Aguardando = "Aguardando";
    public const string Cancelado = "Cancelado";

    public async Task<IReadOnlyList<RotaTabletDto>> RotasDoDiaAsync(
        DispositivoVeiculo dispositivo, CancellationToken ct = default)
    {
        var rotas = await (
            from r in RotasDoVeiculo.DoDia(db, dispositivo.VeiculoId).AsNoTracking()
            join m in db.Motoristas.AsNoTracking() on r.MotoristaId equals m.Id
            orderby r.Data, r.CriadoEm
            select new
            {
                r.Id, r.Data, r.Status, r.MotoristaId, Motorista = m.Usuario.NomeCompleto, r.IniciadaEm,
                r.DistanciaTotalMetros, r.DuracaoEstimadaSegundos, r.PlanoRotaJson,
            }).ToListAsync(ct);
        if (rotas.Count == 0) return [];

        var rotaIds = rotas.Select(r => r.Id).ToList();
        var alocacoes = await (
            from a in db.Alocacoes.AsNoTracking()
            where rotaIds.Contains(a.RotaDiariaId)
            join assento in db.Assentos.AsNoTracking() on a.AssentoId equals assento.Id
            join fileira in db.Fileiras.AsNoTracking() on assento.FileiraId equals fileira.Id
            join s in db.Sessoes.AsNoTracking() on a.SessaoId equals s.Id
            join t in db.Tratamentos.AsNoTracking() on s.TratamentoId equals t.Id
            join u in db.UnidadesAtendimento.AsNoTracking() on t.UnidadeAtendimentoId equals u.Id
            join tt in db.TiposTratamento.AsNoTracking() on t.TipoTratamentoId equals (Guid?)tt.Id into tts
            from tt in tts.DefaultIfEmpty()
            select new LinhaAlocacao(
                a.RotaDiariaId, a.Id, a.SessaoId, a.Tipo, a.OrdemParada, a.CriadoEm,
                new AssentoTabletDto(fileira.Ordem, assento.Numero, assento.Tipo),
                t.PacienteId, s.Status, s.HoraPrevistaBusca, s.AcompanhanteEsperado,
                s.NomeAcompanhante,
                new CondicaoTabletDto(t.Mobilidade, t.DificuldadeVeiculoAlto, t.Isolamento, t.UsaOxigenio,
                    t.NecessitaAjuda, t.AjudaDescricao),
                tt == null ? null : tt.Nome, tt == null ? null : tt.TempoMedioMinutos,
                u.Id, u.Nome, u.Endereco, u.Gps == null ? null : (double?)u.Gps.Latitude,
                u.Gps == null ? null : (double?)u.Gps.Longitude, u.Observacoes)).ToListAsync(ct);

        var sessaoIds = alocacoes.Select(a => a.SessaoId).Distinct().ToList();
        var acompanhantes = (await (
                from sa in db.SessoesAcompanhantes.AsNoTracking()
                where sessaoIds.Contains(sa.SessaoId)
                join ac in db.Acompanhantes.AsNoTracking() on sa.AcompanhanteId equals ac.Id
                orderby ac.Nome
                select new { sa.SessaoId, ac.Nome, ac.Parentesco, ac.Telefone }).ToListAsync(ct))
            .ToLookup(x => x.SessaoId);

        var pacienteIds = alocacoes.Select(a => a.PacienteId).Distinct().ToList();
        var resumos = await ResumosAsync(pacienteIds, ct);
        var enderecos = new Dictionary<Guid, (string? Texto, string? Referencia, Coordenada? Ponto)>();

        var resultado = new List<RotaTabletDto>();
        foreach (var rota in rotas)
        {
            var doPlano = PontosDoPlano(rota.PlanoRotaJson);
            var daRota = alocacoes.Where(a => a.RotaId == rota.Id).ToList();

            // Uma coleta por sessão (paciente), na ordem da rota; sem ordem, pelo horário de busca.
            var sessoes = daRota.GroupBy(a => a.SessaoId)
                .Select(g => new
                {
                    Paciente = g.FirstOrDefault(a => a.Tipo == TipoAlocacao.Paciente) ?? g.First(),
                    AssentosAcompanhante = g.Where(a => a.Tipo == TipoAlocacao.Acompanhante)
                        .OrderBy(a => a.Assento.Fileira).ThenBy(a => a.Assento.Numero)
                        .Select(a => a.Assento).ToList(),
                })
                .OrderBy(x => x.Paciente.OrdemParada ?? int.MaxValue)
                .ThenBy(x => x.Paciente.HoraPrevistaBusca ?? TimeOnly.MaxValue)
                .ThenBy(x => x.Paciente.CriadoEm)
                .ToList();

            var paradas = new List<ParadaTabletDto>();
            var porUnidade = new List<(LinhaAlocacao Linha, List<PassageiroTabletDto> Passageiros)>();
            foreach (var s in sessoes)
            {
                var p = s.Paciente;
                var passageiro = Passageiro(p, s.AssentosAcompanhante, acompanhantes[p.SessaoId].Select(
                    a => (a.Nome, a.Parentesco, (string?)a.Telefone)).ToList(), resumos.GetValueOrDefault(p.PacienteId));

                if (!enderecos.TryGetValue(p.PacienteId, out var end))
                {
                    end = await EnderecoAsync(p.PacienteId, ct);
                    enderecos[p.PacienteId] = end;
                }
                var ponto = doPlano.GetValueOrDefault(p.SessaoId) ?? end.Ponto;
                paradas.Add(new ParadaTabletDto(
                    $"coleta:{p.SessaoId}", paradas.Count + 1, TipoParada.Coleta, passageiro.Nome, end.Texto,
                    ponto?.Latitude, ponto?.Longitude, p.HoraPrevistaBusca, end.Referencia, [passageiro]));

                var destino = porUnidade.FindIndex(d => d.Linha.UnidadeId == p.UnidadeId);
                if (destino < 0) porUnidade.Add((p, [passageiro]));
                else porUnidade[destino].Passageiros.Add(passageiro);
            }

            // Depois das coletas, um destino por unidade de atendimento, na ordem em que apareceram.
            foreach (var (u, quem) in porUnidade)
            {
                paradas.Add(new ParadaTabletDto(
                    $"destino:{u.UnidadeId}", paradas.Count + 1, TipoParada.Destino, u.UnidadeNome,
                    Formatar(u.UnidadeEndereco), u.UnidadeLat, u.UnidadeLng, null, u.UnidadeObservacoes, quem));
            }

            resultado.Add(new RotaTabletDto(
                rota.Id, rota.Data, rota.Status, rota.MotoristaId, rota.Motorista, rota.IniciadaEm,
                rota.DistanciaTotalMetros, rota.DuracaoEstimadaSegundos, paradas));
        }
        return resultado;
    }

    private static PassageiroTabletDto Passageiro(
        LinhaAlocacao p,
        List<AssentoTabletDto> assentosAcompanhante,
        List<(string Nome, ParentescoAcompanhante? Parentesco, string? Telefone)> escolhidos,
        PacienteResumo? resumo)
    {
        var confirmacaoPaciente = p.Status switch
        {
            StatusSessao.Cancelada => Cancelado,
            StatusSessao.Confirmada => Confirmado,
            _ => Aguardando,
        };
        var confirmacaoAcomp = p.AcompanhanteEsperado switch
        {
            true => Confirmado,
            false => Cancelado,
            null => Aguardando,
        };

        // Quem vai: os escolhidos da sessão; sem lista, o nome antigo da sessão; com assento reservado
        // e sem nome nenhum, "Acompanhante". Cada um pega um assento de acompanhante, na ordem.
        List<(string Nome, ParentescoAcompanhante? Parentesco, string? Telefone)> nomes = escolhidos.Count > 0
            ? escolhidos
            : !string.IsNullOrWhiteSpace(p.NomeAcompanhante)
                ? [(p.NomeAcompanhante!, (ParentescoAcompanhante?)null, (string?)null)]
                : [.. assentosAcompanhante.Select(_ => ("Acompanhante", (ParentescoAcompanhante?)null, (string?)null))];
        var acompanhantes = nomes.Select((a, i) => new AcompanhanteTabletDto(
            a.Nome, a.Parentesco, a.Telefone, confirmacaoAcomp,
            i < assentosAcompanhante.Count ? assentosAcompanhante[i] : null)).ToList();

        var nome = resumo?.Nome;
        return new PassageiroTabletDto(
            p.SessaoId, string.IsNullOrWhiteSpace(nome) ? "Paciente" : nome,
            resumo?.Celular, confirmacaoPaciente, p.Assento, p.Condicao, p.TipoTratamento,
            p.TempoMedioMinutos, p.UnidadeNome, acompanhantes);
    }

    private async Task<IReadOnlyDictionary<Guid, PacienteResumo>> ResumosAsync(List<Guid> ids, CancellationToken ct)
    {
        try
        {
            return await pacienteResolver.ResolverManyAsync(ids, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rota do tablet: hub FHIR indisponível; pacientes sem nome.");
            return new Dictionary<Guid, PacienteResumo>();
        }
    }

    /// <summary>Endereço do paciente em texto e o ponto no mapa (cache do geocodificador).</summary>
    private async Task<(string? Texto, string? Referencia, Coordenada? Ponto)> EnderecoAsync(
        Guid pacienteId, CancellationToken ct)
    {
        try
        {
            var dto = await pacientes.ObterPorIdAsync(pacienteId, ct);
            if (dto.Endereco is null) return (null, null, null);
            var entidade = dto.Endereco.ParaEntidade();
            return (Formatar(entidade), entidade.PontoReferencia, await geo.GeocodificarAsync(entidade, ct));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rota do tablet: endereço do paciente {Id} não resolvido.", pacienteId);
            return (null, null, null);
        }
    }

    /// <summary>"Rua X, 12 — Bairro, Cidade" (o que o motorista lê na parada).</summary>
    internal static string? Formatar(Endereco? e)
    {
        if (e is null || string.IsNullOrWhiteSpace(e.Logradouro)) return null;
        var rua = string.IsNullOrWhiteSpace(e.Numero) ? e.Logradouro.Trim() : $"{e.Logradouro.Trim()}, {e.Numero.Trim()}";
        var lugar = string.Join(", ", new[] { e.Bairro, e.Cidade }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()));
        return lugar.Length == 0 ? rua : $"{rua} — {lugar}";
    }

    /// <summary>Coordenadas de coleta gravadas pelo gerador (<c>PlanoRotaJson.paradas</c>), por sessão.
    /// Rota montada à mão não tem plano: o ponto vem do endereço do paciente.</summary>
    internal static Dictionary<Guid, Coordenada> PontosDoPlano(string? json)
    {
        var pontos = new Dictionary<Guid, Coordenada>();
        if (string.IsNullOrWhiteSpace(json)) return pontos;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("paradas", out var paradas) || paradas.ValueKind != JsonValueKind.Array)
                return pontos;
            foreach (var p in paradas.EnumerateArray())
            {
                if (p.TryGetProperty("sessaoId", out var s) && s.TryGetGuid(out var sessaoId)
                    && p.TryGetProperty("lat", out var lat) && lat.TryGetDouble(out var la)
                    && p.TryGetProperty("lng", out var lng) && lng.TryGetDouble(out var lo))
                {
                    pontos[sessaoId] = new Coordenada(la, lo);
                }
            }
        }
        catch (JsonException)
        {
        }
        return pontos;
    }

    private sealed record LinhaAlocacao(
        Guid RotaId,
        Guid AlocacaoId,
        Guid SessaoId,
        TipoAlocacao Tipo,
        int? OrdemParada,
        DateTime CriadoEm,
        AssentoTabletDto Assento,
        Guid PacienteId,
        StatusSessao Status,
        TimeOnly? HoraPrevistaBusca,
        bool? AcompanhanteEsperado,
        string? NomeAcompanhante,
        CondicaoTabletDto Condicao,
        string? TipoTratamento,
        int? TempoMedioMinutos,
        Guid UnidadeId,
        string UnidadeNome,
        Endereco? UnidadeEndereco,
        double? UnidadeLat,
        double? UnidadeLng,
        string? UnidadeObservacoes);
}
