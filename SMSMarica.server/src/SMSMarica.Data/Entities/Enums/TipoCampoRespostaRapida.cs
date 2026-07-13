namespace SMSMarica.Data.Entities.Enums;

/// <summary>Tipo de uma variável manual de resposta rápida — define o campo do mini-form e a
/// formatação do valor no texto final.</summary>
public enum TipoCampoRespostaRapida
{
    /// <summary>Texto livre.</summary>
    Texto = 1,

    /// <summary>Data: entra como <c>yyyy-MM-dd</c> e sai formatada <c>dd/MM/aaaa</c>.</summary>
    Data = 2,

    /// <summary>Número (inteiro ou decimal); recusa o que não for número.</summary>
    Numero = 3,
}
