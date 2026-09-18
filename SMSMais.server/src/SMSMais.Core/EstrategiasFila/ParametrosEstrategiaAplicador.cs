using System.Text.Json;
using SMSMais.Core.EstrategiasFila.Dtos;

namespace SMSMais.Core.EstrategiasFila;

/// <summary>
/// Aplica os números que o agente mandou sobre os parâmetros vigentes, <b>respeitando as travas</b>.
///
/// <para>É a peça que transforma "trava" em contrato: o modelo recebe a instrução de não mexer no
/// que está travado, mas instrução não é garantia. Aqui, parâmetro travado que vier diferente é
/// <b>rejeitado</b> com mensagem que volta ao modelo como erro de ferramenta — ele corrige ou a
/// rodada falha. Parâmetro livre fora de <c>Min..Max</c> também é rejeitado.</para>
/// </summary>
public static class ParametrosEstrategiaAplicador
{
    private const double Tolerancia = 1e-6;

    /// <summary>
    /// Devolve os parâmetros resultantes ou a lista de violações (vazia = tudo certo).
    /// Campos ausentes no JSON mantêm o valor vigente.
    /// </summary>
    public static (ParametrosEstrategia Resultado, List<string> Violacoes) Aplicar(
        ParametrosEstrategia vigentes, JsonElement entrada)
    {
        var violacoes = new List<string>();
        if (entrada.ValueKind != JsonValueKind.Object)
            return (vigentes, violacoes);

        var r = vigentes with
        {
            Unidades = Numero(vigentes.Unidades, "unidades", entrada, violacoes),
            Profissionais = Numero(vigentes.Profissionais, "profissionais", entrada, violacoes),
            DiasPorSemana = Numero(vigentes.DiasPorSemana, "diasPorSemana", entrada, violacoes),
            HorasPorDia = Numero(vigentes.HorasPorDia, "horasPorDia", entrada, violacoes),
            AtendimentosPorHora = Numero(vigentes.AtendimentosPorHora, "atendimentosPorHora", entrada, violacoes),
            Aproveitamento = Numero(vigentes.Aproveitamento, "aproveitamento", entrada, violacoes),
            EntradaSemanal = Numero(vigentes.EntradaSemanal, "entradaSemanal", entrada, violacoes),
        };

        if (entrada.TryGetProperty("mutiroes", out var m) && m.ValueKind == JsonValueKind.Array)
        {
            var lista = new List<MutiraoDto>();
            foreach (var item in m.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var semana = Int(item, "semana");
                var vagas = Int(item, "vagas");
                if (semana is null || vagas is null || semana < 1 || vagas < 1) continue;
                var descricao = item.TryGetProperty("descricao", out var d) && d.ValueKind == JsonValueKind.String
                    ? d.GetString() : null;
                lista.Add(new MutiraoDto(semana.Value, vagas.Value, descricao));
            }

            if (vigentes.MutiroesTravados && !MesmosMutiroes(vigentes.Mutiroes, lista))
                violacoes.Add("mutiroes está travado: mantenha a lista como está.");
            else
                r = r with { Mutiroes = lista };
        }

        return (r, violacoes);
    }

    private static ParametroNumero Numero(
        ParametroNumero atual, string chave, JsonElement o, List<string> violacoes)
    {
        if (!o.TryGetProperty(chave, out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetDouble(out var valor))
            return atual;

        if (atual.Travado)
        {
            if (Math.Abs(valor - atual.Valor) > Tolerancia)
                violacoes.Add($"{chave} está travado em {atual.Valor:0.###} e veio {valor:0.###}.");
            return atual;
        }

        if (double.IsNaN(valor) || double.IsInfinity(valor))
        {
            violacoes.Add($"{chave} não é um número válido.");
            return atual;
        }

        if (atual.Min is { } min && valor < min - Tolerancia)
        {
            violacoes.Add($"{chave} abaixo do mínimo permitido ({min:0.###}); veio {valor:0.###}.");
            return atual;
        }

        if (atual.Max is { } max && valor > max + Tolerancia)
        {
            violacoes.Add($"{chave} acima do máximo permitido ({max:0.###}); veio {valor:0.###}.");
            return atual;
        }

        return atual.Com(Math.Round(valor, 3));
    }

    private static int? Int(JsonElement o, string chave) =>
        o.TryGetProperty(chave, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    private static bool MesmosMutiroes(IReadOnlyList<MutiraoDto> a, IReadOnlyList<MutiraoDto> b) =>
        a.Count == b.Count
        && a.OrderBy(x => x.Semana).ThenBy(x => x.Vagas)
            .SequenceEqual(b.OrderBy(x => x.Semana).ThenBy(x => x.Vagas),
                Comparer.Instancia);

    private sealed class Comparer : IEqualityComparer<MutiraoDto>
    {
        public static readonly Comparer Instancia = new();
        public bool Equals(MutiraoDto? x, MutiraoDto? y) => x?.Semana == y?.Semana && x?.Vagas == y?.Vagas;
        public int GetHashCode(MutiraoDto obj) => HashCode.Combine(obj.Semana, obj.Vagas);
    }
}
