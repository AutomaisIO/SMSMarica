namespace SMSMarica.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Marcação (agendamento) lida do SISREG III por scraping — tela "Agendados pela
/// Regulação" (<c>cons_marcados_reg</c>) enriquecida com a ficha detalhe
/// (<c>EXIBIR_FICHA</c>). Fonte de verdade da importação para <c>SolicitacaoExame</c>.
/// Campos crus (texto do HTML); a normalização/resolução acontece no serviço de importação.
/// </summary>
public sealed record MarcacaoSisreg(
    /// <summary>Código da Solicitação (nº de regulação do SISREG) — chave de idempotência.</summary>
    string CodigoSolicitacao,
    string? CnsPaciente,
    string? NomePaciente,
    /// <summary>Procedimento em TEXTO (ex.: "MAMOGRAFIA BILATERAL").</summary>
    string? ProcedimentoTexto,
    /// <summary>Código SIGTAP (só dígitos, ex.: "0204030030"). Vem direto no TXT — dispensa mapear por texto.</summary>
    string? CodigoSigtap,
    string? CpfMedicoSolicitante,
    string? NomeMedicoSolicitante,
    /// <summary>CRM do solicitante (quase sempre vazio na ficha; derivado depois).</summary>
    string? CrmMedicoSolicitante,
    string? CnesUnidadeSolicitante,
    string? NomeUnidadeSolicitante,
    string? CnesUnidadeExecutante,
    string? NomeUnidadeExecutante,
    /// <summary>Data/hora do atendimento no fuso local (wall-clock), como o SISREG informa.</summary>
    DateTime? DataHoraAtendimento,
    string? Cid,

    // ---- Dados do paciente vindos do TXT (para criar o paciente novo; opcionais) ----
    string? TelefonePaciente = null,
    string? TipoLogradouro = null,
    string? Logradouro = null,
    string? Complemento = null,
    string? Numero = null,
    string? Bairro = null,
    string? Cep = null,
    string? MunicipioResidencia = null,
    string? CodigoIbgeResidencia = null,

    /// <summary>Linha crua do TXT (as-is) que originou esta marcação — proveniência.</summary>
    string? LinhaRaw = null);
