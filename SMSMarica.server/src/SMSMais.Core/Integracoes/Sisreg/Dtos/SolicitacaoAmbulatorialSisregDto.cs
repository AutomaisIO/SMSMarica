using System.Text.Json.Serialization;

namespace SMSMais.Core.Integracoes.Sisreg.Dtos;

/// <summary>
/// Solicitação ambulatorial (fila) — índice <c>solicitacao-ambulatorial-*</c>
/// (Manual API SISREG v2.1 §5.2). Subset dos campos negociais mais úteis.
/// </summary>
public sealed record SolicitacaoAmbulatorialSisregDto(
    [property: JsonPropertyName("codigo_solicitacao")] long? CodigoSolicitacao,
    [property: JsonPropertyName("status_solicitacao")] string? StatusSolicitacao,
    [property: JsonPropertyName("sigla_situacao")] string? SiglaSituacao,
    [property: JsonPropertyName("codigo_central_reguladora")] string? CodigoCentralReguladora,
    [property: JsonPropertyName("data_solicitacao")] string? DataSolicitacao,
    [property: JsonPropertyName("data_desejada")] string? DataDesejada,
    [property: JsonPropertyName("codigo_cid_solicitado")] string? CodigoCidSolicitado,
    [property: JsonPropertyName("descricao_cid_solicitado")] string? DescricaoCidSolicitado,
    [property: JsonPropertyName("codigo_classificacao_risco")] int? CodigoClassificacaoRisco,
    [property: JsonPropertyName("codigo_tipo_regulacao")] string? CodigoTipoRegulacao,
    [property: JsonPropertyName("codigo_tipo_fila")] int? CodigoTipoFila,
    [property: JsonPropertyName("codigo_tipo_vaga_solicitada")] int? CodigoTipoVagaSolicitada,
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
    [property: JsonPropertyName("cpf_profissional_solicitante")] string? CpfProfissionalSolicitante,
    [property: JsonPropertyName("codigo_grupo_procedimento")] string? CodigoGrupoProcedimento,
    [property: JsonPropertyName("nome_grupo_procedimento")] string? NomeGrupoProcedimento,
    [property: JsonPropertyName("codigo_unidade_desejada")] string? CodigoUnidadeDesejada,
    [property: JsonPropertyName("nome_unidade_desejada")] string? NomeUnidadeDesejada);
