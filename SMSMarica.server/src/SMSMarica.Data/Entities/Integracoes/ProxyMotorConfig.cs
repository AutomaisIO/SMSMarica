namespace SMSMarica.Data.Entities.Integracoes;

/// <summary>
/// Configuração de um <b>motor de proxy</b> de consulta externa (validação de CPF na
/// Receita, busca de CEP, ...). Cada serviço (<see cref="Servico"/> = <c>cpf</c>/<c>cep</c>)
/// pode ter vários motores; o orquestrador tenta os <see cref="Ativo"/> em ordem
/// (<see cref="Ordem"/>) e cai para o próximo quando um falha — cada um com seu próprio
/// <see cref="TimeoutSegundos"/> e <see cref="Tentativas"/>.
/// <para>
/// O <see cref="TokenCifrado"/> fica cifrado em repouso (IProtetorSegredos) e é write-only
/// na API (a tela só sabe se já está definido). Linha única por (<see cref="Servico"/>,
/// <see cref="Motor"/>).
/// </para>
/// </summary>
public class ProxyMotorConfig
{
    public Guid Id { get; set; }

    /// <summary>Serviço atendido pelo motor: <c>"cpf"</c> ou <c>"cep"</c>.</summary>
    public string Servico { get; set; } = string.Empty;

    /// <summary>Chave estável do motor, minúscula: <c>"hubdodesenvolvedor"</c>.</summary>
    public string Motor { get; set; } = string.Empty;

    /// <summary>Motor habilitado e participante da cadeia de fallback.</summary>
    public bool Ativo { get; set; } = true;

    /// <summary>Posição na cadeia de fallback (menor primeiro).</summary>
    public int Ordem { get; set; }

    /// <summary>Token/chave de API do motor, cifrado. Write-only na API.</summary>
    public string? TokenCifrado { get; set; }

    /// <summary>Timeout por tentativa, em segundos.</summary>
    public int TimeoutSegundos { get; set; } = 10;

    /// <summary>Tentativas neste motor antes de passar ao próximo.</summary>
    public int Tentativas { get; set; } = 3;

    /// <summary>Parâmetros extras não-sensíveis em JSON (ex.: <c>baseUrl</c> override). Públicos.</summary>
    public string? ParametrosJson { get; set; }

    public DateTime CriadoEm { get; set; }
    public Guid? CriadoPor { get; set; }
    public DateTime? AtualizadoEm { get; set; }
    public Guid? AtualizadoPor { get; set; }
}
