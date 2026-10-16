using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Geo;
using SMSMais.Core.Pacientes;
using SMSMais.Core.Pacientes.Fhir;
using SMSMais.Core.Translado.Viagem;
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
/// <paramref name="Latitude"/>/<paramref name="Longitude"/> nulos = endereço sem ponto no mapa.
/// <paramref name="Concluida"/>: o tablet já resolveu a parada (todos marcados na coleta; no destino,
/// todos desembarcados ou ausentes). <paramref name="Trecho"/>: "Ida" (casa → unidade) ou "Volta"
/// (unidade → casa): fora do município a van espera e traz de volta quem levou; em Maricá, a volta é de
/// quem a central puxou para a rota.</summary>
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
    IReadOnlyList<PassageiroTabletDto> Passageiros,
    bool Concluida = false,
    string Trecho = RotaTabletService.Ida);

/// <summary>Paciente da parada, com os acompanhantes dele. <paramref name="Confirmacao"/>:
/// Confirmado, Aguardando ou Cancelado. <paramref name="Situacao"/>: a última marcação do tablet
/// (Embarcou, Ausente, Desembarcou) ou nula, ainda sem marcação.</summary>
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
    IReadOnlyList<AcompanhanteTabletDto> Acompanhantes,
    string? Situacao = null);

/// <summary><paramref name="Id"/> nulo quando a sessão não tem a lista de acompanhantes (nome antigo
/// ou só o assento reservado).</summary>
public sealed record AcompanhanteTabletDto(
    string Nome,
    ParentescoAcompanhante? Parentesco,
    string? Telefone,
    string Confirmacao,
    AssentoTabletDto? Assento,
    Guid? Id = null,
    string? Situacao = null);

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

    public const string Ida = "Ida";
    public const string Volta = "Volta";

    public const string Embarcou = "Embarcou";
    public const string Ausente = "Ausente";
    public const string Desembarcou = "Desembarcou";

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
                a.RotaDiariaId, a.Id, a.SessaoId, a.Tipo, a.Parada, a.OrdemParada, a.CriadoEm,
                new AssentoTabletDto(fileira.Ordem, assento.Numero, assento.Tipo),
                t.PacienteId, s.Status, s.HoraPrevistaBusca, s.HoraPrevistaRetorno, s.HoraChegadaUnidade,
                s.AcompanhanteEsperado,
                s.NomeAcompanhante,
                new CondicaoTabletDto(t.Mobilidade, t.DificuldadeVeiculoAlto, t.Isolamento, t.UsaOxigenio,
                    t.NecessitaAjuda, t.AjudaDescricao),
                tt == null ? null : tt.Nome, tt == null ? null : tt.TempoMedioMinutos,
                u.Id, u.Nome, u.Endereco, u.Gps == null ? null : (double?)u.Gps.Latitude,
                u.Gps == null ? null : (double?)u.Gps.Longitude, u.Observacoes, u.Externa)).ToListAsync(ct);

        var sessaoIds = alocacoes.Select(a => a.SessaoId).Distinct().ToList();
        var acompanhantes = (await (
                from sa in db.SessoesAcompanhantes.AsNoTracking()
                where sessaoIds.Contains(sa.SessaoId)
                join ac in db.Acompanhantes.AsNoTracking() on sa.AcompanhanteId equals ac.Id
                orderby ac.Nome
                select new { sa.SessaoId, ac.Id, ac.Nome, ac.Parentesco, ac.Telefone }).ToListAsync(ct))
            .ToLookup(x => x.SessaoId);

        var pacienteIds = alocacoes.Select(a => a.PacienteId).Distinct().ToList();
        var resumos = await ResumosAsync(pacienteIds, ct);
        var enderecos = new Dictionary<Guid, (string? Texto, string? Referencia, Coordenada? Ponto)>();
        var marcacoes = await ViagemService.UltimasMarcacoesAsync(db, rotaIds, ct);

        var resultado = new List<RotaTabletDto>();
        foreach (var rota in rotas)
        {
            var doPlano = PontosDoPlano(rota.PlanoRotaJson);
            var daRota = alocacoes.Where(a => a.RotaId == rota.Id).ToList();

            async Task<(string? Texto, string? Referencia, Coordenada? Ponto)> Casa(LinhaAlocacao p)
            {
                if (!enderecos.TryGetValue(p.PacienteId, out var end))
                {
                    end = await EnderecoAsync(p.PacienteId, ct);
                    enderecos[p.PacienteId] = end;
                }
                return (end.Texto, end.Referencia, doPlano.GetValueOrDefault(p.SessaoId) ?? end.Ponto);
            }

            PassageiroTabletDto Montar(SessaoNaRota s, bool volta) => Passageiro(
                s.Paciente, s.AssentosAcompanhante,
                acompanhantes[s.Paciente.SessaoId].Select(a => ((Guid?)a.Id, a.Nome, a.Parentesco, (string?)a.Telefone)).ToList(),
                resumos.GetValueOrDefault(s.Paciente.PacienteId),
                ordem => marcacoes.TryGetValue((rota.Id, s.Paciente.SessaoId, ordem, volta), out var t) ? NomeDaSituacao(t) : null);

            // Ida: uma coleta por sessão (paciente), na ordem da rota; sem ordem, pelo horário de busca.
            var paradas = new List<ParadaTabletDto>();
            var porUnidade = new List<(LinhaAlocacao Unidade, List<SessaoNaRota> Quem, List<PassageiroTabletDto> Passageiros)>();
            foreach (var s in Agrupar(daRota.Where(a => a.Parada != TipoParada.Retorno)))
            {
                var p = s.Paciente;
                var passageiro = Montar(s, volta: false);
                var casa = await Casa(p);
                paradas.Add(new ParadaTabletDto(
                    $"coleta:{p.SessaoId}", paradas.Count + 1, TipoParada.Coleta, passageiro.Nome, casa.Texto,
                    casa.Ponto?.Latitude, casa.Ponto?.Longitude, p.HoraPrevistaBusca, casa.Referencia, [passageiro],
                    Concluida: passageiro.Situacao is not null));

                var i = porUnidade.FindIndex(d => d.Unidade.UnidadeId == p.UnidadeId);
                if (i < 0)
                {
                    porUnidade.Add((p, [s], [passageiro]));
                }
                else
                {
                    porUnidade[i].Quem.Add(s);
                    porUnidade[i].Passageiros.Add(passageiro);
                }
            }

            // Depois das coletas, um destino por unidade de atendimento, na ordem em que apareceram.
            foreach (var (u, _, quem) in porUnidade)
            {
                paradas.Add(new ParadaTabletDto(
                    $"destino:{u.UnidadeId}", paradas.Count + 1, TipoParada.Destino, u.UnidadeNome,
                    Formatar(u.UnidadeEndereco), u.UnidadeLat, u.UnidadeLng, null, u.UnidadeObservacoes, quem,
                    Concluida: quem.All(x => x.Situacao is Ausente or Desembarcou)));
            }

            // Volta (decisão de 16/10: depende do destino). Fora do município a van espera e traz de volta
            // quem levou — menos quem faltou na ida. Em Maricá ela não espera: a volta é de quem a central
            // puxou para esta rota (alocação de retorno).
            // Espera = a van ficou na unidade (externa); sem espera = veio buscar quem foi puxado.
            var voltas = new List<(LinhaAlocacao Unidade, List<SessaoNaRota> Quem, bool Espera)>();
            foreach (var (u, quem, passageiros) in porUnidade.Where(x => x.Unidade.UnidadeExterna))
            {
                var vao = quem.Where((s, i) => passageiros[i].Situacao != Ausente
                    && s.Paciente.Status is not (StatusSessao.Cancelada or StatusSessao.NaoRealizada)).ToList();
                if (vao.Count > 0) voltas.Add((u, vao, true));
            }
            foreach (var s in Agrupar(daRota.Where(a => a.Parada == TipoParada.Retorno)))
            {
                var i = voltas.FindIndex(v => v.Unidade.UnidadeId == s.Paciente.UnidadeId);
                if (i < 0) voltas.Add((s.Paciente, [s], false));
                else voltas[i].Quem.Add(s);
            }

            foreach (var (u, quem, espera) in voltas)
            {
                var embarcam = quem.Select(s => Montar(s, volta: true)).ToList();
                var tempo = quem.Max(s => s.Paciente.TempoMedioMinutos);
                var aviso = espera
                    ? tempo is { } m ? $"A van espera o atendimento acabar (cerca de {Duracao(m)})." : "A van espera o atendimento acabar."
                    : "Buscar quem terminou o atendimento.";
                paradas.Add(new ParadaTabletDto(
                    $"retorno:{u.UnidadeId}", paradas.Count + 1, TipoParada.Coleta, $"Volta — {u.UnidadeNome}",
                    Formatar(u.UnidadeEndereco), u.UnidadeLat, u.UnidadeLng,
                    quem.Select(s => RetornoPrevisto(s.Paciente)).Max(), Juntar(aviso, u.UnidadeObservacoes), embarcam,
                    Concluida: embarcam.All(x => x.Situacao is not null), Trecho: Volta));

                // Entrega em casa na ordem inversa da coleta: o último buscado mora mais perto da unidade.
                for (var k = quem.Count - 1; k >= 0; k--)
                {
                    var casa = await Casa(quem[k].Paciente);
                    var passageiro = embarcam[k];
                    paradas.Add(new ParadaTabletDto(
                        $"casa:{quem[k].Paciente.SessaoId}", paradas.Count + 1, TipoParada.Destino, passageiro.Nome,
                        casa.Texto, casa.Ponto?.Latitude, casa.Ponto?.Longitude, null, casa.Referencia, [passageiro],
                        Concluida: passageiro.Situacao is Ausente or Desembarcou, Trecho: Volta));
                }
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
        List<(Guid? Id, string Nome, ParentescoAcompanhante? Parentesco, string? Telefone)> escolhidos,
        PacienteResumo? resumo,
        Func<int, string?> situacao)
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
        List<(Guid? Id, string Nome, ParentescoAcompanhante? Parentesco, string? Telefone)> nomes = escolhidos.Count > 0
            ? escolhidos
            : !string.IsNullOrWhiteSpace(p.NomeAcompanhante)
                ? [((Guid?)null, p.NomeAcompanhante!, (ParentescoAcompanhante?)null, (string?)null)]
                : [.. assentosAcompanhante.Select(_ => ((Guid?)null, "Acompanhante", (ParentescoAcompanhante?)null, (string?)null))];
        var acompanhantes = nomes.Select((a, i) => new AcompanhanteTabletDto(
            a.Nome, a.Parentesco, a.Telefone, confirmacaoAcomp,
            i < assentosAcompanhante.Count ? assentosAcompanhante[i] : null, a.Id, situacao(i + 1))).ToList();

        var nome = resumo?.Nome;
        return new PassageiroTabletDto(
            p.SessaoId, string.IsNullOrWhiteSpace(nome) ? "Paciente" : nome,
            resumo?.Celular, confirmacaoPaciente, p.Assento, p.Condicao, p.TipoTratamento,
            p.TempoMedioMinutos, p.UnidadeNome, acompanhantes, situacao(0));
    }

    /// <summary>Sessões da rota (uma por paciente, com os assentos dos acompanhantes), na ordem da rota;
    /// sem ordem, pelo horário de busca.</summary>
    private static List<SessaoNaRota> Agrupar(IEnumerable<LinhaAlocacao> linhas) => [.. linhas
        .GroupBy(a => a.SessaoId)
        .Select(g => new SessaoNaRota(
            g.FirstOrDefault(a => a.Tipo == TipoAlocacao.Paciente) ?? g.First(),
            [.. g.Where(a => a.Tipo == TipoAlocacao.Acompanhante)
                .OrderBy(a => a.Assento.Fileira).ThenBy(a => a.Assento.Numero)
                .Select(a => a.Assento)]))
        .OrderBy(x => x.Paciente.OrdemParada ?? int.MaxValue)
        .ThenBy(x => x.Paciente.HoraPrevistaBusca ?? TimeOnly.MaxValue)
        .ThenBy(x => x.Paciente.CriadoEm)];

    /// <summary>Quando o paciente deve estar pronto para voltar: o que a central marcou na sessão; sem
    /// isso, a chegada à unidade mais o tempo médio do tratamento.</summary>
    internal static TimeOnly? RetornoPrevisto(TimeOnly? marcado, TimeOnly? chegada, int? tempoMedioMinutos) =>
        marcado ?? (chegada is { } c && tempoMedioMinutos is { } m ? c.AddMinutes(m) : null);

    private static TimeOnly? RetornoPrevisto(LinhaAlocacao p) =>
        RetornoPrevisto(p.HoraPrevistaRetorno, p.HoraChegadaUnidade, p.TempoMedioMinutos);

    private static string Duracao(int minutos) =>
        minutos < 60 ? $"{minutos} min" : minutos % 60 == 0 ? $"{minutos / 60} h" : $"{minutos / 60} h {minutos % 60:00}";

    private static string Juntar(string a, string? b) => string.IsNullOrWhiteSpace(b) ? a : $"{a} · {b.Trim()}";

    private static string NomeDaSituacao(TipoEventoViagem t) => t switch
    {
        TipoEventoViagem.Embarque => Embarcou,
        TipoEventoViagem.Ausencia => Ausente,
        _ => Desembarcou,
    };

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

    private sealed record SessaoNaRota(LinhaAlocacao Paciente, List<AssentoTabletDto> AssentosAcompanhante);

    private sealed record LinhaAlocacao(
        Guid RotaId,
        Guid AlocacaoId,
        Guid SessaoId,
        TipoAlocacao Tipo,
        TipoParada Parada,
        int? OrdemParada,
        DateTime CriadoEm,
        AssentoTabletDto Assento,
        Guid PacienteId,
        StatusSessao Status,
        TimeOnly? HoraPrevistaBusca,
        TimeOnly? HoraPrevistaRetorno,
        TimeOnly? HoraChegadaUnidade,
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
        string? UnidadeObservacoes,
        bool UnidadeExterna);
}
