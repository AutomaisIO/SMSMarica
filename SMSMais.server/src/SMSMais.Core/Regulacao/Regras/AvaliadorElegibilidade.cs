using System.Text.Json;

using SMSMais.Data.Entities.Enums;
using SMSMais.Data.Entities.Regulacao;

namespace SMSMais.Core.Regulacao.Regras;

/// <param name="Nascimento">Sem data, toda regra de idade fica <c>Indefinido</c> — não se chuta idade.</param>
public sealed record PacienteParaRegras(
    DateOnly? Nascimento, string? Sexo, string? Cpf, string? MunicipioIbge);

/// <param name="Laudado">Exame sem laudo serve de anexo, mas a regulação costuma pedir o laudo.</param>
public sealed record ExameParaRegras(
    Guid Id, Guid? TipoExameId, DateOnly RealizadoEm, bool Laudado, string Descricao, Guid? LaudoId);

public sealed record RegraAvaliadaDto(
    Guid RegraId,
    int Versao,
    TipoRegraRegulacao Tipo,
    SeveridadeRegraRegulacao Severidade,
    SistemaRegulacao? Sistema,
    string Descricao,
    ResultadoRegraRegulacao Resultado,
    /// <summary>Por que deu isso — é o texto que a tela mostra ao lado da regra.</summary>
    string? Motivo);

public sealed record PerguntaPendenteDto(
    Guid RegraId, string Pergunta, SistemaRegulacao? Sistema, SeveridadeRegraRegulacao Severidade);

public sealed record DocumentoPendenteDto(
    Guid RegraId,
    string Rotulo,
    bool Obrigatorio,
    Guid? TipoExameId,
    int? ValidadeDias,
    /// <summary>Exames que o próprio SMSMais já tem e servem — quem decide usar é uma pessoa.</summary>
    IReadOnlyList<ExameParaRegras> ExamesInternosCandidatos);

public sealed record AvaliacaoElegibilidadeDto(
    IReadOnlyList<RegraAvaliadaDto> Regras,
    IReadOnlyList<PerguntaPendenteDto> PerguntasPendentes,
    IReadOnlyList<DocumentoPendenteDto> DocumentosPendentes,
    IReadOnlyList<SistemaRegulacao> DestinosPermitidos,
    IReadOnlyList<SistemaRegulacao> DestinosComRessalva,
    IReadOnlyDictionary<SistemaRegulacao, string> MotivosDeBloqueio,
    bool BloqueiaEnvio);

/// <summary>
/// Decide, a partir das regras do manual, se o pedido pode seguir e para onde (plano 03).
///
/// <para><b>Puro e estático.</b> Recebe tudo pronto — paciente, regras, respostas, exames — e não
/// vai a banco nenhum. É o que permite testar a régua clínica em milissegundos e, principalmente,
/// discutir a régua lendo uma função só, em vez de perseguir consultas.</para>
///
/// <para><b>A ressalva de destino é o coração disto.</b> O mesmo procedimento pode ser bloqueado
/// no SER e livre no SERNIT; devolver um único "pode/não pode" perderia justamente a informação
/// que decide para onde o agente manda o caso.</para>
/// </summary>
public static class AvaliadorElegibilidade
{
    public static AvaliacaoElegibilidadeDto Avaliar(
        IReadOnlyList<RegulacaoRegra> regras,
        PacienteParaRegras paciente,
        string? cid,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao> respostas,
        IReadOnlyList<ExameParaRegras> exames,
        IReadOnlyList<SistemaRegulacao> sistemasCandidatos,
        DateOnly hoje,
        NaoSeiViraRegulacao naoSeiPadrao)
    {
        var avaliadas = new List<RegraAvaliadaDto>();
        var perguntas = new List<PerguntaPendenteDto>();
        var documentos = new List<DocumentoPendenteDto>();
        var bloqueados = new Dictionary<SistemaRegulacao, string>();
        var comRessalva = new HashSet<SistemaRegulacao>();
        var pendenciaTrava = false;

        var idade = Idade(paciente.Nascimento, hoje);

        foreach (var regra in regras.OrderBy(r => r.Ordem).ThenBy(r => r.Descricao))
        {
            // Informativa é texto do manual: aparece na tela e não entra em conta nenhuma.
            // Sem esta saída, 83% das regras extraídas virariam pergunta (spike e).
            if (regra.Tipo == TipoRegraRegulacao.Informativa)
            {
                avaliadas.Add(Avaliada(regra, ResultadoRegraRegulacao.Atende, null));
                continue;
            }

            var (resultado, motivo) = regra.Tipo switch
            {
                TipoRegraRegulacao.Dedutivel => AvaliarDedutivel(regra, paciente, idade, cid),
                TipoRegraRegulacao.NaoDedutivel => AvaliarPergunta(regra, respostas, naoSeiPadrao),
                TipoRegraRegulacao.Documental => (ResultadoRegraRegulacao.Indefinido, null),
                _ => (ResultadoRegraRegulacao.Atende, (string?)null),
            };

            avaliadas.Add(Avaliada(regra, resultado, motivo));

            if (regra.Tipo == TipoRegraRegulacao.NaoDedutivel
                && resultado == ResultadoRegraRegulacao.Indefinido
                && !respostas.ContainsKey(regra.Id))
            {
                perguntas.Add(new PerguntaPendenteDto(
                    regra.Id, regra.Pergunta ?? regra.Descricao, regra.Sistema, regra.Severidade));

                // "Não sei" que vira pendência trava o envio; a regra sem resposta nenhuma
                // também — mas só quando ela é capaz de bloquear. Pergunta de aviso não segura
                // o pedido da unidade.
                if (regra.Severidade == SeveridadeRegraRegulacao.Bloqueia) pendenciaTrava = true;
            }

            if (regra.Tipo == TipoRegraRegulacao.Documental)
            {
                documentos.Add(new DocumentoPendenteDto(
                    regra.Id,
                    regra.DocumentoRotulo ?? regra.Descricao,
                    regra.Obrigatorio,
                    regra.TipoExameId,
                    regra.ValidadeDias,
                    CandidatosInternos(regra, exames, hoje)));
                continue;
            }

            if (resultado is ResultadoRegraRegulacao.Atende or ResultadoRegraRegulacao.Indefinido)
            {
                continue;
            }

            // Regra sem sistema vale para todos os candidatos; com sistema, só para aquele.
            var alvos = regra.Sistema is { } s ? [s] : sistemasCandidatos;
            foreach (var alvo in alvos)
            {
                if (resultado == ResultadoRegraRegulacao.Bloqueia)
                {
                    // Guarda o PRIMEIRO motivo: é o que a tela mostra, e o primeiro da ordem é
                    // o mais específico (as regras vêm ordenadas por `Ordem`).
                    bloqueados.TryAdd(alvo, motivo ?? regra.Descricao);
                }
                else if (resultado == ResultadoRegraRegulacao.Ressalva)
                {
                    comRessalva.Add(alvo);
                }
            }
        }

        var permitidos = sistemasCandidatos.Where(s => !bloqueados.ContainsKey(s)).ToList();

        return new AvaliacaoElegibilidadeDto(
            avaliadas,
            perguntas,
            documentos,
            permitidos,
            [.. comRessalva.Where(permitidos.Contains)],
            bloqueados,
            // Sem destino sobrando, ou com pergunta bloqueante sem resposta, o pedido não sai.
            BloqueiaEnvio: permitidos.Count == 0 || pendenciaTrava);
    }

    private static RegraAvaliadaDto Avaliada(
        RegulacaoRegra r, ResultadoRegraRegulacao resultado, string? motivo) =>
        new(r.Id, r.Versao, r.Tipo, r.Severidade, r.Sistema, r.Descricao, resultado, motivo);

    private static (ResultadoRegraRegulacao, string?) AvaliarDedutivel(
        RegulacaoRegra r, PacienteParaRegras p, int? idade, string? cid)
    {
        if (r.IdadeMinAnos is not null || r.IdadeMaxAnos is not null)
        {
            // Sem nascimento não se chuta idade: fica indefinido e alguém confere.
            if (idade is null) return (ResultadoRegraRegulacao.Indefinido, "Paciente sem data de nascimento.");

            if (r.IdadeMinAnos is { } min && idade < min)
            {
                return (Falha(r), $"Idade {idade} anos, mínimo {min}.");
            }
            if (r.IdadeMaxAnos is { } max && idade > max)
            {
                return (Falha(r), $"Idade {idade} anos, máximo {max}.");
            }
        }

        if (!string.IsNullOrWhiteSpace(r.Sexo))
        {
            if (string.IsNullOrWhiteSpace(p.Sexo)) return (ResultadoRegraRegulacao.Indefinido, "Paciente sem sexo no cadastro.");
            if (!string.Equals(p.Sexo, r.Sexo, StringComparison.OrdinalIgnoreCase))
            {
                return (Falha(r), $"Exigido sexo {r.Sexo}.");
            }
        }

        if (r.ExigeCpf && string.IsNullOrWhiteSpace(p.Cpf))
        {
            return (Falha(r), "Paciente sem CPF.");
        }

        var permitidos = Lista(r.CidsPermitidosJson);
        var excluidos = Lista(r.CidsExcluidosJson);

        if (permitidos.Count > 0 || excluidos.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(cid)) return (ResultadoRegraRegulacao.Indefinido, "Hipótese sem CID.");

            var codigo = cid.Trim().ToUpperInvariant();

            // Prefixo, e não igualdade: o manual fala em "C50" e a hipótese vem "C50.4".
            if (excluidos.Any(e => codigo.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
            {
                return (Falha(r), $"CID {codigo} está entre os excluídos.");
            }
            if (permitidos.Count > 0
                && !permitidos.Any(e => codigo.StartsWith(e, StringComparison.OrdinalIgnoreCase)))
            {
                return (Falha(r), $"CID {codigo} não está entre os aceitos.");
            }
        }

        return (ResultadoRegraRegulacao.Atende, null);
    }

    private static (ResultadoRegraRegulacao, string?) AvaliarPergunta(
        RegulacaoRegra r,
        IReadOnlyDictionary<Guid, RespostaRegraRegulacao> respostas,
        NaoSeiViraRegulacao naoSeiPadrao)
    {
        if (!respostas.TryGetValue(r.Id, out var resposta))
        {
            return (ResultadoRegraRegulacao.Indefinido, null);
        }

        if (resposta == RespostaRegraRegulacao.NaoSei)
        {
            var destino = r.NaoSeiVira ?? naoSeiPadrao;
            return destino == NaoSeiViraRegulacao.Ressalva
                ? (ResultadoRegraRegulacao.Ressalva, "Respondido \"não sei\".")
                : (ResultadoRegraRegulacao.Indefinido, "Respondido \"não sei\" — precisa ser esclarecido.");
        }

        return resposta == r.RespostaBloqueia
            ? (Falha(r), $"Respondido \"{resposta}\".")
            : (ResultadoRegraRegulacao.Atende, null);
    }

    /// <summary>O que a severidade faz quando a regra não é atendida.</summary>
    private static ResultadoRegraRegulacao Falha(RegulacaoRegra r) => r.Severidade switch
    {
        SeveridadeRegraRegulacao.Bloqueia => ResultadoRegraRegulacao.Bloqueia,
        SeveridadeRegraRegulacao.Ressalva => ResultadoRegraRegulacao.Ressalva,
        _ => ResultadoRegraRegulacao.Atende,
    };

    /// <summary>
    /// Exames nossos que servem para aquela exigência. <b>Não resolve a caixinha</b> — só oferece:
    /// quem confirma que aquele exame é o pedido é uma pessoa.
    /// </summary>
    private static IReadOnlyList<ExameParaRegras> CandidatosInternos(
        RegulacaoRegra r, IReadOnlyList<ExameParaRegras> exames, DateOnly hoje)
    {
        if (r.TipoExameId is not { } tipo) return [];

        var limite = r.ValidadeDias is { } dias ? hoje.AddDays(-dias) : DateOnly.MinValue;

        return [.. exames
            .Where(e => e.TipoExameId == tipo && e.RealizadoEm >= limite)
            // Mais recente primeiro, e laudado antes: é o que a regulação costuma aceitar.
            .OrderByDescending(e => e.Laudado).ThenByDescending(e => e.RealizadoEm)];
    }

    private static int? Idade(DateOnly? nascimento, DateOnly hoje)
    {
        if (nascimento is not { } n) return null;
        var anos = hoje.Year - n.Year;
        if (hoje < n.AddYears(anos)) anos--;
        return anos;
    }

    private static List<string> Lista(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            // Regra com JSON torto não pode derrubar a avaliação inteira: ela deixa de restringir.
            return [];
        }
    }
}
