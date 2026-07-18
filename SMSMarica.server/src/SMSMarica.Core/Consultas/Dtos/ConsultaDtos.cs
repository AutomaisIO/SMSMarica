using SMSMarica.Core.SolicitacoesExame.Dtos;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Consultas.Dtos;

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
    ComunicacaoChipDto? ChipConfirmacao = null);

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
    string? Observacoes);

public sealed record FiltroConsultasDto(
    Guid? PacienteId = null,
    string? Busca = null,
    DateOnly? DataInicial = null,
    DateOnly? DataFinal = null,
    StatusSolicitacao? Status = null,
    int Limite = 50);
