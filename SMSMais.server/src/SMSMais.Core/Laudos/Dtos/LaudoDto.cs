using SMSMais.Core.SolicitacoesExame.Dtos;
using SMSMais.Data.Entities.Enums;

namespace SMSMais.Core.Laudos.Dtos;

public sealed record LaudoDto(
    Guid Id,
    string StudyInstanceUID,
    int Versao,
    Guid? LaudoAnteriorId,
    Guid? PacienteId,
    string? PacienteNome,
    string? PacienteCpf,
    // Nome cru do DICOM (0010,0010) capturado na criação — só rótulo temporário
    // enquanto não há vínculo (PacienteId). Limpo ao associar.
    string? PacienteNomeDicom,
    Guid MedicoId,
    string MedicoNome,
    string MedicoCrm,
    string MedicoUfCrm,
    string? MedicoRqe,
    Guid? LaudoTemplateId,
    string? LaudoTemplateNome,
    string Titulo,
    string ConteudoJson,
    string ConteudoHtml,
    StatusLaudo Status,
    string? BiRads,
    string? BiRadsSugerido,
    string? RespostasChecklist,
    DateTime? FinalizadoEm,
    DateTime CriadoEm,
    DateTime? AtualizadoEm,
    bool Assinado = false,
    // Elegibilidade da assinatura digital, resolvida no servidor (ObterPorId):
    // o usuário logado é o autor + laudo finalizado + não assinado + autor tem
    // rubrica. Deixa o front liberar/bloquear o botão "Assinar" sem precisar do
    // módulo Medicos (que o próprio médico não tem).
    bool PodeAssinar = false,
    string? MotivoBloqueioAssinatura = null,
    bool MedicoTemRubrica = false,
    // ADR-0061: como o AUTOR oficializa (Desktop/Nuvem/SemCertificado) — decide o botão e a
    // mensagem de espera no painel — e se o documento oficial saiu só com carimbo.
    ModoAssinaturaMedico ModoAssinatura = ModoAssinaturaMedico.SemCertificado,
    bool AssinaturaSemCertificado = false);

public sealed record LaudoListItemDto(
    Guid Id,
    string StudyInstanceUID,
    int Versao,
    Guid? PacienteId,
    string? PacienteNome,
    // Rótulo temporário do DICOM enquanto o exame não tem vínculo (ver LaudoDto).
    string? PacienteNomeDicom,
    Guid MedicoId,
    string MedicoNome,
    string Titulo,
    StatusLaudo Status,
    string? BiRads,
    DateTime? FinalizadoEm,
    DateTime CriadoEm,
    bool Assinado = false,
    // Checks do aviso "laudo pronto" ao paciente (✓ enviado, ✓✓ entregue, ✓✓ azul
    // lida/visualizada, ⚠ falha). Null quando não há comunicação (ex.: não assinado).
    ComunicacaoChipDto? ChipLaudoPronto = null,
    // ---- Contexto do PEDIDO (mesma leitura da tela de Solicitações) ----
    // O laudo se liga ao exame só pelo StudyInstanceUID, sem FK; estes campos são resolvidos na
    // listagem pelos dois caminhos de sempre (worklist consumada ou associação explícita) e são
    // null no laudo ÓRFÃO — study que não casa com solicitação nenhuma.
    string? AccessionNumber = null,
    string? CodigoSolicitacao = null,
    string? TipoExameNome = null,
    ModalidadeDicom? Modalidade = null,
    string? UnidadeExecutanteNome = null,
    // ADR-0061: liberado só com carimbo (médico sem certificado), sem ICP-Brasil.
    bool AssinaturaSemCertificado = false);

/// <summary>Página da listagem de laudos (paginação offset + total para os controles).</summary>
public sealed record PaginaLaudosDto(
    IReadOnlyList<LaudoListItemDto> Itens,
    int Total,
    int Pagina,
    int Tamanho);

public sealed record LaudoHistoricoItemDto(
    Guid Id,
    int Versao,
    StatusLaudo Status,
    Guid MedicoId,
    string MedicoNome,
    DateTime CriadoEm,
    DateTime? FinalizadoEm);

/// <summary>
/// Linha do dicionário StudyInstanceUID → laudo. Usado pela listagem PACS
/// para saber se já existe laudo (último, ativo) por exame em uma chamada só.
/// </summary>
public sealed record LaudoPorStudyDto(
    string StudyInstanceUID,
    Guid LaudoId,
    int Versao,
    StatusLaudo Status,
    bool Assinado = false);
