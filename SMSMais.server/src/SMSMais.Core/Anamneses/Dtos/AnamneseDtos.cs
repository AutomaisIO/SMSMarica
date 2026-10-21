namespace SMSMais.Core.Anamneses.Dtos;

/// <summary>Anamnese persistida (conteúdo JSON versionado por tipo de questionário).</summary>
public sealed record AnamneseDto(
    Guid Id,
    Guid SolicitacaoExameId,
    string Tipo,
    int Versao,
    string ConteudoJson,
    string? ClassificacaoRisco,
    string? PreenchidoPorNome,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    /// <summary>O que aconteceu com a worklist ao salvar (Holter): enviado, atualizado, aguardando a
    /// recepção. Null = nada a dizer (mamografia).</summary>
    string? AvisoWorklist = null);

/// <summary>
/// Contexto para abrir a tela de anamnese: identifica o pedido + paciente
/// (seção 1 do questionário, somente leitura) e traz a anamnese existente
/// (null = ainda não preenchida).
/// </summary>
public sealed record AnamneseContextoDto(
    Guid SolicitacaoExameId,
    string AccessionNumber,
    string TipoExameNome,
    string ModalidadeDicom,
    Guid PacienteId,
    string PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,
    DateOnly? PacienteNascimento,
    AnamneseDto? Anamnese,
    /// <summary>Protocolo da requisição no SISCAN, quando já foi gerada. Null = ainda não.</summary>
    string? SiscanProtocolo = null,
    /// <summary>Nº do exame no SISCAN — o outro número, que abre o resultado lá.</summary>
    string? SiscanNumeroExame = null,
    /// <summary>"Diagnóstico inicial" do pedido = CID-10 do SISREG. A médica o lê no cabeçalho para
    /// saber o tipo de laudo (ticket #155). Null = o pedido não trouxe CID.</summary>
    string? CidCodigo = null,
    /// <summary>Descrição do CID por extenso (catálogo canônico); null = mostra só o código.</summary>
    string? CidDescricao = null,
    /// <summary>Qual questionário a tela abre: "mamografia" ou "holter" (pela modalidade do tipo de
    /// exame — <see cref="AnamneseHolter.QuestionarioDa"/>).</summary>
    string Questionario = "mamografia");

/// <summary>Payload de criação/edição (upsert) da anamnese de uma solicitação.</summary>
public sealed record SalvarAnamneseDto(
    string Tipo,
    int Versao,
    string ConteudoJson,
    string? ClassificacaoRisco);
