using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Regulacao.Comum;
using SMSMais.Core.Regulacao.Formularios;
using SMSMais.Core.Regulacao.Solicitacoes;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.Conferencia;

/// <param name="Encontrado">O sistema achou o número. Falso com <see cref="Erro"/> nulo = leu e não achou.</param>
/// <param name="Erro">A leitura não aconteceu (sessão caída, orçamento do SISREG no fim). O número
/// continua registrado; é só conferir de novo depois.</param>
/// <param name="Divergencias">Quantos campos têm valor dos dois lados e não batem.</param>
public sealed record ConferenciaExternaDto(
    SistemaRegulacao Sistema,
    string Numero,
    DateTime ConferidoEm,
    bool Encontrado,
    string? Erro,
    int Divergencias,
    IReadOnlyList<ItemConferenciaDto> Itens);

public interface IRegulacaoConferenciaService
{
    /// <summary>
    /// Lê o pedido no sistema de destino pelo número registrado, com a credencial institucional, e
    /// compara com o nosso. Grava o resultado na linha do tempo e devolve. <b>Nunca falha por causa
    /// do sistema de lá</b> — falha de leitura volta em <see cref="ConferenciaExternaDto.Erro"/> —
    /// e não muda o estado da solicitação: o número registrado vale de qualquer jeito.
    /// </summary>
    Task<ConferenciaExternaDto> ConferirAsync(Guid solicitacaoId, CancellationToken ct);

    /// <summary>A conferência mais recente, ou <c>null</c> se nunca houve.</summary>
    Task<ConferenciaExternaDto?> UltimaAsync(Guid solicitacaoId, CancellationToken ct);
}

/// <summary>
/// Conferência do número digitado pelo técnico (pedido do Bernardo, 22/10/2026): quem lançou o pedido
/// à mão no SISREG/SER/SERNIT/ESUS e registrou o número aqui vê, na mesma hora, o que o sistema de
/// lá diz daquele pedido e o que diverge do que foi pedido aqui — "e vida que segue".
///
/// <para><b>Só leitura, com a credencial da integração</b> (a do sincronismo), nunca a pessoal de
/// quem clicou. É o que já se faz todo dia nas varreduras; aqui é um pedido só.</para>
/// </summary>
public sealed class RegulacaoConferenciaService(
    SmsMaisDbContext db,
    IRegulacaoEscopo escopo,
    IRegulacaoEventoService eventos,
    IEnumerable<ILeitorPedidoExterno> leitores,
    ILogger<RegulacaoConferenciaService> logger) : IRegulacaoConferenciaService
{
    /// <summary>camelCase e enum por nome: o detalhe do evento é lido pela tela como veio.</summary>
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<ConferenciaExternaDto> ConferirAsync(Guid solicitacaoId, CancellationToken ct)
    {
        await escopo.ExigirAgenteAsync(ct);
        var s = await db.RegulacaoSolicitacoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Solicitação da regulação", solicitacaoId);
        await escopo.ExigirAlcanceAsync(s, ct);

        if (s.NumeroExterno is not { Length: > 0 } numero || s.SistemaDestino is not { } sistema)
        {
            throw new ValidacaoException("numeroExterno", "Registre o número do sistema antes de conferir.");
        }

        var leitor = leitores.FirstOrDefault(l => l.Sistema == sistema)
            ?? throw new ValidacaoException("sistema", $"Não há leitura do {sistema} para conferir.");

        PedidoNoSistema? lido = null;
        string? erro = null;
        try
        {
            lido = await leitor.LerAsync(numero, ct);
        }
        catch (ConflitoException ex)
        {
            // Orçamento do SISREG no fim: a mensagem já diz quando tentar de novo.
            erro = ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sem dado de paciente no log: só o número e o tipo do erro.
            logger.LogWarning(ex, "Conferência: leitura do pedido {Numero} no {Sistema} falhou.", numero, sistema);
            erro = $"Não foi possível ler o pedido no {Nome(sistema)} agora. O número ficou registrado; confira de novo mais tarde.";
        }

        var itens = lido is null ? [] : ComparadorConferencia.Comparar(await MontarNossoAsync(s, sistema, ct), lido);
        var dto = new ConferenciaExternaDto(
            sistema, numero, DateTime.UtcNow, lido is not null, erro,
            itens.Count(i => i.Resultado == ResultadoItemConferencia.Diferente), itens);

        await eventos.RegistrarAsync(
            s.Id, TipoEventoRegulacao.ConferenciaExterna, PapelEventoRegulacao.Agente, ct,
            detalhe: JsonSerializer.SerializeToElement(dto, Json));
        await db.SaveChangesAsync(ct);

        return dto;
    }

    public async Task<ConferenciaExternaDto?> UltimaAsync(Guid solicitacaoId, CancellationToken ct)
    {
        var s = await db.RegulacaoSolicitacoes.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == solicitacaoId && x.ExcluidoEm == null, ct)
            ?? throw new NaoEncontradoException("Solicitação da regulação", solicitacaoId);
        await escopo.ExigirAlcanceAsync(s, ct);

        var detalhe = await db.RegulacaoEventos.AsNoTracking()
            .Where(e => e.SolicitacaoId == solicitacaoId && e.Tipo == TipoEventoRegulacao.ConferenciaExterna)
            .OrderByDescending(e => e.CriadoEm).ThenByDescending(e => e.Id)
            .Select(e => e.DetalheJson)
            .FirstOrDefaultAsync(ct);

        return detalhe is null ? null : JsonSerializer.Deserialize<ConferenciaExternaDto>(detalhe, Json);
    }

    /// <summary>O nosso pedido no vocabulário da conferência — o formulário e o catálogo daquele sistema.</summary>
    private async Task<NossoPedido> MontarNossoAsync(
        Data.Entities.Regulacao.RegulacaoSolicitacao s, SistemaRegulacao sistema, CancellationToken ct)
    {
        var form = LerCanonico(s.FormularioJson);

        // O rótulo do procedimento NAQUELE sistema vem primeiro: é o que o técnico escolheu lá.
        var origens = await db.RegulacaoProcedimentoOrigens.AsNoTracking()
            .Where(o => o.ProcedimentoId == s.ProcedimentoId && o.Sistema == sistema)
            .Select(o => new { o.RotuloExterno, o.ChaveExterna })
            .ToListAsync(ct);
        var canonico = await db.RegulacaoProcedimentos.AsNoTracking()
            .Where(p => p.Id == s.ProcedimentoId).Select(p => p.NomeCanonico).FirstOrDefaultAsync(ct);
        var rotulos = origens.Select(o => o.RotuloExterno).Append(canonico ?? string.Empty)
            .Where(r => r.Length > 0).Distinct().ToList();
        // Só o SISREG tem código simples na origem (o "pa"); a chave do SER/SERNIT é composta.
        var codigos = sistema == SistemaRegulacao.Sisreg ? origens.Select(o => o.ChaveExterna).ToList() : [];

        // A unidade que o pedido deve levar lá fora: a "em nome de" no NAR, a própria no Interno.
        var unidadeId = s.Fluxo switch
        {
            FluxoRegulacao.Nar => s.UnidadeEmNomeDeId,
            FluxoRegulacao.Interno => s.UnidadeSolicitanteId,
            _ => null,
        };
        var unidade = unidadeId is { } uid
            ? await db.Unidades.AsNoTracking().Where(u => u.Id == uid).Select(u => new { u.Nome, u.Cnes }).FirstOrDefaultAsync(ct)
            : null;

        // Médico: CPF e nome digitados no SISREG; nome escolhido da lista no SER/SERNIT/ESUS. Médico
        // pedido e ainda não cadastrado ("pendente:…") não é nome — fica de fora.
        var medicoLista = Texto(form, RegulacaoFormularioService.ChaveMedicoSolicitante);
        if (medicoLista?.StartsWith("pendente:", StringComparison.Ordinal) == true) medicoLista = null;

        return new NossoPedido(
            s.PacienteNome, s.PacienteCpf, s.PacienteCns,
            rotulos, codigos,
            Texto(form, "cid10") ?? Texto(form, RegulacaoFormularioService.ChaveHipoteseCid),
            unidade?.Nome, unidade?.Cnes,
            Texto(form, "profissional_solicitante_nome") ?? medicoLista,
            Texto(form, "profissional_solicitante_cpf"),
            Texto(form, RegulacaoFormularioService.ChaveClassificacaoRisco));
    }

    private static JsonElement LerCanonico(string formularioJson)
    {
        try
        {
            var raiz = JsonDocument.Parse(formularioJson).RootElement;
            return raiz.TryGetProperty("canonico", out var c) ? c.Clone() : default;
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string? Texto(JsonElement form, string chave) =>
        form.ValueKind == JsonValueKind.Object
        && form.TryGetProperty(chave, out var v)
        && v.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()
            : null;

    private static string Nome(SistemaRegulacao s) => s switch
    {
        SistemaRegulacao.Sisreg => "SISREG",
        SistemaRegulacao.Ser => "SER",
        SistemaRegulacao.Sernit => "SERNIT",
        SistemaRegulacao.EsusSg => "ESUS",
        _ => s.ToString(),
    };
}
