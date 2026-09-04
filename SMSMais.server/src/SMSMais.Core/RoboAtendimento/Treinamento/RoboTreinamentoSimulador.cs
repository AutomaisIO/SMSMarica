using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SMSMais.Core.RoboAtendimento.Dtos;
using SMSMais.Core.RoboAtendimento.Runtime;
using SMSMais.Data;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento.Treinamento;

public interface IRoboTreinamentoSimulador
{
    /// <summary>Ensaia o caso contra o modelo treinado atual e julga se a crítica foi atendida.
    /// A simulação é adicionada ao contexto; quem chama decide o <c>SaveChanges</c>.</summary>
    Task<RoboTreinamentoSimulacao> SimularAsync(
        RoboTreinamentoItem item, string mensagem, IReadOnlyList<(string Papel, string Texto)> historico,
        bool automatica, Guid? quem, CancellationToken ct);
}

/// <summary>
/// A verificação do treinamento: roda o caso criticado contra o robô <b>como ele está agora</b> —
/// mesmo prompt, mesmas ferramentas, sem falar com o cidadão e sem executar comando de escrita — e
/// põe um juiz para dizer se a crítica original foi atendida.
///
/// Sem isso, "treinado" significaria apenas "o agente escreveu uma regra": ninguém saberia se a
/// regra muda o comportamento. É por isso que toda alteração aplicada deixa o item em
/// <see cref="StatusTreinamentoRobo.SimulacaoPendente"/> até rodar.
/// </summary>
public sealed class RoboTreinamentoSimulador(
    IRoboSimulacaoService simulacao,
    ClienteAnthropicTreinamento cliente,
    SmsMaisDbContext db,
    ILogger<RoboTreinamentoSimulador> logger) : IRoboTreinamentoSimulador
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<RoboTreinamentoSimulacao> SimularAsync(
        RoboTreinamentoItem item, string mensagem, IReadOnlyList<(string Papel, string Texto)> historico,
        bool automatica, Guid? quem, CancellationToken ct)
    {
        var registro = new RoboTreinamentoSimulacao
        {
            Id = Guid.CreateVersion7(),
            RoboTreinamentoItemId = item.Id,
            Mensagem = mensagem.Trim(),
            HistoricoJson = historico.Count == 0
                ? null
                : JsonSerializer.Serialize(historico.Select(h => new { papel = h.Papel, texto = h.Texto }), Json),
            Automatica = automatica,
            CriadoEm = DateTime.UtcNow,
            CriadoPor = quem,
        };
        db.RoboTreinamentoSimulacoes.Add(registro);

        RoboSimulacaoDto ensaio;
        try
        {
            // Deliberadamente SEM forçar o assunto: se a correção mexeu no roteamento, é aqui que
            // isso aparece — o classificador escolhe de novo, como no atendimento real.
            ensaio = await simulacao.SimularAsync(
                new SimularRoboRequest(
                    mensagem,
                    AssuntoId: null,
                    Historico: [.. historico.Select(h => new SimularRoboTurnoDto(h.Papel, h.Texto))]),
                ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Ensaio do item de treinamento {Item} falhou.", item.Id);
            registro.ErroMensagem = ex.Message;
            return registro;
        }

        registro.AssuntoNome = ensaio.Assunto;
        registro.Resposta = ensaio.Texto;
        registro.ChamadasJson = JsonSerializer.Serialize(ensaio.Chamadas, Json);
        registro.DuracaoMs = ensaio.DuracaoMs;
        registro.TokensEntrada = ensaio.TokensEntrada ?? 0;
        registro.TokensSaida = ensaio.TokensSaida ?? 0;
        registro.CustoUsd = ensaio.CustoUsd ?? 0m;

        try
        {
            var (veredito, analise, entrada, saida) = await JulgarAsync(item, ensaio, mensagem, historico, ct);
            registro.Veredito = veredito;
            registro.Analise = analise;
            registro.TokensEntrada += entrada;
            registro.TokensSaida += saida;
            registro.CustoUsd += PrecoModeloIa.Calcular(RoboTreinadorAgente.Modelo, entrada, saida) ?? 0m;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // O ensaio vale por si: a resposta simulada está gravada e o humano consegue julgar.
            logger.LogWarning(ex, "Juiz da simulação falhou no item {Item}.", item.Id);
            registro.Analise = $"Não foi possível avaliar automaticamente: {ex.Message}";
        }

        return registro;
    }

    private async Task<(VereditoSimulacaoTreinamento? Veredito, string? Analise, long Entrada, long Saida)>
        JulgarAsync(
            RoboTreinamentoItem item, RoboSimulacaoDto ensaio, string mensagem,
            IReadOnlyList<(string Papel, string Texto)> historico, CancellationToken ct)
    {
        const string sistema = """
            Você avalia se um robô de atendimento por WhatsApp corrigiu um comportamento que um
            atendente humano criticou.

            Você recebe: a crítica original, a resposta antiga (a que gerou a crítica) e a resposta
            que o robô dá HOJE para o mesmo caso, com os comandos que ele chamou.

            Julgue SOMENTE se a crítica foi atendida — não reescreva a resposta, não invente novas
            exigências e não penalize diferenças de estilo que a crítica não mencionou.

            - `Passou`: a resposta de hoje não incorre mais no que foi criticado.
            - `Falhou`: incorre no mesmo problema, ainda que com outras palavras.
            - `Duvidoso`: melhorou mas não resolve, resolve por acaso (não pela regra), ou o caso
              ensaiado não exercita de fato o que foi criticado.

            Se o robô mudou de assunto em relação ao original, diga se essa mudança é a correção
            pretendida ou um efeito colateral.

            Termine chamando `veredito_simulacao` exatamente uma vez.
            """;

        var texto = new StringBuilder();
        texto.AppendLine("## Crítica original").AppendLine().AppendLine(item.Critica.Trim()).AppendLine();
        if (!string.IsNullOrWhiteSpace(item.Observacao))
            texto.AppendLine("Observação de quem mandou treinar: " + item.Observacao!.Trim()).AppendLine();
        if (!string.IsNullOrWhiteSpace(item.Trecho))
            texto.AppendLine("## Resposta ANTIGA (criticada)").AppendLine().AppendLine("```")
                .AppendLine(item.Trecho!.Trim()).AppendLine("```").AppendLine();

        if (historico.Count > 0)
        {
            texto.AppendLine("## Histórico do ensaio").AppendLine();
            foreach (var (papel, t) in historico) texto.AppendLine($"- **{papel}**: {t.Trim()}");
            texto.AppendLine();
        }
        texto.AppendLine("## Mensagem do cidadão no ensaio").AppendLine().AppendLine(mensagem.Trim()).AppendLine();

        texto.AppendLine("## Resposta de HOJE").AppendLine();
        texto.AppendLine($"Assunto escolhido: {ensaio.Assunto ?? "(nenhum)"}");
        texto.AppendLine($"Encaminhou para humano: {(ensaio.HandOff ? "sim" : "não")}"
            + (string.IsNullOrWhiteSpace(ensaio.MotivoHandOff) ? string.Empty : $" ({ensaio.MotivoHandOff})"));
        texto.AppendLine().AppendLine("```").AppendLine(ensaio.Texto.Trim()).AppendLine("```").AppendLine();

        texto.AppendLine("Comandos chamados:");
        if (ensaio.Chamadas.Count == 0) texto.AppendLine("- nenhum");
        else foreach (var ch in ensaio.Chamadas)
            texto.AppendLine($"- `{ch.Comando}` → {ch.Resultado}");

        var ferramenta = new FerramentaModelo(
            "veredito_simulacao",
            "Registra se a crítica foi atendida pela resposta de hoje.",
            new
            {
                type = "object",
                additionalProperties = false,
                required = new[] { "veredito", "analise" },
                properties = new
                {
                    veredito = new { type = "string", @enum = new[] { "Passou", "Falhou", "Duvidoso" } },
                    analise = new
                    {
                        type = "string",
                        description = "Duas ou três frases, em português claro, para o operador ler na tela.",
                    },
                },
            });

        var mensagens = new List<object> { new { role = "user", content = texto.ToString() } };
        var turno = await cliente.ChamarAsync(
            RoboTreinadorAgente.Modelo, sistema, mensagens, [ferramenta], "medium", 2000, ct);

        var chamada = turno.Chamadas.FirstOrDefault(c => c.Nome == "veredito_simulacao");
        if (chamada is null) return (null, null, turno.TokensEntrada, turno.TokensSaida);

        var args = chamada.Argumentos;
        var v = args.TryGetProperty("veredito", out var pv) ? pv.GetString() : null;
        var a = args.TryGetProperty("analise", out var pa) ? pa.GetString() : null;

        return (
            Enum.TryParse<VereditoSimulacaoTreinamento>(v, true, out var parsed) ? parsed : null,
            a,
            turno.TokensEntrada,
            turno.TokensSaida);
    }
}
