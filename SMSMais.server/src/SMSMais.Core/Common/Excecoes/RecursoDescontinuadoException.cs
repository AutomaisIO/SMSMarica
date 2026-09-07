namespace SMSMais.Core.Common.Excecoes;

/// <summary>
/// Lançada quando a operação existia e foi desativada de propósito — o recurso foi substituído
/// por outro caminho. Mapeada para HTTP <b>410 Gone</b> pelo middleware da API.
///
/// <para>Não é 404 (o recurso não sumiu por acaso), nem 409 (não há conflito de estado a
/// resolver): é uma porta fechada com endereço novo. Por isso carrega
/// <see cref="Substituto"/> — a tela precisa dizer para onde a pessoa deve ir, senão o operador
/// só vê "não deu" e tenta de novo.</para>
/// </summary>
public sealed class RecursoDescontinuadoException(string codigo, string mensagem, string? substituto = null)
    : Exception(mensagem)
{
    public string Codigo { get; } = codigo;

    /// <summary>Rota do front que substitui o recurso desativado, quando existe.</summary>
    public string? Substituto { get; } = substituto;
}
