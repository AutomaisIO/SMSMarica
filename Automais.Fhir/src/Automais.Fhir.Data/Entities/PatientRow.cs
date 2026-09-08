namespace Automais.Fhir.Data.Entities;

/// <summary>
/// Armazenamento do recurso FHIR <c>Patient</c>. O recurso completo vive em
/// <see cref="ResourceRow.Content"/> (jsonb); as colunas abaixo são apenas
/// search params extraídos do documento para permitir busca indexada sem
/// abrir o JSON.
/// </summary>
public sealed class PatientRow : ResourceRow
{
    /// <summary>CPF (identifier system https://fhir.saude.gov.br/sid/cpf).</summary>
    public string? Cpf { get; set; }

    /// <summary>CNS <b>oficial</b> — o primeiro identifier do system CNS, ou o marcado como
    /// <c>use=official</c>. É o que representa a pessoa hoje.</summary>
    public string? Cns { get; set; }

    /// <summary>
    /// <b>Todos</b> os CNS do paciente, separados por espaço — o oficial e os anteriores.
    ///
    /// <para><b>Por que a pessoa tem mais de um.</b> O CNS provisório (faixa 898…) é substituído
    /// pelo definitivo quando o cadastro se regulariza, e um mesmo cidadão acumula números ao
    /// longo dos anos e de cadastros feitos em lugares diferentes. Medido na implantação de
    /// 07/09/2026: 3.815 conversões provisório→definitivo e 577 pessoas com dois CNS definitivos,
    /// só entre os pacientes que o SISREG trouxe.</para>
    ///
    /// <para><b>Por que não basta substituir pelo definitivo.</b> Todo o legado — solicitação,
    /// exame, laudo já gravados — aponta para o número antigo. Trocar deixaria esse histórico
    /// órfão: a próxima carga do SISREG procuraria pelo CNS velho e não acharia a pessoa, criando
    /// uma ficha nova. O identificador antigo continua sendo <b>chave de busca válida</b> mesmo
    /// depois de deixar de ser o oficial — é o que o FHIR chama de <c>use=old</c>.</para>
    ///
    /// <para><b>Array, não texto concatenado.</b> O telefone usa string com espaço e busca por
    /// <c>Contains</c>, mas ali a busca é por pedaço do número; aqui é por CNS inteiro. Um
    /// <c>text[]</c> com índice GIN dá igualdade exata e indexada — enquanto <c>LIKE '%…%'</c>
    /// exigiria varredura sequencial em 344 mil linhas a cada consulta, ou a extensão
    /// <c>pg_trgm</c>, que não está instalada neste banco.</para>
    ///
    /// <para>O documento canônico continua sendo o <c>identifier</c> do recurso; isto é projeção
    /// de busca, reconstruída a cada gravação.</para>
    /// </summary>
    public string[]? CnsTodos { get; set; }

    /// <summary>Nome oficial (name[use=official].text), para busca textual.</summary>
    public string? Nome { get; set; }

    /// <summary>
    /// Dígitos dos telefones (Patient.telecom[system=phone]) concatenados por
    /// espaço, para busca por telefone (ex.: "21981979202 2133334444").
    /// </summary>
    public string? Telefone { get; set; }

    /// <summary>Data de nascimento (birthDate).</summary>
    public DateOnly? Nascimento { get; set; }
}
