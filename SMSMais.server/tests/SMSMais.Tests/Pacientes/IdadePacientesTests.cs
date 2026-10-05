using SMSMais.Core.Pacientes;

namespace SMSMais.Tests.Pacientes;

/// <summary>
/// A idade ao lado do nome ("54a", "8m") é em meses COMPLETOS: quem faz aniversário amanhã ainda
/// tem a idade de hoje. Errar por um dia aqui é o tipo de coisa que a recepção percebe na hora.
/// </summary>
public sealed class IdadePacientesTests
{
    [Theory]
    [InlineData("1972-10-05", "2026-10-05", 648)] // aniversário hoje: 54 anos cheios
    [InlineData("1972-10-06", "2026-10-05", 647)] // aniversário amanhã: ainda 53
    [InlineData("2026-02-10", "2026-10-05", 7)]   // bebê: 7 meses (dia 10 ainda não chegou em outubro)
    [InlineData("2026-10-01", "2026-10-05", 0)]   // recém-nascido
    [InlineData("2024-02-29", "2025-02-28", 11)]  // bissexto: 28/02 ainda não fecha o mês do dia 29
    public void Idade_em_meses_completos(string nascimento, string hoje, int esperado) =>
        Assert.Equal(
            esperado,
            IdadePacientesService.IdadeEmMeses(DateOnly.Parse(nascimento), DateOnly.Parse(hoje)));

    [Fact]
    public void Nascimento_no_futuro_nao_tem_idade() =>
        Assert.Null(IdadePacientesService.IdadeEmMeses(new DateOnly(2027, 1, 1), new DateOnly(2026, 10, 5)));
}
