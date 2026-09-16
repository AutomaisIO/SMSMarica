namespace SMSMais.Core.Integracoes.KlinikosWeb;

/// <summary>
/// Uma instância do Klinikos (Eco Sistemas) já resolvida para uso: <c>Slug</c> da
/// <c>IaFonte</c>, URL base, caminho da app e código da unidade. São três <b>servidores e bases
/// distintos</b> (Conde, UPA Maricá, Santa Rita — só sincronizam login e cadastro entre si);
/// o <c>AppRoot</c> difere: Conde serve em <c>/KlinikosNet</c>; UPA e Santa Rita em <c>/UPA24H</c>
/// (medido 16/09/2026). A config completa (URL/usuário/senha/params) vive na <c>IaFonte</c> e é
/// montada por <see cref="KlinikosWebFonteResolver"/>.
/// </summary>
public sealed record KlinikosInstancia(string Slug, Uri BaseUri, string AppRoot, string UnidCodigo);

/// <summary>
/// Uma linha do relatório 407 (Pacientes Registrados no Dia): a base do Encounter + identidade.
/// <c>SpaCodigo</c> já vem normalizado (12 dígitos, zero-pad).
/// </summary>
public sealed record BoletimRegistro(
    string SpaCodigo, string? Prontuario, string? Paciente, string? NascimentoIdade, string? Clinica);

/// <summary>
/// Uma linha do relatório 667 (Nominal por Classificação de Risco): chegada + COR por boletim.
/// A cor vem de cabeçalho de grupo (não é coluna), resolvida no parser.
/// </summary>
public sealed record ClassificacaoRegistro(
    string SpaCodigo, string? Chegada, string? Cor, string? Origem);

/// <summary>
/// Registro da ESPINHA por boletim, resultado do JOIN 407×667 por <c>SpaCodigo</c> — o que a via
/// rápida gravaria como Encounter (chegada, cor, clínica) + identidade (paciente/prontuário).
/// A etapa de escrita FHIR converte isto em recurso via o mapper canônico.
/// </summary>
public sealed record EspinhaRegistro(
    string SpaCodigo, string? Chegada, string? Cor, string? Clinica,
    string? Paciente, string? Prontuario, string? NascimentoIdade, string? Origem,
    bool Em407, bool Em667);

/// <summary>
/// Uma linha do relatório 526 (Atendimentos por Profissional): traz, por boletim, a hora do
/// atendimento e — em sub-linha — o TEXTO do CID (às vezes sem código). É a fonte do CID em
/// lote (o relatório nominal dedicado, o 815, está quebrado no Crystal deles).
/// </summary>
public sealed record AtendimentoRegistro(string SpaCodigo, string? HoraAtendimento, string? CidTexto);

/// <summary>Resultado do de-para de CID: código quando mapeável, sempre o texto original.</summary>
public sealed record CidResolvido(string? Codigo, string Texto, bool Mapeado);

/// <summary>Contagens do dry-run da espinha (sem PII) — o que seria montado, sem gravar nada.</summary>
public sealed record ResumoDryRun(
    string Slug,
    DateOnly Dia,
    int Boletins,
    int ComCor,
    int ComChegada,
    int SoEm407,
    int SoEm667,
    int EmAmbos,
    IReadOnlyDictionary<string, int> PorCor,
    double Segundos);
