namespace SMSMais.Data.Entities.Alertas;

/// <summary>
/// Uma FONTE de aviso ("o robô parou", "a varredura do SER falhou", "erro no log de X"). É a linha
/// da tela "O que é reportado": diz o que existe, quantas vezes aconteceu e se está silenciada.
///
/// <para>As fontes conhecidas vêm do catálogo em código; as outras aparecem sozinhas na primeira
/// vez em que um erro novo é logado — é o que garante que um erro acrescentado amanhã já nasce
/// sendo reportado, sem ninguém lembrar de ligar.</para>
/// </summary>
public sealed class AlertaOrigem
{
    /// <summary>Chave estável (ex.: <c>robo.falha</c>, <c>log:Ser.VarreduraSerRunner</c>).</summary>
    public string Chave { get; set; } = string.Empty;

    /// <summary>Nome legível ("Robô de atendimento").</summary>
    public string Rotulo { get; set; } = string.Empty;

    /// <summary>Agrupamento da tela (Robô, Sincronismo, Sistema, IA…).</summary>
    public string Grupo { get; set; } = string.Empty;

    /// <summary>Silenciada = a ocorrência continua contada aqui, mas não sai para o celular.</summary>
    public bool Silenciada { get; set; }

    public DateTime CriadaEm { get; set; }
    public DateTime? UltimaOcorrenciaEm { get; set; }
    public int Ocorrencias { get; set; }

    /// <summary>Ocorrências desde o último aviso enviado (as que o freio segurou).</summary>
    public int OcorrenciasSemAviso { get; set; }

    public DateTime? UltimoAvisoEm { get; set; }

    /// <summary>Avisos seguidos sem a fonte esfriar — alonga o freio (30 min, 1 h, 2 h…).</summary>
    public int AvisosSeguidos { get; set; }
    public string? UltimoTitulo { get; set; }
    public string? UltimoDetalhe { get; set; }

    public string? AtualizadaPor { get; set; }
}
