using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Integracoes.Credenciais;
using SMSMais.Core.Integracoes.Credenciais.Dtos;

namespace SMSMais.Core.Integracoes.SisregWeb.Fila;

/// <summary>A releitura completa diária da fila: ligada ou não, e a que horas (Brasília).</summary>
public sealed record FilaAgendamentoDto(bool Ativo, string HoraLocal);

public sealed record SalvarFilaAgendamentoRequest(bool Ativo, string? HoraLocal);

/// <summary>
/// Onde mora o horário da releitura diária da fila: no <c>ParametrosJson</c> da credencial
/// <c>sisreg</c>, o mesmo lugar do agendamento das escalas e do lote de mapeamento. Sem tabela nova,
/// sem migration.
///
/// <para><b>Por que reler o passado todo dia.</b> Decidido em 12/09/2026. A leitura só "daqui para
/// frente" (31 dias) e o fechamento pela agenda cobrem entradas e 94% das saídas, mas não quatro
/// coisas que mudam em pedido antigo sem aviso: saída sem agendamento (cancelado, negado,
/// devolvido); pedido antigo reenviado, que volta com a data original (8 das 9 pessoas de 2023
/// eram reenviadas); troca de risco, procedimento e CID; e os nossos próprios erros de leitura,
/// que só se curam relendo. A releitura completa custa ~88 requisições (~20 min) — menos da metade
/// do que a varredura de agendas gasta por dia.</para>
/// </summary>
public static class AgendamentoDaFila
{
    public const string ChaveAtivo = "filaReleituraAtiva";
    public const string ChaveHora = "filaReleituraHora";

    /// <summary>
    /// Madrugada, longe do expediente (a sessão do SISREG é única por operador) e antes das 04:30,
    /// quando a varredura das agendas costuma começar.
    /// </summary>
    public const string HoraPadrao = "03:00";

    /// <summary>
    /// Sem configuração, a releitura vem <b>ligada</b>: é o que mantém a fila verdadeira, e desligar
    /// é decisão de alguém. A chave-mestra do sincronismo automático continua valendo por cima.
    /// </summary>
    public static FilaAgendamentoDto Ler(JsonObject? json)
    {
        var ativo = true;
        if (json?[ChaveAtivo] is JsonValue a && a.TryGetValue<bool>(out var b)) ativo = b;

        var hora = HoraPadrao;
        if (json?[ChaveHora] is JsonValue h && h.TryGetValue<string>(out var s)
            && TimeOnly.TryParseExact(s.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
        {
            hora = t.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        return new FilaAgendamentoDto(ativo, hora);
    }

    public static async Task<FilaAgendamentoDto> ObterAsync(
        IIntegracaoCredencialService credenciais, CancellationToken ct)
    {
        try
        {
            var atual = await credenciais.ObterAsync(SisregWebSessao.Provedor, ct);
            return Ler(Parse(atual.ParametrosJson));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Credencial ausente ou ilegível não pode derrubar o agendador: vale o padrão.
            return Ler(null);
        }
    }

    public static async Task<FilaAgendamentoDto> SalvarAsync(
        IIntegracaoCredencialService credenciais, SalvarFilaAgendamentoRequest request, CancellationToken ct)
    {
        if (!TimeOnly.TryParseExact(request.HoraLocal?.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var hora))
        {
            throw new ValidacaoException(
                "fila.hora_invalida", "Informe a hora da releitura no formato HH:mm (horário de Brasília).");
        }

        // Merge: usuário, senha, escalas e mapeamento vivem no MESMO ParametrosJson — sobrescrever
        // o JSON inteiro apagaria a credencial de acesso.
        var atual = await credenciais.ObterAsync(SisregWebSessao.Provedor, ct);
        var json = Parse(atual.ParametrosJson) ?? new JsonObject();
        json[ChaveAtivo] = request.Ativo;
        json[ChaveHora] = hora.ToString("HH:mm", CultureInfo.InvariantCulture);

        await credenciais.AtualizarAsync(
            SisregWebSessao.Provedor,
            new AtualizarIntegracaoCredencialRequest(
                ClientId: null,
                ClientSecret: null,
                RedirectUri: atual.RedirectUri,
                ParametrosJson: json.ToJsonString(),
                Ativo: atual.Ativo),
            ct);

        return Ler(json);
    }

    private static JsonObject? Parse(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        try
        {
            return JsonNode.Parse(texto) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
