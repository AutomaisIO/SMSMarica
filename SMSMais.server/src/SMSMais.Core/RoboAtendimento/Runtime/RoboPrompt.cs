using System.Text;
using SMSMais.Data.Entities.Robo;

namespace SMSMais.Core.RoboAtendimento.Runtime;

/// <summary>
/// Monta o prompt de sistema do robô a partir da persona global, do assunto e dos treinos — e diz
/// se o assunto está dentro do próprio horário. Vivia privado no
/// <see cref="RoboAtendimentoProcessador"/>; foi extraído para que a SIMULAÇÃO (tela do módulo
/// Robô) produza exatamente o mesmo prompt do atendimento real. Se divergirem, a simulação deixa
/// de valer como ensaio.
/// </summary>
public static class RoboPrompt
{
    /// <param name="pertoDoLimite">Última resposta antes de o robô se calar por teto de interações.
    /// Em vez de sumir no meio da conversa, ele mesmo prepara a pessoa para a passagem.</param>
    public static string MontarInstrucao(
        string personaGlobal, RoboAssunto? assunto, bool dentroHorario, string? urlApp, bool pertoDoLimite = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine(personaGlobal.Trim());
        sb.AppendLine();
        if (assunto is null)
        {
            sb.AppendLine("Você não identificou um assunto específico para esta mensagem. Tente entender, "
                + "de forma cordial, do que a pessoa precisa; se não puder ajudar, encaminhe para um atendente humano.");
        }
        else
        {
            sb.AppendLine($"Assunto: {assunto.Nome}. {assunto.InstrucoesPersona.Trim()}");
            var regras = assunto.Treinos.Where(t => t.Ativo).OrderBy(t => t.Ordem).ToList();
            if (regras.Count > 0)
            {
                sb.AppendLine().AppendLine("Regras (siga cada uma):");
                foreach (var r in regras)
                {
                    var titulo = string.IsNullOrWhiteSpace(r.Titulo) ? string.Empty : $"{r.Titulo.Trim()}: ";
                    sb.AppendLine($"- {titulo}{r.Conteudo.Trim()}");
                }
            }
        }
        sb.AppendLine();
        sb.AppendLine(dentroHorario
            ? "Há atendente humano disponível no horário. NÃO ofereça encaminhar para um atendente por "
              + "conta própria: só encaminhe se a pessoa PEDIR um atendente humano ou se você realmente não "
              + "conseguir resolver — nunca de forma preventiva nem como fecho de cortesia."
            : "ESTAMOS FORA DO HORÁRIO DE ATENDIMENTO HUMANO: não há atendente disponível agora. NÃO ofereça "
              + "nem prometa encaminhar para um atendente. Ajude no que puder; se não resolver, oriente a pessoa "
              + "a procurar o atendimento humano dentro do horário.");
        if (pertoDoLimite)
        {
            sb.AppendLine();
            sb.AppendLine(dentroHorario
                ? "ATENÇÃO: esta é a ÚLTIMA mensagem que você pode enviar nesta conversa. Responda o "
                  + "que der e, no fim, avise com naturalidade que a partir daqui um ATENDENTE vai "
                  + "continuar o atendimento — sem dizer que existe limite, sistema ou robô."
                : "ATENÇÃO: esta é a ÚLTIMA mensagem que você pode enviar nesta conversa e estamos FORA "
                  + "do horário de atendimento. Responda o que der e, no fim, oriente a pessoa a "
                  + "retornar o contato dentro do horário comercial — sem prometer atendente agora e "
                  + "sem falar em limite, sistema ou robô.");
        }
        sb.AppendLine();
        var app = string.IsNullOrWhiteSpace(urlApp) ? "o aplicativo do cidadão da prefeitura" : urlApp!.Trim();
        sb.AppendLine($"AO SE DESPEDIR, sempre oriente a pessoa: acesse {app} — lá ficam os exames, consultas e "
            + "atendimentos que ela já teve na rede municipal; peça para manter os dados sempre atualizados.");
        return sb.ToString();
    }

    public static bool DentroDoHorario(RoboAssunto a)
    {
        // Regra única de fuso: Brasília fixo (UTC-3).
        var agora = DateTime.UtcNow.AddHours(-3);

        if (a.DiasSemana is int mask)
        {
            var bit = (int)agora.DayOfWeek; // domingo = 0
            if ((mask & (1 << bit)) == 0) return false;
        }

        if (a.HorarioInicio is { } ini && a.HorarioFim is { } fim)
        {
            var hora = TimeOnly.FromDateTime(agora);
            if (ini <= fim) return hora >= ini && hora <= fim;
            return hora >= ini || hora <= fim; // vira a meia-noite
        }
        return true;
    }
}
