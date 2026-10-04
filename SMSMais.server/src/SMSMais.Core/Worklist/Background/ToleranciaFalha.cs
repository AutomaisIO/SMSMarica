namespace SMSMais.Core.Worklist.Background;

/// <summary>
/// Conta há quanto tempo um motor falha SEM interrupção. Uma passagem boa zera a contagem.
/// Serve para separar "soluço" (restart programado do PACS, rede piscando) de "PACS fora":
/// só a falha que passa da tolerância vira erro — e, pela captura do log, aviso no celular.
/// </summary>
public sealed class ToleranciaFalha(TimeSpan tolerancia)
{
    private DateTime? _falhandoDesde;
    private bool _escalou;

    /// <summary>Desde quando está falhando (null = a última passagem foi boa).</summary>
    public DateTime? FalhandoDesde => _falhandoDesde;

    /// <summary>Registra uma falha. True quando ela já passou da tolerância (deve virar erro).</summary>
    public bool RegistrarFalha(DateTime agora)
    {
        _falhandoDesde ??= agora;
        if (agora - _falhandoDesde.Value < tolerancia) return false;
        _escalou = true;
        return true;
    }

    /// <summary>
    /// Registra uma passagem boa. Devolve quanto tempo durou a falha quando ela tinha chegado a
    /// virar erro (para logar a volta); null quando não havia falha ou ela ficou na tolerância.
    /// </summary>
    public TimeSpan? RegistrarSucesso(DateTime agora)
    {
        var duracao = _escalou && _falhandoDesde is { } desde ? agora - desde : (TimeSpan?)null;
        _falhandoDesde = null;
        _escalou = false;
        return duracao;
    }
}
