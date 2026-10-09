using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Regulacao.AnaliseRegras.Dtos;
using SMSMais.Core.Regulacao.Configuracao;
using SMSMais.Core.Regulacao.Regras;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.EsusSg;
using SMSMais.Data.Entities.Regulacao;
using SMSMais.Data.Entities.Ser;
using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Regulacao.AnaliseRegras;

/// <summary>Balanço de uma passada da análise.</summary>
public sealed record ResultadoAnaliseRegras(int Lidos, int Analisados, int SemMudanca);

/// <summary>
/// <b>Análise automática das regras de elegibilidade sobre os pedidos que CHEGAM</b> dos sistemas
/// externos — SER, SERNIT e ESUS de São Gonçalo (ADR-0063 §4, pedido do Bernardo em 30/09/2026).
///
/// <para>Até aqui as regras (<c>regulacao_regra</c>, plano 03) só rodavam no assistente de Nova
/// Solicitação. O que alguém incluía direto no SER/SERNIT/ESUS nunca era conferido. Esta análise
/// usa o MESMO avaliador puro (<see cref="AvaliadorElegibilidade"/>) e o MESMO critério de regras
/// do assistente (regras ativas do procedimento canônico; regra com sistema só vale para aquele
/// sistema), com o que o espelho sabe: nascimento, sexo, CPF e CID.</para>
///
/// <para><b>O que ela não faz:</b> não responde pergunta nem anexa documento — isso é juízo de
/// pessoa. Pergunta que bloqueia e não tem resposta vira <see cref="VereditoAnaliseRegras.AConferir"/>.
/// <b>Documento não decide o parecer</b> (09/10/2026): os anexos ficam no sistema de origem e a
/// análise não os enxerga — ele só aparece listado, para o regulador conferir lá. E ela não escreve
/// nada no sistema externo nem muda o pedido: é um parecer ao lado, para o agente olhar.</para>
///
/// <para><b>Só pedidos em aberto</b> (em fila ou pendentes): analisar o que já foi agendado ou teve
/// alta seria ruído. A última análise de um pedido que fechou fica guardada.</para>
///
/// <para><b>Reanálise por hash:</b> cada análise guarda o hash da entrada (dado do pedido + versão
/// das regras do procedimento). Regra editada na tela muda o hash, e a próxima passada reanalisa
/// sozinha — sem gatilho de edição.</para>
/// </summary>
public interface IAnaliseRegrasEspelhoService
{
    /// <summary>Analisa os pedidos em aberto cujo hash mudou. Chamado pelo worker.</summary>
    Task<ResultadoAnaliseRegras> AnalisarPendentesAsync(SistemaRegulacao sistema, int limite, CancellationToken ct);

    /// <summary>Reanalisa um pedido agora, ignorando o hash (botão "reanalisar" do detalhe).</summary>
    Task<AnaliseRegrasDetalheDto?> ReanalisarAsync(SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct);

    Task<AnaliseRegrasDetalheDto?> ObterDetalheAsync(SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, AnaliseRegrasResumoDto>> ResumosAsync(
        SistemaRegulacao sistema, IReadOnlyCollection<Guid> espelhoIds, CancellationToken ct);

    Task<IReadOnlyList<AnaliseRegrasContagemDto>> ContagemAsync(SistemaRegulacao sistema, CancellationToken ct);
}

public sealed partial class AnaliseRegrasEspelhoService(
    SmsMaisDbContext db,
    IRegulacaoConfiguracaoService configuracao,
    ILogger<AnaliseRegrasEspelhoService> log) : IAnaliseRegrasEspelhoService
{
    /// <summary>Os sistemas que a análise cobre — os três espelhos com pedido vindo de fora.</summary>
    public static readonly SistemaRegulacao[] Cobertos =
        [SistemaRegulacao.Ser, SistemaRegulacao.Sernit, SistemaRegulacao.EsusSg];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>O que a análise precisa saber de um pedido de espelho, venha de onde vier.</summary>
    private sealed record Entrada(
        Guid EspelhoId,
        string Numero,
        TipoProcedimentoRegulacao Tipo,
        string Recurso,
        Guid? PacienteId,
        DateOnly? Nascimento,
        string? Sexo,
        string? Cpf,
        string? Cid);

    /// <summary>O que vai gravado em <c>alertas_json</c>.</summary>
    private sealed record Alertas(
        IReadOnlyList<RegraAvaliadaDto> Regras,
        IReadOnlyList<PerguntaPendenteDto> Perguntas,
        IReadOnlyList<string> Documentos);

    public async Task<ResultadoAnaliseRegras> AnalisarPendentesAsync(
        SistemaRegulacao sistema, int limite, CancellationToken ct)
    {
        var entradas = await EntradasEmAbertoAsync(sistema, ct);
        if (entradas.Count == 0) return new ResultadoAnaliseRegras(0, 0, 0);

        var contexto = await ContextoAsync(sistema, ct);
        var ids = entradas.Select(e => e.EspelhoId).ToList();
        var existentes = await CarregarExistentesAsync(sistema, ids, ct);

        var analisados = 0;
        var semMudanca = 0;
        foreach (var e in entradas)
        {
            ct.ThrowIfCancellationRequested();
            var (procedimentoId, regras) = contexto.Resolver(e);
            var hash = Hash(sistema, e, procedimentoId, contexto.HashRegras(procedimentoId), contexto.NaoSei);
            existentes.TryGetValue(e.EspelhoId, out var atual);
            if (atual?.EntradaHash == hash)
            {
                semMudanca++;
                continue;
            }

            Aplicar(sistema, e, procedimentoId, regras, contexto.NaoSei, hash, atual);
            analisados++;
            if (analisados % 200 == 0) await db.SaveChangesAsync(ct);
            if (analisados >= limite) break;
        }

        await db.SaveChangesAsync(ct);
        if (analisados > 0)
        {
            log.LogInformation("Análise de regras {Sistema}: {Analisados} pedido(s) analisado(s), {Iguais} sem mudança.",
                sistema, analisados, semMudanca);
        }
        return new ResultadoAnaliseRegras(entradas.Count, analisados, semMudanca);
    }

    public async Task<AnaliseRegrasDetalheDto?> ReanalisarAsync(
        SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct)
    {
        var entrada = (await EntradasAsync(sistema, [espelhoId], somenteAbertos: false, ct)).FirstOrDefault();
        if (entrada is null) return null;

        var contexto = await ContextoAsync(sistema, ct);
        var atual = (await CarregarExistentesAsync(sistema, [espelhoId], ct)).GetValueOrDefault(espelhoId);
        var (procedimentoId, regras) = contexto.Resolver(entrada);
        var hash = Hash(sistema, entrada, procedimentoId, contexto.HashRegras(procedimentoId), contexto.NaoSei);
        Aplicar(sistema, entrada, procedimentoId, regras, contexto.NaoSei, hash, atual);
        await db.SaveChangesAsync(ct);
        return await ObterDetalheAsync(sistema, espelhoId, ct);
    }

    public async Task<AnaliseRegrasDetalheDto?> ObterDetalheAsync(
        SistemaRegulacao sistema, Guid espelhoId, CancellationToken ct)
    {
        var a = await db.RegulacaoAnalisesEspelho.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Sistema == sistema && x.EspelhoId == espelhoId, ct);
        if (a is null) return null;

        string? nome = null;
        if (a.ProcedimentoId is { } pid)
        {
            nome = await db.RegulacaoProcedimentos.AsNoTracking()
                .Where(p => p.Id == pid).Select(p => p.NomeCanonico).FirstOrDefaultAsync(ct);
        }

        Alertas? alertas = null;
        try
        {
            alertas = JsonSerializer.Deserialize<Alertas>(a.AlertasJson, Json);
        }
        catch (JsonException ex)
        {
            log.LogWarning(ex, "alertas_json ilegível na análise {Id}.", a.Id);
        }

        return new AnaliseRegrasDetalheDto(
            ParaResumo(a), a.ProcedimentoId, nome,
            alertas?.Regras ?? [], alertas?.Perguntas ?? [], alertas?.Documentos ?? []);
    }

    public async Task<IReadOnlyDictionary<Guid, AnaliseRegrasResumoDto>> ResumosAsync(
        SistemaRegulacao sistema, IReadOnlyCollection<Guid> espelhoIds, CancellationToken ct)
    {
        if (espelhoIds.Count == 0) return new Dictionary<Guid, AnaliseRegrasResumoDto>();
        var ids = espelhoIds.ToList();
        return await db.RegulacaoAnalisesEspelho.AsNoTracking()
            .Where(x => x.Sistema == sistema && ids.Contains(x.EspelhoId))
            .ToDictionaryAsync(x => x.EspelhoId, ParaResumo, ct);
    }

    public async Task<IReadOnlyList<AnaliseRegrasContagemDto>> ContagemAsync(
        SistemaRegulacao sistema, CancellationToken ct)
    {
        // Conta só o que está em aberto no espelho — o cartão da fila fala do que está esperando.
        var abertos = (await EntradasEmAbertoAsync(sistema, ct)).Select(e => e.EspelhoId).ToHashSet();
        var linhas = await db.RegulacaoAnalisesEspelho.AsNoTracking()
            .Where(x => x.Sistema == sistema)
            .Select(x => new { x.EspelhoId, x.Veredito })
            .ToListAsync(ct);
        return linhas.Where(l => abertos.Contains(l.EspelhoId))
            .GroupBy(l => l.Veredito)
            .Select(g => new AnaliseRegrasContagemDto(g.Key, g.Count()))
            .OrderBy(c => c.Veredito)
            .ToList();
    }

    // ------------------------------------------------------------------ avaliação

    private void Aplicar(
        SistemaRegulacao sistema, Entrada e, Guid? procedimentoId, IReadOnlyList<RegulacaoRegra> regras,
        NaoSeiViraRegulacao naoSei, string hash, RegulacaoAnaliseEspelho? atual)
    {
        var agora = DateTime.UtcNow;
        var alvo = atual ?? new RegulacaoAnaliseEspelho
        {
            Id = Guid.CreateVersion7(),
            Sistema = sistema,
            EspelhoId = e.EspelhoId,
            CriadoEm = agora,
        };
        if (atual is null) db.RegulacaoAnalisesEspelho.Add(alvo);

        alvo.NumeroExterno = e.Numero.Length <= 40 ? e.Numero : e.Numero[..40];
        alvo.ProcedimentoId = procedimentoId;
        alvo.PacienteId = e.PacienteId;
        alvo.EntradaHash = hash;
        alvo.AnalisadoEm = agora;

        var avaliaveis = regras.Where(r => r.Tipo != TipoRegraRegulacao.Informativa).ToList();
        if (procedimentoId is null || avaliaveis.Count == 0)
        {
            alvo.Veredito = procedimentoId is null ? VereditoAnaliseRegras.SemProcedimento : VereditoAnaliseRegras.SemRegras;
            alvo.Bloqueios = alvo.Ressalvas = alvo.Avisos = alvo.PerguntasPendentes = alvo.DocumentosPendentes = 0;
            alvo.Resumo = procedimentoId is null
                ? $"\"{Recortar(e.Recurso, 120)}\" não está ligado a um procedimento do catálogo."
                : "O procedimento não tem regra que valha para este sistema.";
            alvo.AlertasJson = JsonSerializer.Serialize(new Alertas(
                regras.Select(r => new RegraAvaliadaDto(r.Id, r.Versao, r.Tipo, r.Severidade, r.Sistema, r.Descricao,
                    ResultadoRegraRegulacao.Atende, null)).ToList(), [], []), Json);
            return;
        }

        var avaliacao = AvaliadorElegibilidade.Avaliar(
            regras,
            new PacienteParaRegras(e.Nascimento, e.Sexo, e.Cpf, null),
            e.Cid,
            new Dictionary<Guid, RespostaRegraRegulacao>(),
            [],
            [sistema],
            FusoBrasilia.HojeEmBrasilia(),
            naoSei);

        var bloqueios = avaliacao.Regras.Where(r => r.Resultado == ResultadoRegraRegulacao.Bloqueia).ToList();
        var ressalvas = avaliacao.Regras.Where(r => r.Resultado == ResultadoRegraRegulacao.Ressalva).ToList();
        var avisos = avaliacao.Regras.Count(r => r.Severidade == SeveridadeRegraRegulacao.Aviso
                                                  && r.Resultado != ResultadoRegraRegulacao.Atende);
        var perguntasQueTravam = avaliacao.PerguntasPendentes
            .Where(p => p.Severidade == SeveridadeRegraRegulacao.Bloqueia).ToList();
        // Documento NÃO decide o parecer (09/10/2026). A análise não enxerga os anexos do pedido —
        // eles ficam no sistema de origem —, então todo documento obrigatório ficava "pendente" para
        // sempre, e qualquer procedimento com a regra do encaminhamento caía em "A conferir": o
        // parecer virava ruído e escondia o pedido que de fato tem pergunta em aberto. O documento
        // segue listado (alertas e resumo) para o regulador conferir no sistema de origem.
        var documentosObrigatorios = avaliacao.DocumentosPendentes
            .Where(d => d.Obrigatorio)
            .Select(d => d.Rotulo)
            .ToList();

        alvo.Bloqueios = bloqueios.Count;
        alvo.Ressalvas = ressalvas.Count;
        alvo.Avisos = avisos;
        alvo.PerguntasPendentes = avaliacao.PerguntasPendentes.Count;
        alvo.DocumentosPendentes = avaliacao.DocumentosPendentes.Count(d => d.Obrigatorio);

        if (avaliacao.MotivosDeBloqueio.TryGetValue(sistema, out var motivo))
        {
            alvo.Veredito = VereditoAnaliseRegras.Bloqueado;
            alvo.Resumo = Recortar(motivo, 500);
        }
        else if (perguntasQueTravam.Count > 0)
        {
            alvo.Veredito = VereditoAnaliseRegras.AConferir;
            alvo.Resumo = Recortar($"Conferir: {perguntasQueTravam[0].Pergunta}", 500);
        }
        else if (avaliacao.DestinosComRessalva.Contains(sistema) || ressalvas.Count > 0)
        {
            alvo.Veredito = VereditoAnaliseRegras.ComRessalva;
            alvo.Resumo = Recortar(ressalvas.FirstOrDefault()?.Motivo ?? ressalvas.FirstOrDefault()?.Descricao, 500);
        }
        else
        {
            alvo.Veredito = VereditoAnaliseRegras.Apto;
            alvo.Resumo = documentosObrigatorios.Count == 0
                ? null
                : Recortar(
                    $"Nada do que o sistema confere barrou. Conferir no {NomeSistema(sistema)} se o pedido traz: "
                    + string.Join("; ", documentosObrigatorios) + ".", 500);
        }

        alvo.AlertasJson = JsonSerializer.Serialize(new Alertas(
            avaliacao.Regras,
            avaliacao.PerguntasPendentes,
            documentosObrigatorios), Json);
    }

    // ------------------------------------------------------------------ entradas por sistema

    private Task<List<Entrada>> EntradasEmAbertoAsync(SistemaRegulacao sistema, CancellationToken ct) =>
        EntradasAsync(sistema, null, somenteAbertos: true, ct);

    private async Task<List<Entrada>> EntradasAsync(
        SistemaRegulacao sistema, IReadOnlyCollection<Guid>? ids, bool somenteAbertos, CancellationToken ct)
    {
        var lista = ids?.ToList();
        switch (sistema)
        {
            case SistemaRegulacao.Ser:
            {
                var q = db.SerSolicitacoes.AsNoTracking().Where(x => x.ExcluidoEm == null);
                if (somenteAbertos) q = q.Where(x => x.Situacao == SituacaoSer.EmFila || x.Situacao == SituacaoSer.Pendente);
                if (lista is not null) q = q.Where(x => lista.Contains(x.Id));
                return (await q.Select(x => new { x.Id, x.IdSer, x.Tipo, x.Recurso, x.PacienteId, x.DataNascimento, x.Sexo, x.Cpf, x.Cid })
                        .ToListAsync(ct))
                    .Select(x => new Entrada(x.Id, x.IdSer,
                        x.Tipo == TipoRecursoSer.Exame ? TipoProcedimentoRegulacao.Exame : TipoProcedimentoRegulacao.Consulta,
                        x.Recurso, x.PacienteId, x.DataNascimento, x.Sexo, x.Cpf, CodigoCid(x.Cid)))
                    .ToList();
            }
            case SistemaRegulacao.Sernit:
            {
                var q = db.SernitSolicitacoes.AsNoTracking().Where(x => x.ExcluidoEm == null);
                if (somenteAbertos) q = q.Where(x => x.Situacao == SituacaoSernit.EmFila || x.Situacao == SituacaoSernit.Pendente);
                if (lista is not null) q = q.Where(x => lista.Contains(x.Id));
                return (await q.Select(x => new { x.Id, x.IdSernit, x.Tipo, x.Recurso, x.PacienteId, x.DataNascimento, x.Sexo, x.Cpf, x.Cid })
                        .ToListAsync(ct))
                    .Select(x => new Entrada(x.Id, x.IdSernit,
                        x.Tipo == TipoRecursoSernit.Exame ? TipoProcedimentoRegulacao.Exame : TipoProcedimentoRegulacao.Consulta,
                        x.Recurso, x.PacienteId, x.DataNascimento, x.Sexo, x.Cpf, CodigoCid(x.Cid)))
                    .ToList();
            }
            case SistemaRegulacao.EsusSg:
            {
                var q = db.EsusSgSolicitacoes.AsNoTracking().Where(x => x.ExcluidoEm == null);
                if (somenteAbertos) q = q.Where(x => x.Situacao == SituacaoEsusSg.EmFila || x.Situacao == SituacaoEsusSg.Pendente);
                if (lista is not null) q = q.Where(x => lista.Contains(x.Id));
                // O ESUS não traz CID na fila de Maricá.
                return (await q.Select(x => new { x.Id, x.IdEsusSg, x.Tipo, x.Recurso, x.PacienteId, x.DataNascimento, x.Sexo, x.Cpf })
                        .ToListAsync(ct))
                    .Select(x => new Entrada(x.Id, x.IdEsusSg,
                        x.Tipo == TipoRecursoEsusSg.Exame ? TipoProcedimentoRegulacao.Exame : TipoProcedimentoRegulacao.Consulta,
                        x.Recurso, x.PacienteId, x.DataNascimento, x.Sexo, x.Cpf, null))
                    .ToList();
            }
            default:
                return [];
        }
    }

    private async Task<Dictionary<Guid, RegulacaoAnaliseEspelho>> CarregarExistentesAsync(
        SistemaRegulacao sistema, List<Guid> ids, CancellationToken ct)
    {
        var mapa = new Dictionary<Guid, RegulacaoAnaliseEspelho>();
        foreach (var lote in ids.Chunk(2000))
        {
            var l = lote.ToList();
            foreach (var a in await db.RegulacaoAnalisesEspelho
                         .Where(x => x.Sistema == sistema && l.Contains(x.EspelhoId)).ToListAsync(ct))
            {
                mapa[a.EspelhoId] = a;
            }
        }
        return mapa;
    }

    // ------------------------------------------------------------------ contexto (catálogo + regras)

    private sealed class Contexto(
        SistemaRegulacao sistema,
        Dictionary<string, Guid> procedimentoPorRotulo,
        Dictionary<Guid, List<RegulacaoRegra>> regrasPorProcedimento,
        NaoSeiViraRegulacao naoSei)
    {
        public NaoSeiViraRegulacao NaoSei { get; } = naoSei;

        /// <summary>Pedido → procedimento canônico pela origem do catálogo do MESMO sistema, casando
        /// o rótulo (o espelho só guarda o texto do recurso). Regras do procedimento que valem para
        /// o sistema: sem sistema (todas) ou com este sistema.</summary>
        public (Guid? ProcedimentoId, IReadOnlyList<RegulacaoRegra> Regras) Resolver(Entrada e)
        {
            if (!procedimentoPorRotulo.TryGetValue(ChaveRotulo.Normalizar(e.Recurso), out var pid))
            {
                return (null, []);
            }
            var regras = regrasPorProcedimento.TryGetValue(pid, out var r)
                ? r.Where(x => x.Sistema is null || x.Sistema == sistema).ToList()
                : [];
            return (pid, regras);
        }

        public string HashRegras(Guid? procedimentoId)
        {
            if (procedimentoId is not { } pid || !regrasPorProcedimento.TryGetValue(pid, out var r)) return "-";
            return string.Join(',', r.Where(x => x.Sistema is null || x.Sistema == sistema)
                .OrderBy(x => x.Id).Select(x => $"{x.Id:N}:{x.Versao}:{x.AtualizadoEm?.Ticks ?? 0}"));
        }
    }

    private async Task<Contexto> ContextoAsync(SistemaRegulacao sistema, CancellationToken ct)
    {
        var origens = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.Sistema == sistema && o.Ativo)
            .Select(o => new { o.RotuloExterno, o.ProcedimentoId, o.ConfirmadoEm })
            .ToListAsync(ct);

        // Confirmada por pessoa ganha do automático quando o mesmo rótulo aparece duas vezes
        // (o SER tem o mesmo recurso nos ramos AE e não-AE).
        var porRotulo = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var o in origens.OrderByDescending(o => o.ConfirmadoEm.HasValue))
        {
            porRotulo.TryAdd(ChaveRotulo.Normalizar(o.RotuloExterno), o.ProcedimentoId);
        }

        var procedimentos = porRotulo.Values.Distinct().ToList();
        var regras = await db.RegulacaoRegras.AsNoTracking()
            .Where(r => procedimentos.Contains(r.ProcedimentoId) && r.Ativo && r.ExcluidoEm == null)
            .OrderBy(r => r.Ordem)
            .ToListAsync(ct);

        var config = await configuracao.ObterEntidadeAsync(ct);
        return new Contexto(
            sistema,
            porRotulo,
            regras.GroupBy(r => r.ProcedimentoId).ToDictionary(g => g.Key, g => g.ToList()),
            config.NaoSeiPadrao);
    }

    // ------------------------------------------------------------------ util

    private static string NomeSistema(SistemaRegulacao s) => s switch
    {
        SistemaRegulacao.Ser => "SER",
        SistemaRegulacao.Sernit => "SERNIT",
        SistemaRegulacao.EsusSg => "ESUS",
        SistemaRegulacao.Sisreg => "SISREG",
        _ => s.ToString(),
    };

    /// <summary>"J353 - Hipertrofia das amígdalas" → "J35.3". O avaliador compara por PREFIXO
    /// ("C50" aceita "C50.4"), então o código vai no formato pontuado da CID-10.</summary>
    internal static string? CodigoCid(string? cid)
    {
        if (string.IsNullOrWhiteSpace(cid)) return null;
        var m = RegexCid().Match(cid.Trim().ToUpperInvariant());
        if (!m.Success) return null;
        var letraNum = m.Groups[1].Value;
        var sub = m.Groups[2].Value;
        return sub.Length > 0 ? $"{letraNum}.{sub}" : letraNum;
    }

    [GeneratedRegex(@"^([A-Z]\d{2})\.?(\d?)")]
    private static partial Regex RegexCid();

    private static string Hash(
        SistemaRegulacao sistema, Entrada e, Guid? procedimentoId, string hashRegras, NaoSeiViraRegulacao naoSei)
    {
        var texto = string.Join('|', [
            ((int)sistema).ToString(), procedimentoId?.ToString("N") ?? "-", e.Nascimento?.ToString("O") ?? "-",
            e.Sexo ?? "-", string.IsNullOrWhiteSpace(e.Cpf) ? "0" : "1", e.Cid ?? "-", hashRegras, ((int)naoSei).ToString(),
            // A idade (em anos, como as regras a leem) muda com o tempo: entra no hash para a regra
            // de idade virar no aniversário — sem reanalisar tudo todo dia.
            IdadeEmAnos(e.Nascimento)?.ToString() ?? "-",
        ]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
    }

    private static int? IdadeEmAnos(DateOnly? nascimento)
    {
        if (nascimento is not { } n) return null;
        var hoje = FusoBrasilia.HojeEmBrasilia();
        var idade = hoje.Year - n.Year;
        if (hoje < n.AddYears(idade)) idade--;
        return idade;
    }

    private static AnaliseRegrasResumoDto ParaResumo(RegulacaoAnaliseEspelho a) => new(
        a.Veredito, a.Resumo, a.Bloqueios, a.Ressalvas, a.PerguntasPendentes, a.DocumentosPendentes, a.AnalisadoEm);

    private static string? Recortar(string? s, int max) =>
        s is null ? null : s.Length <= max ? s : s[..max];
}
