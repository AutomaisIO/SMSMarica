using SMSMais.Core.Ouvidoria.Dtos;

namespace SMSMais.Core.Ouvidoria;

/// <summary>
/// Máquina de estados da manifestação de ouvidoria (ADR-0060; plano §2). Toda transição grava um
/// <c>OuvidoriaEvento</c> append-only. A visibilidade (§2.9) é decidida aqui pelo que o usuário
/// atual tem: <c>Ouvidoria</c> vê a fila (denúncia/sigilosa com manifestante mascarado),
/// <c>OuvidoriaSigilo</c> vê denúncias e revela identidade com justificativa, membro de ponto de
/// resposta vê só o que foi encaminhado ao seu ponto, sem manifestante.
/// </summary>
public interface IOuvidoriaManifestacaoService
{
    Task<PaginaDto<ManifestacaoListaDto>> ListarAsync(ManifestacaoFiltro filtro, CancellationToken ct = default);

    /// <summary>Contadores para as abas da fila, no mesmo escopo de visibilidade da lista.</summary>
    Task<OuvidoriaResumoDto> ObterResumoAsync(CancellationToken ct = default);

    /// <summary>Detalhe com trilha, anexos, possíveis duplicatas e ações permitidas. Fora do escopo → 404.</summary>
    Task<ManifestacaoDetalheDto> ObterAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Registra (§2.1). Devolve o código de acesso <b>uma única vez</b>. Funciona sem usuário
    /// autenticado (site público): <c>CriadoPor</c> nulo e autor "Cidadão".
    /// </summary>
    Task<ManifestacaoCriadaDto> RegistrarAsync(RegistrarManifestacaoRequest request, CancellationToken ct = default);

    Task TriarAsync(Guid id, TriarRequest request, CancellationToken ct = default);
    Task EncaminharAsync(Guid id, EncaminharRequest request, CancellationToken ct = default);
    Task PedirComplementacaoAsync(Guid id, TextoRequest request, CancellationToken ct = default);

    /// <summary>Complementação recebida (técnico ou cidadão pelo canal público): retoma o relógio.</summary>
    Task ComplementarAsync(Guid id, TextoComAnexosRequest request, CancellationToken ct = default);

    /// <summary>Resposta da área (membro do ponto ou técnico da ouvidoria). Não visível ao cidadão.</summary>
    Task ResponderAreaAsync(Guid id, TextoComAnexosRequest request, CancellationToken ct = default);
    Task DevolverParaReanaliseAsync(Guid id, TextoRequest request, CancellationToken ct = default);
    Task ResponderCidadaoAsync(Guid id, ResponderCidadaoRequest request, CancellationToken ct = default);
    Task ProrrogarAsync(Guid id, TextoRequest request, CancellationToken ct = default);
    Task CobrarAsync(Guid id, TextoRequest? request, CancellationToken ct = default);
    Task EscalonarAsync(Guid id, TextoRequest request, CancellationToken ct = default);

    /// <summary>Recurso do cidadão (técnico ou canal público): <c>Respondida → EmRecurso</c>, uma vez.</summary>
    Task RegistrarRecursoAsync(Guid id, TextoRequest request, CancellationToken ct = default);
    Task ConcluirAsync(Guid id, CancellationToken ct = default);
    Task ArquivarAsync(Guid id, ArquivarRequest request, CancellationToken ct = default);
    Task EncaminharExternoAsync(Guid id, EncaminharExternoRequest request, CancellationToken ct = default);
    Task HabilitarDenunciaAsync(Guid id, TextoRequest request, CancellationToken ct = default);
    Task AtualizarTeorPseudonimizadoAsync(Guid id, TextoRequest request, CancellationToken ct = default);
    Task AnotarAsync(Guid id, TextoRequest request, CancellationToken ct = default);

    /// <summary>Revela o manifestante de sigilosa/denúncia: grava acesso + evento + auditoria. Exige justificativa.</summary>
    Task<ManifestanteDto> RevelarIdentidadeAsync(Guid id, string justificativa, CancellationToken ct = default);

    /// <summary>Rotina: <c>AguardandoComplementacao</c> há mais de <c>ComplementacaoDias</c> → <c>Arquivada</c>. Devolve quantas.</summary>
    Task<int> ArquivarSemComplementacaoAsync(CancellationToken ct = default);

    /// <summary>Rotina: <c>Respondida</c> há mais de <c>ArquivamentoAutomaticoDias</c> sem recurso → <c>Concluida</c>. Devolve quantas.</summary>
    Task<int> ConcluirRespondidasSemRecursoAsync(CancellationToken ct = default);

    Task<OuvidoriaPainelDto> ObterPainelAsync(DateOnly? de, DateOnly? ate, Guid? unidadeId, CancellationToken ct = default);
}
