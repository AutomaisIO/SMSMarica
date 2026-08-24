using System.Text.Json.Serialization;

namespace SMSMais.Core.Integracoes.Sisreg.Dtos;

/// <summary>
/// Marcação/agendamento ambulatorial — índice <c>marcacao-ambulatorial-*</c>
/// (Manual API SISREG v2.1 §5.3). Usado pelas consultas de novas solicitações,
/// agendadas, atendidas e canceladas/devolvidas. Subset dos campos mais úteis.
/// </summary>
public sealed record MarcacaoAmbulatorialSisregDto(
    [property: JsonPropertyName("codigo_solicitacao")] long? CodigoSolicitacao,
    [property: JsonPropertyName("status_solicitacao")] string? StatusSolicitacao,
    [property: JsonPropertyName("sigla_situacao")] string? SiglaSituacao,
    [property: JsonPropertyName("codigo_central_reguladora")] string? CodigoCentralReguladora,
    [property: JsonPropertyName("data_solicitacao")] string? DataSolicitacao,
    [property: JsonPropertyName("data_aprovacao")] string? DataAprovacao,
    [property: JsonPropertyName("data_confirmacao")] string? DataConfirmacao,
    [property: JsonPropertyName("data_marcacao")] string? DataMarcacao,
    [property: JsonPropertyName("data_desejada")] string? DataDesejada,
    [property: JsonPropertyName("codigo_interno_procedimento")] string? CodigoInternoProcedimento,
    [property: JsonPropertyName("descricao_interna_procedimento")] string? DescricaoInternaProcedimento,
    [property: JsonPropertyName("codigo_classificacao_risco")] int? CodigoClassificacaoRisco,
    [property: JsonPropertyName("codigo_tipo_regulacao")] string? CodigoTipoRegulacao,
    [property: JsonPropertyName("codigo_grupo_procedimento")] string? CodigoGrupoProcedimento,
    [property: JsonPropertyName("nome_grupo_procedimento")] string? NomeGrupoProcedimento,
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
    [property: JsonPropertyName("codigo_unidade_executante")] string? CodigoUnidadeExecutante,
    [property: JsonPropertyName("nome_unidade_executante")] string? NomeUnidadeExecutante,
    [property: JsonPropertyName("nome_profissional_executante")] string? NomeProfissionalExecutante,
    [property: JsonPropertyName("cpf_profissional_executante")] string? CpfProfissionalExecutante,
    [property: JsonPropertyName("marcacao_executada")] int? MarcacaoExecutada,
    [property: JsonPropertyName("st_falta_registrada")] int? FaltaRegistrada);
