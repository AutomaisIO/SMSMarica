using SMSMais.Core.SolicitacoesExame.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Consultas.Dtos;

/// <summary>Linha da listagem de consultas (Solicitacao categoria não-imagem). Sem PACS/laudo.</summary>
public sealed record ConsultaListItemDto(
    Guid Id,
    string? CodigoSolicitacao,
    Guid PacienteId,
    string? PacienteNome,
    string Categoria,
    string? Especialidade,
    string UnidadeExecutanteNome,
    string SolicitanteNome,
    DateTime? DataAgendada,
    DateOnly? DataSolicitacao,
    string Status,
    string StatusConfirmacao,
    /// <summary>Estado da confirmação por WhatsApp (mesmo chip da lista de exames). Null = não houve.</summary>
    ComunicacaoChipDto? ChipConfirmacao = null,
    /// <summary>Direção relativa à unidade ativa: Recebida (é a executora) / Enviada (é a solicitante).
    /// Null quando não há unidade de referência única (visão do conjunto/admin sem unidade).</summary>
    DirecaoSolicitacao? Direcao = null);

public sealed record ConsultaDetalheDto(
    Guid Id,
    string? CodigoSolicitacao,
    Guid PacienteId,
    string? PacienteNome,
    string? PacienteCpf,
    string? PacienteCns,
    string Categoria,
    string? Especialidade,
    string? ProcedimentoTexto,
    string? ProcedimentoSigtapCodigo,
    string UnidadeExecutanteNome,
    string? UnidadeSolicitanteNome,
    string SolicitanteNome,
    DateTime? DataAgendada,
    DateOnly? DataSolicitacao,
    DateOnly? DataRegulacao,
    string Status,
    string StatusConfirmacao,
    string? Observacoes,
    /// <summary>Linha crua do SISREG que originou a consulta (proveniência). Null quando não guardada.</summary>
    string? RawSisreg = null);

public sealed record FiltroConsultasDto(
    Guid? PacienteId = null,
    string? Busca = null,
    DateOnly? DataInicial = null,
    DateOnly? DataFinal = null,
    StatusSolicitacao? Status = null,
    int Limite = 50);
