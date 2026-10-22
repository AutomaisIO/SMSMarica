using System.Text.RegularExpressions;

using SMSMais.Core.Regulacao.Catalogo;

namespace SMSMais.Core.Regulacao.Conferencia;

/// <summary>O pedido como NÓS o montamos, já no vocabulário da conferência.</summary>
/// <param name="ProcedimentoRotulos">O nome canônico e os rótulos do procedimento NAQUELE sistema
/// (as origens do catálogo) — qualquer um deles casando é o mesmo procedimento.</param>
/// <param name="ProcedimentoCodigos">Os códigos das origens naquele sistema (SISREG: o código interno).</param>
/// <param name="UnidadeNome">A unidade em cujo nome o pedido entra: no NAR, a "em nome de"; no
/// Interno, a solicitante; no Externo, nenhuma (lá fora o solicitante é o município).</param>
public sealed record NossoPedido(
    string PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,
    IReadOnlyList<string> ProcedimentoRotulos,
    IReadOnlyList<string> ProcedimentoCodigos,
    string? Cid,
    string? UnidadeNome,
    string? UnidadeCnes,
    string? MedicoNome,
    string? MedicoCpf,
    string? Risco);

public enum ResultadoItemConferencia
{
    Igual = 1,

    /// <summary>Os dois lados têm valor e não batem. É o que conta como divergência.</summary>
    Diferente = 2,

    /// <summary>Só o sistema tem o dado (nós não pedimos, ou ainda não tínhamos).</summary>
    SoNoSistema = 3,

    /// <summary>Lado a lado, sem veredito: vocabulários diferentes (o risco) ou dado só de lá (a situação).</summary>
    Informativo = 4,
}

public sealed record ItemConferenciaDto(
    string Campo, string Rotulo, string? Nosso, string? NoSistema, ResultadoItemConferencia Resultado);

/// <summary>
/// Compara o pedido nosso com o que o sistema mostra, campo a campo. <b>Puro</b> — a régua se testa
/// sem banco e sem sistema externo.
///
/// <para><b>Só compara o que o sistema mostra.</b> Campo que o sistema não exibe (o SER não tem
/// risco na grade) não vira item: "não mostrado" toda vez seria ruído, e ruído esconde a
/// divergência que importa. Nada aqui bloqueia nada — é para o técnico ver.</para>
/// </summary>
public static partial class ComparadorConferencia
{
    public static IReadOnlyList<ItemConferenciaDto> Comparar(NossoPedido nosso, PedidoNoSistema la)
    {
        var itens = new List<ItemConferenciaDto>();

        if (la.Situacao is { } situacao)
        {
            itens.Add(new("situacao", "Situação no sistema", null, situacao, ResultadoItemConferencia.Informativo));
        }

        // Paciente: documento quando os dois lados têm; o nome sempre que lá mostra.
        Documento(itens, "paciente_cns", "CNS do paciente", nosso.PacienteCns, la.PacienteCns);
        Documento(itens, "paciente_cpf", "CPF do paciente", nosso.PacienteCpf, la.PacienteCpf);
        Nome(itens, "paciente_nome", "Nome do paciente", nosso.PacienteNome, la.PacienteNome);

        if (la.Procedimentos.Count > 0)
        {
            var nossoTexto = nosso.ProcedimentoRotulos.FirstOrDefault();
            var laTexto = string.Join("; ", la.Procedimentos.Select(p => p.Codigo is { } c ? $"{p.Nome} ({c})" : p.Nome));
            var rotulos = nosso.ProcedimentoRotulos.Select(IdentidadePorNome.Chave).Where(c => c.Length > 0).ToHashSet();
            var codigos = nosso.ProcedimentoCodigos.Select(Digitos).Where(c => c.Length > 0).ToHashSet();
            var casa = la.Procedimentos.Any(p =>
                rotulos.Contains(IdentidadePorNome.Chave(p.Nome))
                || (p.Codigo is { } codigo && Digitos(codigo) is { Length: > 0 } d && codigos.Contains(d)));
            itens.Add(new("procedimento", "Procedimento", nossoTexto, laTexto,
                casa ? ResultadoItemConferencia.Igual : ResultadoItemConferencia.Diferente));
        }

        if (la.Cid is { } cidLa && !string.IsNullOrWhiteSpace(cidLa))
        {
            var resultado = string.IsNullOrWhiteSpace(nosso.Cid)
                ? ResultadoItemConferencia.SoNoSistema
                : MesmoCid(nosso.Cid, cidLa) ? ResultadoItemConferencia.Igual : ResultadoItemConferencia.Diferente;
            itens.Add(new("cid", "CID", Vazio(nosso.Cid), cidLa, resultado));
        }

        // Unidade: é o que mais importa no NAR — o pedido tem de ter entrado com o CNES da unidade
        // "em nome de", e não com o de quem o lançou.
        if (la.UnidadeSolicitanteCnes is { } cnesLa && !string.IsNullOrWhiteSpace(cnesLa) && nosso.UnidadeCnes is { } cnesNosso)
        {
            itens.Add(new("unidade", "Unidade solicitante",
                $"{nosso.UnidadeNome} (CNES {cnesNosso})",
                la.UnidadeSolicitante is { } u ? $"{u} (CNES {cnesLa})" : $"CNES {cnesLa}",
                Digitos(cnesNosso).PadLeft(7, '0') == Digitos(cnesLa).PadLeft(7, '0')
                    ? ResultadoItemConferencia.Igual
                    : ResultadoItemConferencia.Diferente));
        }
        else if (la.UnidadeSolicitante is { } unidadeLa && nosso.UnidadeNome is { } unidadeNossa)
        {
            Nome(itens, "unidade", "Unidade solicitante", unidadeNossa, unidadeLa);
        }

        // Médico: pelo CPF quando os dois lados têm (nome do médico é escrito de mil jeitos no SISREG).
        if (Digitos(la.MedicoCpf) is { Length: 11 } cpfLa && Digitos(nosso.MedicoCpf) is { Length: 11 } cpfNosso)
        {
            itens.Add(new("medico", "Médico solicitante",
                $"{nosso.MedicoNome} (CPF {cpfNosso})", $"{la.MedicoNome} (CPF {cpfLa})",
                cpfLa == cpfNosso ? ResultadoItemConferencia.Igual : ResultadoItemConferencia.Diferente));
        }
        else if (la.MedicoNome is { } medicoLa)
        {
            Nome(itens, "medico", "Médico solicitante", nosso.MedicoNome, medicoLa);
        }

        // Risco: cada sistema tem a sua escala e as suas palavras — lado a lado, sem veredito.
        if (la.Risco is { } riscoLa && !string.IsNullOrWhiteSpace(riscoLa))
        {
            itens.Add(new("risco", "Classificação de risco", Vazio(nosso.Risco), riscoLa,
                string.IsNullOrWhiteSpace(nosso.Risco) ? ResultadoItemConferencia.SoNoSistema : ResultadoItemConferencia.Informativo));
        }

        if (la.DataSolicitacao is { } data && !string.IsNullOrWhiteSpace(data))
        {
            itens.Add(new("data", "Data da solicitação no sistema", null, data, ResultadoItemConferencia.Informativo));
        }

        return itens;
    }

    private static void Documento(List<ItemConferenciaDto> itens, string campo, string rotulo, string? nosso, string? la)
    {
        var dLa = Digitos(la);
        if (dLa.Length == 0) return;
        var dNosso = Digitos(nosso);
        itens.Add(new(campo, rotulo, Vazio(nosso), la,
            dNosso.Length == 0 ? ResultadoItemConferencia.SoNoSistema
            : dNosso == dLa ? ResultadoItemConferencia.Igual
            : ResultadoItemConferencia.Diferente));
    }

    private static void Nome(List<ItemConferenciaDto> itens, string campo, string rotulo, string? nosso, string? la)
    {
        if (string.IsNullOrWhiteSpace(la)) return;
        itens.Add(new(campo, rotulo, Vazio(nosso), la,
            string.IsNullOrWhiteSpace(nosso) ? ResultadoItemConferencia.SoNoSistema
            : IdentidadePorNome.Chave(nosso) == IdentidadePorNome.Chave(la) ? ResultadoItemConferencia.Igual
            : ResultadoItemConferencia.Diferente));
    }

    /// <summary>
    /// "C50.9 - NEOPLASIA…" × "C509": compara o código. Se um dos lados só tem a categoria (três
    /// caracteres), a categoria basta — o SER e o SISREG aceitam os dois jeitos.
    /// </summary>
    public static bool MesmoCid(string a, string b)
    {
        var ca = CodigoCid(a);
        var cb = CodigoCid(b);
        if (ca is null || cb is null) return IdentidadePorNome.Chave(a) == IdentidadePorNome.Chave(b);
        return ca.Length == 4 && cb.Length == 4 ? ca == cb : ca[..3] == cb[..3];
    }

    private static string? CodigoCid(string texto)
    {
        var m = RegexCid().Match(texto.ToUpperInvariant());
        return m.Success ? m.Groups[1].Value + m.Groups[2].Value + m.Groups[3].Value : null;
    }

    [GeneratedRegex(@"\b([A-Z])\s?(\d{2})\.?(\d)?")]
    private static partial Regex RegexCid();

    private static string Digitos(string? s) => s is null ? string.Empty : new string([.. s.Where(char.IsDigit)]);

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
