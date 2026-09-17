namespace SMSMais.Core.Regulacao.Notificacoes;

/// <summary>
/// <b>Técnico regulador</b> das notificações do SER e do SERNIT: quem incluiu a solicitação no
/// sistema externo.
///
/// <para><b>De onde vem:</b> do <c>usuario</c> do evento <c>Solicitar</c> MAIS ANTIGO da trilha.
/// Não é usuário do nosso sistema — é o nome livre que o SER/SERNIT grava. Mais antigo porque a
/// mesma solicitação pode ter vários "Solicitar" (em 17/09/2026, 7.276 do SER tinham mais de um):
/// o primeiro é a inclusão, os demais são reenvios.</para>
///
/// <para><b>A chave é o nome em maiúsculas, sem espaço nas pontas</b>: o SER grava em caixa
/// livre ("patricia ferreira da silva") e o SERNIT em maiúsculas — sem normalizar, a mesma
/// pessoa viraria dois técnicos, com duas cores. Grafias diferentes de verdade ("GUEDES" x
/// "GUETES") continuam separadas: corrigir nome alheio por aproximação é pior que mostrar dois.</para>
///
/// <para>Solicitação cujo histórico ainda não foi lido não tem "Solicitar" — cai em
/// <see cref="SemTecnico"/>, que é filtrável como qualquer técnico.</para>
/// </summary>
public static class TecnicoInclusao
{
    /// <summary>Chave do grupo "sem técnico identificado" (histórico não lido ou sem usuário).
    /// Não colide com nome: tem sublinhado, e nome do SER não tem.</summary>
    public const string SemTecnico = "__SEM_TECNICO__";

    /// <summary>Chaves recebidas da tela, limpas e sem repetição. Vazio = sem filtro.</summary>
    public static IReadOnlyList<string> Normalizar(IEnumerable<string>? tecnicos) =>
        tecnicos is null
            ? []
            : [.. tecnicos
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t == SemTecnico ? t : t.Trim().ToUpperInvariant())
                .Distinct()];

    /// <summary>Junta a lista de nomes com as contagens: todo nome aparece (zero incluído) e o
    /// "sem técnico" só entra quando há pendência — como opção vazia seria ruído.</summary>
    public static IReadOnlyList<TecnicoNotificacaoDto> ComPendentes(
        IEnumerable<string> nomes, IEnumerable<(string? Tecnico, int Quantidade)> pendentes)
    {
        var contagem = pendentes.ToDictionary(p => p.Tecnico ?? TecnicoInclusao.SemTecnico, p => p.Quantidade);
        var lista = nomes
            .Union(contagem.Keys.Where(k => k != TecnicoInclusao.SemTecnico))
            .OrderBy(n => n, StringComparer.InvariantCulture)
            .Select(n => new TecnicoNotificacaoDto(n, contagem.GetValueOrDefault(n)))
            .ToList();
        if (contagem.TryGetValue(TecnicoInclusao.SemTecnico, out var sem) && sem > 0)
        {
            lista.Add(new TecnicoNotificacaoDto(TecnicoInclusao.SemTecnico, sem));
        }
        return lista;
    }
}

/// <summary>Um técnico do filtro: a chave (nome normalizado) e quantas notificações pendentes
/// são de solicitações que ele incluiu. Zero é legítimo — o técnico aparece no filtro mesmo sem
/// nada pendente agora, para quem salvou a seleção não ver a opção sumir.</summary>
public sealed record TecnicoNotificacaoDto(string Chave, int Pendentes);
