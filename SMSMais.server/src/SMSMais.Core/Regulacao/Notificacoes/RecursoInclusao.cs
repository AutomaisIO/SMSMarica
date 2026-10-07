namespace SMSMais.Core.Regulacao.Notificacoes;

/// <summary>
/// <b>Recurso</b> das notificações do SER/SERNIT/ESUS de São Gonçalo: o procedimento/especialidade
/// que a solicitação pede (ex.: <c>CONSULTA EM OFTALMOLOGIA - PEDIATRIA</c>).
///
/// <para><b>De onde vem:</b> da coluna <c>recurso</c> da própria solicitação, como o sistema
/// externo a grava. <b>Não se normaliza</b> (ao contrário do técnico): o valor que a opção devolve
/// é o mesmo que o filtro compara, então a grafia do sistema externo é a chave — igualar casing
/// aqui só desencontraria opção e filtro.</para>
///
/// <para>As opções são o <b>catálogo completo</b> de recursos já vistos (todos os valores
/// distintos, não só os com pendência): quem monta a própria tela precisa poder marcar um recurso
/// que ainda não apareceu na fila, para já estar filtrado quando aparecer. O número ao lado é a
/// pendência de agora (0 é legítimo). Opção marcada que sumiu do catálogo continua filtrável — a
/// tela a reexibe para dar para desmarcar.</para>
/// </summary>
public static class RecursoInclusao
{
    /// <summary>Recursos recebidos da tela, limpos (sem brancos nem vazios) e sem repetição.
    /// Vazio = sem filtro.</summary>
    public static IReadOnlyList<string> Normalizar(IEnumerable<string>? recursos) =>
        recursos is null
            ? []
            : [.. recursos
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct()];
}

/// <summary>Uma opção do filtro por recurso: o nome do recurso, o tipo (Consulta/Exame — serializado
/// como string, usado só para agrupar/colorir na tela) e quantas notificações pendentes são de
/// solicitações desse recurso agora.</summary>
public sealed record RecursoNotificacaoDto(string Recurso, string Tipo, int Pendentes);
