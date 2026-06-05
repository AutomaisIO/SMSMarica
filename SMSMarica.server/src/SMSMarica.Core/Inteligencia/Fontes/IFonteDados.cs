using SMSMarica.Data.Entities.Ia;

namespace SMSMarica.Core.Inteligencia.Fontes;

/// <summary>Resultado tabular de uma execução read-only contra uma fonte.</summary>
public sealed record ResultadoConsulta(
    bool Sucesso,
    IReadOnlyList<string> Colunas,
    IReadOnlyList<IReadOnlyList<object?>> Linhas,
    string? Erro = null)
{
    public static ResultadoConsulta ComErro(string erro) => new(false, [], [], erro);
}

/// <summary>
/// Alvo de dados executável (Salux/Oracle, Postgres, FHIR...). Sempre read-only:
/// implementações devem validar e impedir qualquer escrita, aplicar timeout e cap de linhas.
/// </summary>
public interface IFonteDados
{
    Task<ResultadoConsulta> ExecutarAsync(string sql, CancellationToken cancellationToken = default);
    Task<bool> TestarConexaoAsync(CancellationToken cancellationToken = default);
}

/// <summary>Cria a <see cref="IFonteDados"/> adequada a partir do registro <see cref="IaFonte"/>.</summary>
public interface IFonteDadosFactory
{
    IFonteDados Criar(IaFonte fonte);
}
