using System.Globalization;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.Pep.Divergencias;

/// <summary>Tri-estado de uma consulta oficial de CPF para um par (CPF, nascimento).</summary>
public enum ResultadoConsultaCpf
{
    /// <summary>O par confere na base oficial.</summary>
    Validou = 1,

    /// <summary>Negativa autoritativa: o par não confere.</summary>
    Negou = 2,

    /// <summary>Ninguém respondeu (motor fora do ar, sem saldo, sem motor ativo).</summary>
    Indisponivel = 3,
}

/// <summary>Veredicto + quantas consultas foram gastas para chegar nele.</summary>
public sealed record ArbitragemIdentidade(
    VeredictoDivergenciaIdentidade Veredicto,
    string? ValorCorreto,
    string Detalhe,
    bool Indisponivel,
    int ConsultasGastas);

/// <summary>
/// Lógica PURA da arbitragem de identidade (ADR-0039) — sem banco, sem HTTP, sem relógio:
/// recebe as duas datas em disputa e um delegate que consulta a base oficial, e devolve o
/// veredicto. Testável de ponta a ponta, no mesmo espírito do <c>DecididorAgendaPep</c>.
///
/// <para>A regra vem da semântica da consulta de CPF: ela <b>só valida quando o par
/// CPF + nascimento confere</b>. Então perguntar pelas duas datas resolve o conflito sem
/// opinião nossa — e, como a data tem que bater exata, duas datas diferentes nunca validam
/// as duas. Por isso, se a da origem valida, a segunda consulta é dispensada (cada consulta
/// custa saldo).</para>
/// </summary>
public static class ArbitroIdentidade
{
    public static async Task<ArbitragemIdentidade> ArbitrarNascimentoAsync(
        string valorOrigem,
        string valorHub,
        Func<DateOnly, Task<(ResultadoConsultaCpf Resultado, string? Detalhe)>> consultar)
    {
        if (!TentarData(valorOrigem, out var dataOrigem) || !TentarData(valorHub, out var dataHub))
        {
            return new ArbitragemIdentidade(
                VeredictoDivergenciaIdentidade.Inconclusivo, null,
                "Valor em disputa não é uma data ISO (yyyy-MM-dd) válida.", false, 0);
        }

        var (rOrigem, dOrigem) = await consultar(dataOrigem);
        if (rOrigem == ResultadoConsultaCpf.Indisponivel)
        {
            return new ArbitragemIdentidade(
                VeredictoDivergenciaIdentidade.Indefinido, null,
                $"Consulta indisponível: {dOrigem}", true, 1);
        }

        if (rOrigem == ResultadoConsultaCpf.Validou)
        {
            // Data exata confere ⇒ a do hub não pode conferir. Poupa a 2ª consulta.
            return new ArbitragemIdentidade(
                VeredictoDivergenciaIdentidade.OrigemCorreta, valorOrigem,
                "Consulta oficial validou a data da origem.", false, 1);
        }

        var (rHub, dHub) = await consultar(dataHub);
        if (rHub == ResultadoConsultaCpf.Indisponivel)
        {
            return new ArbitragemIdentidade(
                VeredictoDivergenciaIdentidade.Indefinido, null,
                $"Consulta indisponível ao conferir o valor do hub: {dHub}", true, 2);
        }

        if (rHub == ResultadoConsultaCpf.Validou)
        {
            return new ArbitragemIdentidade(
                VeredictoDivergenciaIdentidade.HubCorreto, valorHub,
                "Consulta oficial validou a data do hub — a origem NÃO pode sobrescrever.", false, 2);
        }

        return new ArbitragemIdentidade(
            VeredictoDivergenciaIdentidade.AmbosNegados, null,
            $"Nenhuma das duas datas confere com o CPF (origem: {dOrigem}; hub: {dHub}). CPF suspeito.",
            false, 2);
    }

    private static bool TentarData(string? iso, out DateOnly data) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out data);
}
