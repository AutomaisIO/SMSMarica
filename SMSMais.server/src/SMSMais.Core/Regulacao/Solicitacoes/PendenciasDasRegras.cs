using SMSMais.Core.Regulacao.Regras;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Regulacao.Solicitacoes;

/// <summary>
/// O que as regras do manual seguram no envio da solicitação à pré-regulação (plano 03).
///
/// <para><b>Por que existe (09/10/2026).</b> O passo "Regras" do assistente mostrava as perguntas,
/// mas nada obrigava a respondê-las: o "Avançar" passava sempre, e o envio conferia CPF, destino,
/// formulário e caixinhas — não as regras. O pedido chegava ao técnico regulador sem garantia de
/// que o solicitante tinha visto o manual, e ele precisava conferir tudo de novo.</para>
///
/// <para><b>O que trava, e o que não trava.</b> Trava a pergunta <b>sem resposta nenhuma</b> que é
/// capaz de bloquear, e o destino que as regras barraram. <b>"Não sei" não trava</b> — a tela promete
/// isso ao solicitante ("marca o pedido para o agente conferir, em vez de travar você aqui"), e é o
/// caminho honesto para quem não tem a informação. Pergunta de ressalva ou aviso também não trava.</para>
///
/// <para><b>Só vale a regra do destino escolhido</b> (ou a regra sem sistema): uma pergunta do SER
/// não segura um pedido que vai para o SISREG.</para>
/// </summary>
public static class PendenciasDasRegras
{
    /// <summary>Motivo de bloqueio é texto do manual e pode ser longo; a pendência é uma linha.</summary>
    private const int LimiteMotivo = 300;

    public static IReadOnlyList<PendenciaEnvioDto> De(
        AvaliacaoElegibilidadeDto avaliacao, SistemaRegulacao? destino)
    {
        var pendencias = new List<PendenciaEnvioDto>();

        if (destino is { } d)
        {
            if (avaliacao.MotivosDeBloqueio.TryGetValue(d, out var motivo))
            {
                pendencias.Add(new PendenciaEnvioDto(
                    "regra.bloqueio",
                    $"Pelas regras do manual, este pedido não pode ir para o {Nome(d)}: {Recortar(motivo)}"));
            }
        }
        else if (avaliacao.DestinosPermitidos.Count == 0 && avaliacao.MotivosDeBloqueio.Count > 0)
        {
            // Sem destino escolhido e nenhum sobrando: o primeiro motivo diz por quê.
            pendencias.Add(new PendenciaEnvioDto(
                "regra.bloqueio",
                $"Pelas regras do manual, este pedido não pode seguir: {Recortar(avaliacao.MotivosDeBloqueio.Values.First())}"));
        }

        foreach (var p in avaliacao.PerguntasPendentes)
        {
            if (p.Severidade != SeveridadeRegraRegulacao.Bloqueia) continue;
            if (p.Sistema is { } s && s != destino) continue;

            pendencias.Add(new PendenciaEnvioDto(
                $"regra.{p.RegraId}",
                $"Responda no passo Regras: {p.Pergunta}"));
        }

        return pendencias;
    }

    private static string Nome(SistemaRegulacao s) => s switch
    {
        SistemaRegulacao.Sisreg => "SISREG",
        SistemaRegulacao.Ser => "SER",
        SistemaRegulacao.Sernit => "SERNIT",
        SistemaRegulacao.EsusSg => "ESUS de São Gonçalo",
        _ => s.ToString(),
    };

    private static string Recortar(string texto) =>
        texto.Length <= LimiteMotivo ? texto : texto[..LimiteMotivo].TrimEnd() + "…";
}
