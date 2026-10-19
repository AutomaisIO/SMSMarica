using SMSMais.Data.Entities;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Translado.Geracao;

/// <summary>O que a viagem do paciente exige do veículo (condição no cadastro do Atendimento) e quantos
/// acompanhantes vão com ele.</summary>
internal sealed record NecessidadeViagem(
    Guid SessaoId,
    MobilidadeTransporte Mobilidade,
    bool Isolamento,
    bool DificuldadeVeiculoAlto,
    int Acompanhantes);

/// <summary>Onde cada um vai: o paciente e os acompanhantes, na ordem.</summary>
internal sealed record Acomodacao(Assento Paciente, IReadOnlyList<Assento> Acompanhantes);

/// <summary>
/// Quem cabe em qual veículo, pela condição do paciente:
/// <list type="bullet">
/// <item>cadeirante na própria cadeira só vai em assento de cadeirante;</item>
/// <item>maca só em ambulância, uma por veículo (ocupa o lugar da maca: o assento de cadeirante, ou um banco);</item>
/// <item>isolamento é transporte exclusivo: ninguém mais no veículo além dos acompanhantes dele;</item>
/// <item>cada acompanhante leva um banco (o de acompanhante, ou um de passageiro).</item>
/// </list>
/// Dificuldade com veículo alto não bloqueia: só pesa na escolha do veículo.
/// </summary>
internal static class AcomodacaoNoVeiculo
{
    public static bool VeiculoAlto(TipoVeiculo t) =>
        t is TipoVeiculo.Van or TipoVeiculo.MicroOnibus or TipoVeiculo.Onibus or TipoVeiculo.Ambulancia;

    /// <summary>Distribui os assentos <paramref name="livres"/> do veículo entre <paramref name="quem"/>;
    /// nulo quando alguém não cabe. <paramref name="aBordo"/>: quem já está no carro (no resgate), que
    /// conta para o isolamento e para a maca, mas já tem o próprio lugar.</summary>
    public static Dictionary<Guid, Acomodacao>? Acomodar(
        Veiculo v, IReadOnlyList<Assento> livres, IReadOnlyList<NecessidadeViagem> quem,
        IReadOnlyCollection<NecessidadeViagem>? aBordo = null)
    {
        IReadOnlyList<NecessidadeViagem> todos = aBordo is { Count: > 0 } ? [.. aBordo, .. quem] : quem;
        if (todos.Count > 1 && todos.Any(q => q.Isolamento)) return null;
        var macas = todos.Count(q => q.Mobilidade == MobilidadeTransporte.Maca);
        if (macas > (v.Tipo == TipoVeiculo.Ambulancia ? 1 : 0)) return null;

        var usadas = new HashSet<Guid>();
        Assento? Pegar(params TipoAssento[] tipos)
        {
            foreach (var t in tipos)
            {
                var a = livres.FirstOrDefault(x => x.Tipo == t && !usadas.Contains(x.Id));
                if (a is not null)
                {
                    usadas.Add(a.Id);
                    return a;
                }
            }
            return null;
        }

        // Primeiro quem tem lugar certo (cadeira, maca); o banco comum fica para os outros.
        var lugar = new Dictionary<Guid, Assento>();
        foreach (var q in quem.Where(q => q.Mobilidade == MobilidadeTransporte.CadeiranteVeiculoAdaptado))
        {
            if (Pegar(TipoAssento.Cadeirante) is not { } a) return null;
            lugar[q.SessaoId] = a;
        }
        foreach (var q in quem.Where(q => q.Mobilidade == MobilidadeTransporte.Maca))
        {
            if (Pegar(TipoAssento.Cadeirante, TipoAssento.Passageiro) is not { } a) return null;
            lugar[q.SessaoId] = a;
        }
        foreach (var q in quem.Where(q => !lugar.ContainsKey(q.SessaoId)))
        {
            if (Pegar(TipoAssento.Passageiro, TipoAssento.Acompanhante) is not { } a) return null;
            lugar[q.SessaoId] = a;
        }

        var resultado = new Dictionary<Guid, Acomodacao>();
        foreach (var q in quem)
        {
            var acompanhantes = new List<Assento>();
            for (var i = 0; i < q.Acompanhantes; i++)
            {
                if (Pegar(TipoAssento.Acompanhante, TipoAssento.Passageiro) is not { } a) return null;
                acompanhantes.Add(a);
            }
            resultado[q.SessaoId] = new Acomodacao(lugar[q.SessaoId], acompanhantes);
        }
        return resultado;
    }

    /// <summary>Quem escolhe veículo primeiro: maca, isolamento, cadeirante na cadeira, com
    /// acompanhantes e, por último, os demais.</summary>
    public static int Exigencia(NecessidadeViagem q) =>
        q.Mobilidade == MobilidadeTransporte.Maca ? 0
        : q.Isolamento ? 1
        : q.Mobilidade == MobilidadeTransporte.CadeiranteVeiculoAdaptado ? 2
        : q.Acompanhantes > 0 ? 3
        : 4;

    /// <summary>Quanto menor, melhor o veículo para quem abre a rota: a ambulância fica para quem vai
    /// de maca, o adaptado para cadeirante, e quem tem dificuldade com veículo alto vai de carro.</summary>
    public static int Preferencia(Veiculo v, IReadOnlyList<Assento> livres, NecessidadeViagem q)
    {
        var p = 0;
        if (q.Mobilidade != MobilidadeTransporte.Maca && v.Tipo == TipoVeiculo.Ambulancia) p += 4;
        if (q.Mobilidade != MobilidadeTransporte.CadeiranteVeiculoAdaptado && livres.Any(a => a.Tipo == TipoAssento.Cadeirante)) p += 2;
        if (q.DificuldadeVeiculoAlto && VeiculoAlto(v.Tipo)) p += 1;
        return p;
    }

    /// <summary>Por que não houve veículo para o paciente (o que aparece em "não alocados").</summary>
    public static string MotivoSemVeiculo(NecessidadeViagem q) => q.Mobilidade switch
    {
        MobilidadeTransporte.Maca => "Viaja de maca: precisa de ambulância livre",
        MobilidadeTransporte.CadeiranteVeiculoAdaptado =>
            "Cadeirante na própria cadeira: precisa de veículo com assento de cadeirante livre",
        _ when q.Acompanhantes > 0 =>
            $"Sem veículo livre com lugar para o paciente e {(q.Acompanhantes == 1 ? "o acompanhante" : "os 2 acompanhantes")}",
        _ => "Sem veículo/motorista disponível",
    };

    /// <summary>Por que <paramref name="quem"/> não cabe agora no carro (o que o motorista e a central
    /// leem ao tentar marcar).</summary>
    public static string MotivoNaoCabe(
        Veiculo v, IReadOnlyList<Assento> livres, IReadOnlyList<NecessidadeViagem> quem,
        IReadOnlyCollection<NecessidadeViagem> aBordo)
    {
        if (aBordo.Any(q => q.Isolamento))
            return "Há um paciente em isolamento no carro: ninguém mais pode embarcar com ele.";
        if (quem.Any(q => q.Isolamento) && (aBordo.Count > 0 || quem.Count > 1))
            return "Paciente em isolamento só viaja sozinho (com os acompanhantes dele).";
        if (quem.Any(q => q.Mobilidade == MobilidadeTransporte.Maca))
            return v.Tipo == TipoVeiculo.Ambulancia
                ? "A ambulância já leva um paciente de maca."
                : "Paciente de maca só vai de ambulância.";
        var cadeira = quem.Count(q => q.Mobilidade == MobilidadeTransporte.CadeiranteVeiculoAdaptado);
        if (cadeira > livres.Count(a => a.Tipo == TipoAssento.Cadeirante))
            return "Não há assento de cadeirante livre neste carro agora.";
        var pessoas = quem.Sum(q => 1 + q.Acompanhantes);
        return livres.Count == 0
            ? "O carro está cheio agora."
            : $"Não cabe agora: {Pessoas(pessoas)} para {(livres.Count == 1 ? "1 lugar livre" : $"{livres.Count} lugares livres")}.";
    }

    private static string Pessoas(int n) => n == 1 ? "1 pessoa" : $"{n} pessoas";

    /// <summary>Quantos acompanhantes vão: os escolhidos para a viagem; sem lista, um quando o paciente
    /// confirmou que vai acompanhado; nenhum quando disse que não. Nunca acima do limite do atendimento.</summary>
    public static int AcompanhantesDaViagem(bool? esperado, int escolhidos, int limite)
    {
        if (esperado == false) return 0;
        var n = escolhidos > 0 ? escolhidos : (esperado == true ? 1 : 0);
        return Math.Clamp(n, 0, Math.Max(limite, 0));
    }
}
