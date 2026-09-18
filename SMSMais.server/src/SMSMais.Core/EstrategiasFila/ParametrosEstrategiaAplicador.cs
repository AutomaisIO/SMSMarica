using System.Text.Json;
using SMSMais.Core.EstrategiasFila.Dtos;

namespace SMSMais.Core.EstrategiasFila;

/// <summary>
/// Aplica o que o agente mandou sobre o quadro vigente, <b>respeitando as travas e a realidade</b>.
///
/// <para>É a peça que transforma "trava" em contrato: o modelo recebe a instrução de não mexer no
/// que está travado, mas instrução não é garantia. Aqui, linha travada que vier diferente é
/// <b>rejeitada</b> com mensagem que volta ao modelo como erro de ferramenta — ele corrige ou a
/// rodada falha. E profissional <b>real</b> não ganha dia em que já tem escala de outro
/// procedimento ou unidade (<see cref="LinhaQuadro.OutrasEscalas"/>): pedido do Bernardo em
/// 18/09/2026 — "conferir se esse mesmo médico não cumpre escala de outro procedimento ou em
/// outra unidade antes de propor".</para>
///
/// <para>Formato de entrada (o schema da ferramenta): <c>quadro[]</c> altera linhas existentes
/// por <c>id</c>; <c>novos[]</c> é a lista COMPLETA dos médicos de simulação livres (substitui os
/// anteriores não travados); <c>aproveitamento</c>, <c>entradaSemanal</c> e <c>mutiroes</c> como
/// antes. Campos ausentes mantêm o valor vigente.</para>
/// </summary>
public static class ParametrosEstrategiaAplicador
{
    private const double Tolerancia = 1e-6;
    private static readonly string[] Dias = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    public static (ParametrosEstrategia Resultado, List<string> Violacoes) Aplicar(
        ParametrosEstrategia vigentes, JsonElement entrada)
    {
        var violacoes = new List<string>();
        vigentes = vigentes.Sanear();
        if (entrada.ValueKind != JsonValueKind.Object)
            return (vigentes, violacoes);

        var quadro = vigentes.Quadro.ToDictionary(l => l.Id, StringComparer.Ordinal);
        var unidadesConhecidas = vigentes.Quadro.Select(l => l.Unidade)
            .Concat(vigentes.UnidadesSimuladas)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var unidadesSimuladas = vigentes.UnidadesSimuladas.ToList();

        // ---- linhas existentes ----
        if (entrada.TryGetProperty("quadro", out var q) && q.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in q.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var id = Str(item, "id");
                if (id is null || !quadro.TryGetValue(id, out var linha))
                {
                    violacoes.Add($"quadro: id '{id ?? "?"}' não existe. Use os ids da tabela do cenário.");
                    continue;
                }

                var dias = LerDias(item, "dias", linha.Dias, violacoes, linha.Nome);
                var atend = Num(item, "atendimentosPorTurno") ?? linha.AtendimentosPorTurno;
                var unidade = Str(item, "unidade") ?? linha.Unidade;

                if (linha.Travado)
                {
                    if (!MesmosDias(dias, linha.Dias) || Math.Abs(atend - linha.AtendimentosPorTurno) > Tolerancia
                        || !string.Equals(unidade, linha.Unidade, StringComparison.OrdinalIgnoreCase))
                        violacoes.Add($"{linha.Nome} está TRAVADO: mantenha dias, atendimentos por turno e unidade como estão.");
                    continue;
                }

                if (atend < 0)
                {
                    violacoes.Add($"{linha.Nome}: atendimentos por turno não pode ser negativo.");
                    continue;
                }

                if (!linha.Simulado)
                {
                    var conflitos = dias.Where(d => !linha.Dias.Contains(d) && linha.OutrasEscalas.ContainsKey(d)).ToList();
                    foreach (var d in conflitos)
                        violacoes.Add($"{linha.Nome} já tem escala em {Dias[d]}: {linha.OutrasEscalas[d]}. Não pode ganhar esse dia.");
                    if (conflitos.Count > 0) continue;
                }

                if (!unidadesConhecidas.Contains(unidade, StringComparer.OrdinalIgnoreCase))
                {
                    unidadesConhecidas.Add(unidade);
                    unidadesSimuladas.Add(unidade);
                }

                quadro[id] = linha with { Dias = dias, AtendimentosPorTurno = Math.Round(atend, 2), Unidade = unidade };
            }
        }

        // ---- médicos de simulação (lista completa dos livres) ----
        if (entrada.TryGetProperty("novos", out var nv) && nv.ValueKind == JsonValueKind.Array)
        {
            var travadosSimulados = quadro.Values.Where(l => l.Simulado && l.Travado).ToList();
            var novos = new List<LinhaQuadro>();
            var padraoAtend = vigentes.AtendimentosPorTurnoPadrao();
            var n = 0;
            foreach (var item in nv.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                n++;
                var nome = Str(item, "nome") is { Length: > 0 } s ? s.Trim() : $"Médico simulação {n}";
                var unidade = Str(item, "unidade") is { Length: > 0 } u ? u.Trim()
                    : unidadesConhecidas.FirstOrDefault() ?? "Unidade simulação";
                var dias = LerDias(item, "dias", [], violacoes, nome);
                var atend = Num(item, "atendimentosPorTurno") ?? padraoAtend;
                if (atend < 0) { violacoes.Add($"{nome}: atendimentos por turno não pode ser negativo."); continue; }
                if (!unidadesConhecidas.Contains(unidade, StringComparer.OrdinalIgnoreCase))
                {
                    unidadesConhecidas.Add(unidade);
                    unidadesSimuladas.Add(unidade);
                }
                novos.Add(new LinhaQuadro($"sim-{Guid.NewGuid():N}"[..12], nome, true, null, unidade,
                    dias, Math.Round(atend, 2), false, [], new Dictionary<int, string>()));
            }

            if (!vigentes.PermitirNovosProfissionais && novos.Count > 0)
                violacoes.Add("Não é permitido acrescentar médicos de simulação nesta estratégia (o gestor travou).");
            else if (travadosSimulados.Count + novos.Count > vigentes.MaxNovosProfissionais)
                violacoes.Add($"No máximo {vigentes.MaxNovosProfissionais} médico(s) de simulação (você mandou {travadosSimulados.Count + novos.Count}).");
            else
            {
                foreach (var l in quadro.Values.Where(l => l.Simulado && !l.Travado).ToList()) quadro.Remove(l.Id);
                foreach (var l in novos) quadro[l.Id] = l;
            }
        }

        var r = vigentes with
        {
            Quadro = [.. vigentes.Quadro.Where(l => quadro.ContainsKey(l.Id)).Select(l => quadro[l.Id])
                .Concat(quadro.Values.Where(l => vigentes.Quadro.All(v => v.Id != l.Id)))],
            UnidadesSimuladas = unidadesSimuladas.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
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
                lista.Add(new MutiraoDto(semana.Value, vagas.Value, Str(item, "descricao")));
            }

            if (vigentes.MutiroesTravados && !MesmosMutiroes(vigentes.Mutiroes, lista))
                violacoes.Add("mutiroes está travado: mantenha a lista como está.");
            else
                r = r with { Mutiroes = lista };
        }

        return (r, violacoes);
    }

    // ------------------------------------------------------------------ apoio

    private static IReadOnlyList<int> LerDias(
        JsonElement o, string chave, IReadOnlyList<int> atual, List<string> violacoes, string nome)
    {
        if (!o.TryGetProperty(chave, out var v) || v.ValueKind != JsonValueKind.Array) return atual;
        var dias = new SortedSet<int>();
        foreach (var d in v.EnumerateArray())
        {
            if (d.ValueKind == JsonValueKind.Number && d.TryGetInt32(out var n) && n is >= 0 and <= 6) dias.Add(n);
            else violacoes.Add($"{nome}: dia inválido em `dias` (use 0=dom … 6=sáb).");
        }
        return [.. dias];
    }

    private static bool MesmosDias(IReadOnlyList<int> a, IReadOnlyList<int> b) =>
        a.Distinct().Order().SequenceEqual(b.Distinct().Order());

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

    private static string? Str(JsonElement o, string campo) =>
        o.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? Num(JsonElement o, string campo) =>
        o.TryGetProperty(campo, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d) ? d : null;

    private static int? Int(JsonElement o, string chave) =>
        o.TryGetProperty(chave, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    private static bool MesmosMutiroes(IReadOnlyList<MutiraoDto> a, IReadOnlyList<MutiraoDto> b) =>
        a.Count == b.Count
        && a.OrderBy(x => x.Semana).ThenBy(x => x.Vagas)
            .SequenceEqual(b.OrderBy(x => x.Semana).ThenBy(x => x.Vagas), Comparer.Instancia);

    private sealed class Comparer : IEqualityComparer<MutiraoDto>
    {
        public static readonly Comparer Instancia = new();
        public bool Equals(MutiraoDto? x, MutiraoDto? y) => x?.Semana == y?.Semana && x?.Vagas == y?.Vagas;
        public int GetHashCode(MutiraoDto obj) => HashCode.Combine(obj.Semana, obj.Vagas);
    }
}
