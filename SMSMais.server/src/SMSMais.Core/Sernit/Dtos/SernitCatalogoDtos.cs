using SMSMais.Data.Entities.Sernit;

namespace SMSMais.Core.Sernit.Dtos;

public sealed record SernitOpcaoDto(string Valor, string Rotulo);

/// <summary>Um campo do bloco dinâmico (muda por recurso).</summary>
public sealed record SernitCampoDinamicoDto(
    string Numero,
    string Campo,
    string Rotulo,
    /// <summary><c>text</c>, <c>textarea</c>, <c>select</c>, <c>radio</c> ou <c>checkbox</c>.</summary>
    string Tipo,
    bool Obrigatorio,
    IReadOnlyList<SernitOpcaoDto>? Opcoes);

/// <summary>Uma linha do autocomplete de CID da Hipótese.</summary>
/// <param name="Codigo">código sem ponto (<c>A09</c>, <c>E119</c>).</param>
/// <param name="Descricao">texto do CID, com os acentos/faltas do próprio SERNIT.</param>
/// <param name="Texto">o que o navegador escreve ao clicar (<c>(A09 ) Diarréia…</c>) — é o que o
/// SERNIT espera de volta em <c>form0:procedimento</c>.</param>
public sealed record SernitCidDto(string Codigo, string Descricao, string Texto);

public sealed record SernitCidSugestoesDto(
    IReadOnlyList<SernitCidDto> Itens,
    /// <summary>O SERNIT cortou a lista no teto — há mais CID que casam o termo.</summary>
    bool Truncado);

/// <summary>A "impressão digital" da lista de CID de um recurso (contagens de sondagem).</summary>
public sealed record SernitAssinaturaCidDto(string Tipo, string Recurso, string Assinatura);

/// <summary>Bloco fixo do formulário de nova solicitação, lido AO VIVO. Sem "ambulatório estadual".</summary>
public sealed record SernitFormularioNovaDto(
    IReadOnlyList<SernitOpcaoDto> Tipos,
    IReadOnlyList<SernitOpcaoDto> ClassificacoesRisco,
    IReadOnlyList<SernitOpcaoDto> Medicos,
    IReadOnlyList<SernitCampoDinamicoDto> CamposDinamicosPadrao);

/// <summary>Um campo do cadastro do paciente como o SERNIT devolve na pesquisa por CNS/CPF.</summary>
public sealed record SernitCampoPacienteDto(
    string Campo,
    string Rotulo,
    string? Valor,
    string Tipo,
    bool Obrigatorio,
    bool Editavel,
    IReadOnlyList<SernitOpcaoDto>? Opcoes);

/// <summary>Resultado da pesquisa de paciente no SERNIT (CNS ou CPF).</summary>
public sealed record SernitPacienteEncontradoDto(
    bool Encontrado,
    IReadOnlyList<string> Avisos,
    IReadOnlyList<SernitCampoPacienteDto> Campos,
    Guid? PacienteIdNosso = null,
    string? TelefoneVerificadoNosso = null);

/// <summary>O formulário montado a partir do NOSSO catálogo — sem tocar no SERNIT.</summary>
public sealed record SernitCatalogoFormularioDto(
    IReadOnlyList<SernitOpcaoDto> ClassificacoesRisco,
    IReadOnlyList<SernitOpcaoDto> Medicos,
    IReadOnlyList<SernitCatalogoRecursoDto> Recursos,
    DateTime? SincronizadoEm,
    int RecursosSemCampos,
    int CidsCopiados,
    int RecursosSemCid,
    bool CopiaEmAndamento,
    string? UltimoErro);

public sealed record SernitCatalogoRecursoDto(
    TipoRecursoSernit Tipo, string Valor, string Rotulo, bool CamposLidos);

// ---------------------------------------------------------------- rascunhos

public sealed record SernitRascunhoListaDto(
    Guid Id,
    StatusRascunhoSernit Status,
    TipoRecursoSernit? Tipo,
    string? RecursoRotulo,
    string? PacienteNome,
    string? Cns,
    string? Hipotese,
    string? IdSernitGerado,
    string? CriadoPorNome,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    DateTime? EnviadoEm,
    int Anexos);

public sealed record SernitRascunhoAnexoDto(
    Guid Id,
    Guid MidiaId,
    string NomeArquivo,
    string? ContentType,
    long Tamanho,
    DateTime? EnviadoEm,
    DateTime CriadoEm);

public sealed record SernitRascunhoDetalheDto(
    Guid Id,
    StatusRascunhoSernit Status,
    TipoRecursoSernit? Tipo,
    string? RecursoValor,
    string? RecursoRotulo,
    string? Cns,
    string? PacienteNome,
    string? Hipotese,
    IReadOnlyDictionary<string, string> Campos,
    string? IdSernitGerado,
    string? MensagemErro,
    string? CriadoPorNome,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    DateTime? EnviadoEm,
    IReadOnlyList<SernitRascunhoAnexoDto> Anexos);

public sealed record SernitRascunhoRequest
{
    public TipoRecursoSernit? Tipo { get; init; }
    public string? RecursoValor { get; init; }
    public string? RecursoRotulo { get; init; }
    public string? Cns { get; init; }
    public string? PacienteNome { get; init; }
    public string? Hipotese { get; init; }
    public Dictionary<string, string>? Campos { get; init; }
}
