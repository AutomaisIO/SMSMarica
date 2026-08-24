using System.Text.Json.Serialization;

namespace SMSMais.Core.Integracoes.Sisreg.Dtos;

/// <summary>
/// Solicitação hospitalar (internação) — índice <c>solicitacao-hospitalar-*</c>
/// (Manual API SISREG v2.1 §5.1). Subset dos campos negociais mais úteis.
/// </summary>
public sealed record SolicitacaoHospitalarSisregDto(
    [property: JsonPropertyName("codigo_solicitacao")] long? CodigoSolicitacao,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("carater")] string? Carater,
    [property: JsonPropertyName("codigo_central_reguladora")] string? CodigoCentralReguladora,
    [property: JsonPropertyName("data_solicitacao")] string? DataSolicitacao,
    [property: JsonPropertyName("data_internacao")] string? DataInternacao,
    [property: JsonPropertyName("data_alta")] string? DataAlta,
    [property: JsonPropertyName("codigo_cid")] string? CodigoCid,
    [property: JsonPropertyName("descricao_cid")] string? DescricaoCid,
    [property: JsonPropertyName("codigo_procedimento")] string? CodigoProcedimento,
    [property: JsonPropertyName("descricao_procedimento")] string? DescricaoProcedimento,
    [property: JsonPropertyName("codigo_classificacao_risco")] int? CodigoClassificacaoRisco,
    [property: JsonPropertyName("cns_usuario")] string? CnsUsuario,
    [property: JsonPropertyName("cpf_usuario")] string? CpfUsuario,
    [property: JsonPropertyName("no_usuario")] string? NomeUsuario,
    [property: JsonPropertyName("no_mae_usuario")] string? NomeMaeUsuario,
    [property: JsonPropertyName("dt_nascimento_usuario")] string? DataNascimentoUsuario,
    [property: JsonPropertyName("sexo_usuario")] string? SexoUsuario,
    [property: JsonPropertyName("telefone")] string? Telefone,
    [property: JsonPropertyName("municipio_paciente_residencia")] string? MunicipioPacienteResidencia,
    [property: JsonPropertyName("codigo_unidade_solicitante")] string? CodigoUnidadeSolicitante,
    [property: JsonPropertyName("nome_unidade_solicitante")] string? NomeUnidadeSolicitante,
    [property: JsonPropertyName("nome_medico_solicitante")] string? NomeMedicoSolicitante,
    [property: JsonPropertyName("cpf_medico_solicitante")] string? CpfMedicoSolicitante,
    [property: JsonPropertyName("codigo_unidade_executante")] string? CodigoUnidadeExecutante,
    [property: JsonPropertyName("nome_unidade_executante")] string? NomeUnidadeExecutante,
    [property: JsonPropertyName("nome_clinica")] string? NomeClinica,
    [property: JsonPropertyName("nome_leito")] string? NomeLeito,
    [property: JsonPropertyName("sintomas")] string? Sintomas,
    [property: JsonPropertyName("justificativa")] string? Justificativa);
