namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Canal de publicação de um computador (ADR-0064). É o que separa "publiquei para conferir" de
/// "chegou a todo mundo": versão recém-publicada só vai aos computadores de <see cref="Teste"/>;
/// promovida, vai a todos.
/// </summary>
public enum ExtensaoCanal
{
    /// <summary>Recebe a versão mais nova publicada, promovida ou não.</summary>
    Teste = 1,

    /// <summary>Recebe só o que foi promovido. É o canal de todo computador ao ser autorizado.</summary>
    Prod = 2,
}
