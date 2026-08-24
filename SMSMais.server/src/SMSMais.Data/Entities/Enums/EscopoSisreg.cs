namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Escopo da credencial SISREG, que define o nome do índice consultado.
/// Municipal → <c>{tipo}-{uf}-{municipio}</c>; Nacional → <c>{tipo}-nacional</c>
/// (ver Manual de uso da API SISREG v2.1 §3.4).
/// </summary>
public enum EscopoSisreg
{
    /// <summary>Credencial municipal: índice por UF + município.</summary>
    Municipal = 1,

    /// <summary>Credencial federal/nacional: índice com sufixo <c>-nacional</c>.</summary>
    Nacional = 2,
}
