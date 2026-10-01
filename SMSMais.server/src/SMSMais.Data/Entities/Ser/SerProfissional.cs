namespace SMSMais.Data.Entities.Ser;

/// <summary>
/// Profissional de saúde como está no SER (Cadastro → Profissionais) — <b>espelho à parte</b>,
/// nunca misturado ao nosso cadastro de Médicos (ADR-0065).
///
/// <para>O cadastro do SER é estadual, único por CPF, e de qualidade ruim: CPF quase sempre vazio,
/// homônimos, nomes com pontuação. Por isso fica isolado aqui, exatamente como o SER mostra, e só
/// encosta no nosso médico pela <see cref="MedicoId"/> — que uma PESSOA confirma, nunca a máquina.
/// O espelho alimenta a lista "Médico solicitante" das solicitações e a tela Regulação → SER →
/// Médicos.</para>
///
/// <para><b>O SER não expõe identificador estável</b>: o link "Editar" da pesquisa dá HTTP 500 e o
/// value do combo de médico é índice de view do Seam (medido em 01/10/2026). A chave natural é a
/// <see cref="Chave"/>: o CPF quando existe; senão documento + tipo + nome.</para>
/// </summary>
public class SerProfissional
{
    public Guid Id { get; set; }

    /// <summary>
    /// Chave natural: <c>cpf:{11 dígitos}</c> ou <c>doc:{tipo}:{documento}:{nome normalizado}</c>.
    /// Linhas do SER com a mesma chave viram uma só (o SER tem cadastro duplicado).
    /// </summary>
    public string Chave { get; set; } = string.Empty;

    /// <summary>Nome do jeito que o SER escreve — é por ele que a solicitação escolhe o médico.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Nome sem acento, maiúsculo e com espaços colapsados — para a busca.</summary>
    public string NomeNormalizado { get; set; } = string.Empty;

    /// <summary>CPF só com dígitos, quando o SER tem. O SER não valida o dígito verificador.</summary>
    public string? Cpf { get; set; }

    /// <summary>Documento da pesquisa do SER (na prática, o número do CRM).</summary>
    public string? Documento { get; set; }

    /// <summary>CNS, RG, CRM, CPF, PMM ou RMS — o combo "Tipo de Documento" do SER.</summary>
    public string? TipoDocumento { get; set; }

    /// <summary>A coluna "Ativo" da pesquisa. Inativo não aparece no combo de médico da solicitação.</summary>
    public bool Ativo { get; set; }

    /// <summary>Quantas linhas da pesquisa do SER caíram nesta chave (cadastro duplicado lá).</summary>
    public int Ocorrencias { get; set; } = 1;

    /// <summary>Estava na última leitura completa? Falso = sumiu da pesquisa do SER.</summary>
    public bool PresenteNoSer { get; set; } = true;

    public DateTime PrimeiraLeituraEm { get; set; }
    public DateTime UltimaLeituraEm { get; set; }

    /// <summary>
    /// O nosso médico (Practitioner do FHIR) que uma pessoa confirmou ser este. Sem FK: o
    /// Practitioner mora no serviço FHIR.
    /// </summary>
    public Guid? MedicoId { get; set; }

    /// <summary>Nome do médico ligado, guardado para a tela não depender do FHIR para listar.</summary>
    public string? MedicoNome { get; set; }

    public DateTime? LigadoEm { get; set; }
    public Guid? LigadoPor { get; set; }
}
