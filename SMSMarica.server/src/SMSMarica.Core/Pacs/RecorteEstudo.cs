namespace SMSMarica.Core.Pacs;

/// <summary>
/// O que sabemos de um estudo do PACS pelo NOSSO lado. <c>null</c> em vez de uma instância disto
/// significa <b>órfão</b>: nenhum pedido casado, logo sem unidade e sem tipo.
/// </summary>
public sealed record ContextoEstudo(Guid UnidadeExecutanteId, Guid? UnidadeSolicitanteId, Guid? TipoExameId);

/// <summary>Resultado da decisão de recorte de UM estudo.</summary>
public enum DecisaoRecorte
{
    /// <summary>Entra na lista.</summary>
    Mostra,

    /// <summary>Fica de fora: pertence a outra unidade, ou o tipo não está marcado.</summary>
    Descarta,

    /// <summary>
    /// Órfão descartado porque há filtro de TIPO ativo. Separado de <see cref="Descarta"/> porque
    /// a tela precisa avisar: sem esse aviso, o exame que acabou de chegar sumiria sem explicação
    /// e ninguém iria associá-lo.
    /// </summary>
    OrfaoOcultoPorTipo,
}

/// <summary>
/// A regra de quem vê o quê na tela de Exames, isolada do transporte para poder ser lida e
/// testada de uma vez. Fica sobre uma dicotomia: <b>ou o estudo é conhecido</b> (tem pedido
/// casado — sabemos unidade e tipo, mesmo que o objeto DICOM tenha sido reescrito e perdido o AE
/// de origem), <b>ou é órfão</b> (flutua para todo mundo até alguém associar — regra de produto).
/// </summary>
public static class RecorteEstudo
{
    /// <param name="conhecido">Contexto do pedido, ou <c>null</c> para órfão.</param>
    /// <param name="restritoPorUnidade">Se o escopo do usuário recorta (falso para acesso global,
    /// job sem usuário ou recorte desligado).</param>
    /// <param name="unidadesVisiveis">Unidades do escopo quando ele recorta.</param>
    /// <param name="tiposMarcados">Tipos marcados no filtro. Vazio = todos.</param>
    public static DecisaoRecorte Decidir(
        ContextoEstudo? conhecido,
        bool restritoPorUnidade,
        IReadOnlyCollection<Guid> unidadesVisiveis,
        IReadOnlyCollection<Guid> tiposMarcados)
    {
        if (tiposMarcados.Count > 0)
        {
            // Filtro de tipo é sobre o que já foi identificado — o órfão não tem tipo a comparar.
            if (conhecido is null) return DecisaoRecorte.OrfaoOcultoPorTipo;
            if (conhecido.TipoExameId is not { } tipo || !tiposMarcados.Contains(tipo))
                return DecisaoRecorte.Descarta;
        }

        if (!restritoPorUnidade) return DecisaoRecorte.Mostra;

        // Órfão passa: sem pedido casado não há unidade a comparar, e escondê-lo deixaria o exame
        // recém-chegado invisível justamente para quem precisa associá-lo.
        if (conhecido is null) return DecisaoRecorte.Mostra;

        var visivel = unidadesVisiveis.Contains(conhecido.UnidadeExecutanteId)
            || (conhecido.UnidadeSolicitanteId is { } solicitante && unidadesVisiveis.Contains(solicitante));

        return visivel ? DecisaoRecorte.Mostra : DecisaoRecorte.Descarta;
    }
}
