namespace SMSMais.Data.Entities.Enums;

/// <summary>
/// Conjunto de ações liberadas para um módulo. Flags combináveis num único int.
/// </summary>
[Flags]
public enum AcoesPermissao
{
    Nenhuma = 0,
    Consulta = 1,
    Inclusao = 2,
    Edicao = 4,
    Exclusao = 8,
    Todas = Consulta | Inclusao | Edicao | Exclusao,
}
