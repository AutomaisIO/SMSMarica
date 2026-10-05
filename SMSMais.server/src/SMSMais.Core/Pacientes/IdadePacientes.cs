using SMSMais.Core.Common.Tempo;
using SMSMais.Core.Pacientes.Fhir;

namespace SMSMais.Core.Pacientes;

/// <param name="IdadeMeses">
/// Idade em meses completos, hoje em Brasília. Nulo = paciente sem data de nascimento no cadastro
/// (ou não encontrado). Vai em meses, e não em anos, para a tela poder mostrar o bebê ("8m") sem
/// pedir a data de nascimento.
/// </param>
public sealed record IdadePacienteDto(Guid Id, int? IdadeMeses);

public sealed record IdadesPacientesRequest(IReadOnlyList<Guid> Ids);

/// <summary>
/// A idade que acompanha o nome do paciente em toda tela ("54a").
///
/// <para><b>Por que um endpoint próprio, e não a data de nascimento em cada DTO:</b> o nome do
/// paciente aparece em dezenas de listas, cada uma com o seu DTO, e o paciente vive no hub FHIR.
/// Pôr a data em todas seria mexer em cada serviço — e esquecer um. A tela junta os ids que
/// aparecem nela e pede de uma vez; o hub responde em lote (<c>_id=a,b,c</c>).</para>
///
/// <para><b>Devolve a idade, não a data de nascimento.</b> Quem vê o nome do paciente numa fila
/// não precisa do dia em que ele nasceu — e a data, junto com o nome, é dado de identificação.
/// Por isso basta estar autenticado: não exige o módulo Pacientes.</para>
/// </summary>
public interface IIdadePacientesService
{
    Task<IReadOnlyList<IdadePacienteDto>> ObterAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
}

public sealed class IdadePacientesService(IPacienteResolver resolver) : IIdadePacientesService
{
    /// <summary>Teto por chamada. A tela pede em lotes; mais que isso é uso que não é de tela.</summary>
    public const int MaximoPorChamada = 200;

    public async Task<IReadOnlyList<IdadePacienteDto>> ObterAsync(
        IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var distintos = (ids ?? []).Where(i => i != Guid.Empty).Distinct().Take(MaximoPorChamada).ToArray();
        if (distintos.Length == 0) return [];

        var resumos = await resolver.ResolverManyAsync(distintos, ct);
        var hoje = FusoBrasilia.HojeEmBrasilia();

        return [.. distintos.Select(id => new IdadePacienteDto(
            id,
            resumos.TryGetValue(id, out var r) && r.DataNascimento is { } nasc ? IdadeEmMeses(nasc, hoje) : null))];
    }

    /// <summary>Meses completos entre o nascimento e hoje. Nascimento no futuro (cadastro errado) = nulo.</summary>
    public static int? IdadeEmMeses(DateOnly nascimento, DateOnly hoje)
    {
        if (nascimento > hoje) return null;
        var meses = (hoje.Year - nascimento.Year) * 12 + (hoje.Month - nascimento.Month);
        if (hoje.Day < nascimento.Day) meses--;
        return Math.Max(0, meses);
    }
}
