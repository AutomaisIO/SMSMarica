namespace SMSMais.Core.ComunicacaoInterna;

/// <summary>
/// Quem está com o painel aberto agora (ADR-0079 §5): online = ao menos uma conexão viva no hub da
/// Comunicação interna — qualquer aba ou a janela solta. Vive em memória, no singleton: uma
/// instância da API por município (ADR-0043) torna isso suficiente; com duas instâncias, precisaria
/// de backplane. Um restart zera tudo e cada aba reaparece ao reconectar.
/// </summary>
public interface IPresencaInterna
{
    /// <summary>Registra a conexão. <c>true</c> quando a pessoa acabou de ficar online (era a primeira).</summary>
    bool Conectou(Guid usuarioId, string connectionId);

    /// <summary>Remove a conexão. <c>true</c> quando a pessoa acabou de ficar offline (era a última).</summary>
    bool Desconectou(Guid usuarioId, string connectionId);

    bool EstaOnline(Guid usuarioId);
}

public sealed class PresencaInterna : IPresencaInterna
{
    private readonly Dictionary<Guid, HashSet<string>> _conexoes = [];
    private readonly Lock _trava = new();

    public bool Conectou(Guid usuarioId, string connectionId)
    {
        lock (_trava)
        {
            if (!_conexoes.TryGetValue(usuarioId, out var conjunto))
            {
                conjunto = [];
                _conexoes[usuarioId] = conjunto;
            }
            return conjunto.Add(connectionId) && conjunto.Count == 1;
        }
    }

    public bool Desconectou(Guid usuarioId, string connectionId)
    {
        lock (_trava)
        {
            if (!_conexoes.TryGetValue(usuarioId, out var conjunto) || !conjunto.Remove(connectionId))
                return false;
            if (conjunto.Count > 0) return false;
            _conexoes.Remove(usuarioId);
            return true;
        }
    }

    public bool EstaOnline(Guid usuarioId)
    {
        lock (_trava) return _conexoes.ContainsKey(usuarioId);
    }
}
