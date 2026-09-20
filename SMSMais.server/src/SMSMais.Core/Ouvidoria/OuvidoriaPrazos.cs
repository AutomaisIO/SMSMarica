using SMSMais.Core.Common.Tempo;
using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Ouvidoria;

namespace SMSMais.Core.Ouvidoria;

/// <summary>
/// Cálculo puro de prazos da ouvidoria (D-4; Lei 13.460 art. 16). Sem banco, sem relógio —
/// quem chama passa o "hoje" (fuso da instância: <see cref="Hoje"/>). Dias úteis = seg–sex, sem
/// feriados (fase 1).
/// </summary>
public static class OuvidoriaPrazos
{
    /// <summary>"Hoje" no fuso da instância — REGRA ÚNICA do repo: UTC convertido para Brasília na exibição.</summary>
    public static DateOnly Hoje() => DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(DateTime.UtcNow));

    /// <summary>Data (fuso da instância) de um instante UTC.</summary>
    public static DateOnly DataDe(DateTime utc) => DateOnly.FromDateTime(FusoBrasilia.ParaExibicao(utc));

    /// <summary>Prazo de resposta ao cidadão: registro + N dias corridos.</summary>
    public static DateOnly PrazoCidadao(DateOnly registro, int dias) => registro.AddDays(dias);

    /// <summary>
    /// Prazo da área: Urgente = dias úteis da configuração; Alta = <c>PrazoAreaAltaDias</c>;
    /// Normal = prazo próprio do ponto, senão <c>PrazoAreaDias</c>.
    /// </summary>
    public static DateOnly PrazoArea(DateOnly hoje, OuvidoriaPrioridade prioridade, OuvidoriaConfiguracao config, int? prazoDoPonto)
        => prioridade switch
        {
            OuvidoriaPrioridade.Urgente => AdicionarDiasUteis(hoje, config.PrazoAreaUrgenteDiasUteis),
            OuvidoriaPrioridade.Alta => hoje.AddDays(config.PrazoAreaAltaDias),
            _ => hoje.AddDays(prazoDoPonto ?? config.PrazoAreaDias),
        };

    /// <summary>Dias além do prazo (nunca negativo).</summary>
    public static int DiasAtraso(DateOnly prazo, DateOnly hoje)
        => Math.Max(0, hoje.DayNumber - prazo.DayNumber);

    /// <summary>Faixa do tempo de resposta para o painel: <c>ate30 | 31a60 | mais60</c>.</summary>
    public static string FaixaPrazo(int dias) => dias switch
    {
        <= 30 => "ate30",
        <= 60 => "31a60",
        _ => "mais60",
    };

    /// <summary>Soma dias úteis (seg–sex) a partir de <paramref name="inicio"/>, pulando fim de semana.</summary>
    public static DateOnly AdicionarDiasUteis(DateOnly inicio, int diasUteis)
    {
        var data = inicio;
        var restantes = Math.Max(0, diasUteis);
        while (restantes > 0)
        {
            data = data.AddDays(1);
            if (data.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                restantes--;
            }
        }
        return data;
    }
}
