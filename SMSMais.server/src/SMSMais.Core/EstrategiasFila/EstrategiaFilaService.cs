using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.EstrategiasFila.Dtos;
using SMSMais.Core.Identidade;
using SMSMais.Core.RoboAtendimento.Runtime;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EstrategiasFila;

namespace SMSMais.Core.EstrategiasFila;

/// <summary>
/// Implementação do ciclo: simular → salvar → rerodar → comparar → marcar aplicada.
///
/// <para><b>Rodada é append-only.</b> Editar a estratégia muda só a cabeça (nome, parâmetros
/// vigentes, estado). O que já foi visto fica nas rodadas, e a "rodada atual" é a última que
/// produziu projeção — uma rodada do agente que falhou fica registrada (com o custo), mas não vira
/// a atual.</para>
///
/// <para>Os JSONs gravados são os DTOs deste módulo serializados com as opções web (camelCase); é o
/// mesmo formato que a API entrega, então o front lê rodada antiga e nova do mesmo jeito.</para>
/// </summary>
public sealed class EstrategiaFilaService(
    SmsMaisDbContext db,
    ICenarioFilaService cenarios,
    IEstrategiaAgenteIa agente,
    IUsuarioAtualAccessor usuarioAtual) : IEstrategiaFilaService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ------------------------------------------------------------------ simular

    public async Task<SimularRespostaDto> SimularAsync(SimularRequest request, CancellationToken ct = default)
    {
        var cenario = await cenarios.MontarAsync(request.ProcedimentoCodigo, request.ProcedimentoNome, ct);
        var parametros = Normalizar(request.Parametros ?? cenario.ParametrosIniciais);
        return new SimularRespostaDto(cenario, SimuladorFila.Projetar(parametros, cenario.Fila.Total));
    }

    public ProjecaoDto Projetar(ProjetarRequest request) =>
        SimuladorFila.Projetar(Normalizar(request.Parametros), request.FilaInicial);

    // ------------------------------------------------------------------ leitura

    public async Task<IReadOnlyList<EstrategiaResumoDto>> ListarAsync(EstrategiaFiltro filtro, CancellationToken ct = default)
    {
        var q = db.EstrategiasFila.AsNoTracking()
            .Include(e => e.RegulacaoProcedimento)
            .Where(e => e.ExcluidoEm == null);

        if (filtro.Status is { } status) q = q.Where(e => e.Status == status);
        else if (!filtro.IncluirArquivadas) q = q.Where(e => e.Status != StatusEstrategiaFila.Arquivada);

        if (!string.IsNullOrWhiteSpace(filtro.ProcedimentoCodigo))
            q = q.Where(e => e.ProcedimentoCodigo == filtro.ProcedimentoCodigo);
        if (!string.IsNullOrWhiteSpace(filtro.ProcedimentoNome))
            q = q.Where(e => e.ProcedimentoNome == filtro.ProcedimentoNome);

        var lista = await q.OrderByDescending(e => e.AtualizadoEm ?? e.CriadoEm).ToListAsync(ct);
        if (lista.Count == 0) return [];

        var ids = lista.Select(e => e.Id).ToList();
        var rodadas = await db.EstrategiaFilaRodadas.AsNoTracking()
            .Where(r => ids.Contains(r.EstrategiaId))
            .Select(r => new
            {
                r.Id, r.EstrategiaId, r.Numero, r.Modo, r.ProjecaoJson, r.Falha, r.Modelo, r.CustoUsd, r.DuracaoMs, r.CriadoEm, r.CriadoPor,
            })
            .ToListAsync(ct);
        var porEstrategia = rodadas.ToLookup(r => r.EstrategiaId);

        return [.. lista.Select(e =>
        {
            var atual = e.RodadaAtualId is { } ra ? porEstrategia[e.Id].FirstOrDefault(r => r.Id == ra) : null;
            return new EstrategiaResumoDto(
                e.Id, e.Nome, e.ProcedimentoCodigo, e.ProcedimentoNome, e.RegulacaoProcedimento?.NomeCanonico,
                e.Status, porEstrategia[e.Id].Count(),
                atual is null ? null : ResumoRodada(atual.Id, atual.Numero, atual.Modo, atual.ProjecaoJson, atual.Falha,
                    atual.Modelo, atual.CustoUsd, atual.DuracaoMs, atual.CriadoEm, atual.CriadoPor),
                e.AplicadaEm, e.CriadoEm, e.AtualizadoEm);
        })];
    }

    public async Task<EstrategiaDto> ObterAsync(Guid id, CancellationToken ct = default)
    {
        var e = await CarregarAsync(id, rastrear: false, ct);
        var rodadas = await db.EstrategiaFilaRodadas.AsNoTracking()
            .Where(r => r.EstrategiaId == id)
            .OrderBy(r => r.Numero)
            .ToListAsync(ct);

        var atual = rodadas.FirstOrDefault(r => r.Id == e.RodadaAtualId);

        return new EstrategiaDto(
            e.Id, e.Nome, e.ProcedimentoCodigo, e.ProcedimentoNome, e.RegulacaoProcedimento?.NomeCanonico,
            e.Status,
            (Ler<ParametrosEstrategia>(e.ParametrosJson) ?? throw new ConflitoException("estrategia.parametros_invalidos", "Os parâmetros gravados não puderam ser lidos.")).Sanear(),
            atual is null ? null : ParaDto(atual),
            [.. rodadas.Select(r => ResumoRodada(r.Id, r.Numero, r.Modo, r.ProjecaoJson, r.Falha, r.Modelo, r.CustoUsd, r.DuracaoMs, r.CriadoEm, r.CriadoPor))],
            e.AplicadaEm, e.AplicadaPor, e.AplicacaoNota, e.CriadoEm, e.CriadoPor, e.AtualizadoEm);
    }

    public async Task<RodadaDto> ObterRodadaAsync(Guid id, int numero, CancellationToken ct = default)
    {
        _ = await CarregarAsync(id, rastrear: false, ct);
        var r = await db.EstrategiaFilaRodadas.AsNoTracking()
            .FirstOrDefaultAsync(x => x.EstrategiaId == id && x.Numero == numero, ct)
            ?? throw new NaoEncontradoException("EstrategiaFilaRodada", $"{id}/{numero}");
        return ParaDto(r);
    }

    // ------------------------------------------------------------------ escrita

    public async Task<Guid> CriarAsync(CriarEstrategiaRequest request, CancellationToken ct = default)
    {
        var nome = request.Nome.Trim();
        var codigo = string.IsNullOrWhiteSpace(request.ProcedimentoCodigo) ? null : request.ProcedimentoCodigo.Trim();
        var procedimentoNome = request.ProcedimentoNome.Trim();

        var cenario = await cenarios.MontarAsync(codigo, procedimentoNome, ct);
        var parametros = Normalizar(request.Parametros ?? cenario.ParametrosIniciais);
        var agora = DateTime.UtcNow;
        var quem = usuarioAtual.UsuarioId;

        var e = new EstrategiaFila
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            ProcedimentoCodigo = codigo,
            ProcedimentoNome = procedimentoNome,
            RegulacaoProcedimentoId = cenario.Procedimento.RegulacaoProcedimentoId,
            Status = StatusEstrategiaFila.Rascunho,
            ParametrosJson = Gravar(parametros),
            CriadoEm = agora,
            CriadoPor = quem,
        };
        db.EstrategiasFila.Add(e);

        var rodada = RodadaManual(e, 1, cenario, parametros, agora, quem);
        db.EstrategiaFilaRodadas.Add(rodada);
        e.RodadaAtualId = rodada.Id;

        await db.SaveChangesAsync(ct);
        return e.Id;
    }

    public async Task AtualizarAsync(Guid id, AtualizarEstrategiaRequest request, CancellationToken ct = default)
    {
        var e = await CarregarAsync(id, rastrear: true, ct);
        GarantirEditavel(e);

        e.Nome = request.Nome.Trim();
        e.ParametrosJson = Gravar(Normalizar(request.Parametros));
        if (request.Status is { } status)
        {
            if (status is StatusEstrategiaFila.Aplicada)
                throw new ValidacaoException("status", "Use 'marcar como aplicada' para registrar a aplicação.");
            e.Status = status;
        }
        Carimbar(e);
        await db.SaveChangesAsync(ct);
    }

    public async Task<RodadaDto> RodarAsync(Guid id, NovaRodadaRequest request, CancellationToken ct = default)
    {
        var e = await CarregarAsync(id, rastrear: true, ct);
        GarantirEditavel(e);

        var parametros = Normalizar(request.Parametros ?? Ler<ParametrosEstrategia>(e.ParametrosJson)!.Sanear());
        var cenario = await cenarios.MontarAsync(e.ProcedimentoCodigo, e.ProcedimentoNome, ct);
        var numero = await db.EstrategiaFilaRodadas.Where(r => r.EstrategiaId == id).MaxAsync(r => (int?)r.Numero, ct) ?? 0;
        numero++;
        var agora = DateTime.UtcNow;
        var quem = usuarioAtual.UsuarioId;

        EstrategiaFilaRodada rodada;
        if (request.Modo == ModoRodadaEstrategia.Agente)
        {
            var resultado = await agente.PlanejarAsync(cenario, parametros, ct);
            var finais = resultado.ParametrosFinais ?? parametros;
            var projecao = resultado.Projecao ?? SimuladorFila.Projetar(parametros, cenario.Fila.Total);

            rodada = new EstrategiaFilaRodada
            {
                Id = Guid.CreateVersion7(),
                EstrategiaId = e.Id,
                Numero = numero,
                Modo = ModoRodadaEstrategia.Agente,
                ParametrosEntradaJson = Gravar(parametros),
                CenarioJson = Gravar(cenario),
                ParametrosResultadoJson = Gravar(finais),
                ProjecaoJson = Gravar(projecao),
                PropostaJson = resultado.Proposta is null ? null : Gravar(resultado.Proposta),
                Modelo = resultado.Modelo,
                TokensEntrada = resultado.TokensEntrada,
                TokensSaida = resultado.TokensSaida,
                CustoUsd = PrecoModeloIa.Calcular(resultado.Modelo, resultado.TokensEntrada, resultado.TokensSaida),
                DuracaoMs = resultado.DuracaoMs,
                Falha = resultado.Falha,
                CriadoEm = agora,
                CriadoPor = quem,
            };
            db.EstrategiaFilaRodadas.Add(rodada);

            if (resultado.Falha is null)
            {
                // A proposta preenche os parâmetros vigentes: o operador vê os livres já ajustados e
                // pode travar/liberar e rodar de novo.
                e.ParametrosJson = Gravar(finais);
                e.RodadaAtualId = rodada.Id;
            }
        }
        else
        {
            rodada = RodadaManual(e, numero, cenario, parametros, agora, quem);
            db.EstrategiaFilaRodadas.Add(rodada);
            e.ParametrosJson = Gravar(parametros);
            e.RodadaAtualId = rodada.Id;
        }

        Carimbar(e);
        await db.SaveChangesAsync(ct);
        return ParaDto(rodada);
    }

    public async Task MarcarAplicadaAsync(Guid id, MarcarAplicadaRequest request, CancellationToken ct = default)
    {
        var e = await CarregarAsync(id, rastrear: true, ct);
        if (e.Status == StatusEstrategiaFila.Arquivada)
            throw new ConflitoException("estrategia.arquivada", "Estratégia arquivada não pode ser marcada como aplicada.");

        e.Status = StatusEstrategiaFila.Aplicada;
        e.AplicadaEm = DateTime.UtcNow;
        e.AplicadaPor = usuarioAtual.UsuarioId;
        e.AplicacaoNota = string.IsNullOrWhiteSpace(request.Nota) ? null : request.Nota.Trim();
        Carimbar(e);
        await db.SaveChangesAsync(ct);
    }

    public async Task ArquivarAsync(Guid id, CancellationToken ct = default)
    {
        var e = await CarregarAsync(id, rastrear: true, ct);
        e.Status = StatusEstrategiaFila.Arquivada;
        Carimbar(e);
        await db.SaveChangesAsync(ct);
    }

    public async Task ExcluirAsync(Guid id, CancellationToken ct = default)
    {
        var e = await CarregarAsync(id, rastrear: true, ct);
        e.ExcluidoEm = DateTime.UtcNow;
        e.ExcluidoPor = usuarioAtual.UsuarioId;
        await db.SaveChangesAsync(ct);
    }

    // ------------------------------------------------------------------ apoio

    private async Task<EstrategiaFila> CarregarAsync(Guid id, bool rastrear, CancellationToken ct)
    {
        IQueryable<EstrategiaFila> q = db.EstrategiasFila.Include(e => e.RegulacaoProcedimento);
        if (!rastrear) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(e => e.Id == id && e.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException(nameof(EstrategiaFila), id);
    }

    private static void GarantirEditavel(EstrategiaFila e)
    {
        if (e.Status == StatusEstrategiaFila.Arquivada)
            throw new ConflitoException("estrategia.arquivada", "Estratégia arquivada não pode ser alterada. Reabra-a primeiro.");
    }

    private void Carimbar(EstrategiaFila e)
    {
        e.AtualizadoEm = DateTime.UtcNow;
        e.AtualizadoPor = usuarioAtual.UsuarioId;
    }

    private static EstrategiaFilaRodada RodadaManual(
        EstrategiaFila e, int numero, CenarioFilaDto cenario, ParametrosEstrategia parametros, DateTime agora, Guid? quem)
    {
        var projecao = SimuladorFila.Projetar(parametros, cenario.Fila.Total);
        return new EstrategiaFilaRodada
        {
            Id = Guid.CreateVersion7(),
            EstrategiaId = e.Id,
            Numero = numero,
            Modo = ModoRodadaEstrategia.Manual,
            ParametrosEntradaJson = Gravar(parametros),
            CenarioJson = Gravar(cenario),
            ParametrosResultadoJson = Gravar(parametros),
            ProjecaoJson = Gravar(projecao),
            CriadoEm = agora,
            CriadoPor = quem,
        };
    }

    /// <summary>Objetivo fechado num conjunto conhecido; horizonte dentro do teto; listas nunca nulas.</summary>
    private static ParametrosEstrategia Normalizar(ParametrosEstrategia p)
    {
        p = p.Sanear();
        var objetivo = ObjetivoEstrategia.Todos.Contains(p.Objetivo ?? string.Empty)
            ? p.Objetivo! : ObjetivoEstrategia.ZerarEmSemanas;
        var horizonte = p.HorizonteSemanas <= 0
            ? ParametrosEstrategia.HorizontePadrao
            : Math.Min(p.HorizonteSemanas, ParametrosEstrategia.HorizonteMaximo);
        return p with
        {
            Objetivo = objetivo,
            HorizonteSemanas = horizonte,
            Mutiroes = p.Mutiroes ?? [],
            PrazoAlvoSemanas = p.PrazoAlvoSemanas is { } pz && pz > 0 ? Math.Min(pz, horizonte) : null,
        };
    }

    private static RodadaDto ParaDto(EstrategiaFilaRodada r) => new(
        r.Id, r.EstrategiaId, r.Numero, r.Modo,
        Ler<ParametrosEstrategia>(r.ParametrosEntradaJson)!.Sanear(),
        Ler<CenarioFilaDto>(r.CenarioJson)!,
        Ler<ParametrosEstrategia>(r.ParametrosResultadoJson)!.Sanear(),
        Ler<ProjecaoDto>(r.ProjecaoJson)!,
        r.PropostaJson is null ? null : Ler<PropostaAgenteDto>(r.PropostaJson),
        r.Modelo, r.TokensEntrada, r.TokensSaida, r.CustoUsd, r.DuracaoMs, r.Falha, r.CriadoEm, r.CriadoPor);

    private static RodadaResumoDto ResumoRodada(
        Guid id, int numero, ModoRodadaEstrategia modo, string projecaoJson, string? falha,
        string? modelo, decimal? custo, int duracaoMs, DateTime criadoEm, Guid? criadoPor)
    {
        var p = Ler<ProjecaoDto>(projecaoJson);
        return new RodadaResumoDto(id, numero, modo, p?.Zera, p?.SemanaZera, p?.CapacidadeSemanal ?? 0,
            falha, modelo, custo, duracaoMs, criadoEm, criadoPor);
    }

    private static string Gravar<T>(T valor) => JsonSerializer.Serialize(valor, Json);

    private static T? Ler<T>(string json) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return null; }
    }
}
