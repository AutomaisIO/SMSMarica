namespace SMSMarica.Core.Integracoes.SisregWeb.Mapeamento.Dtos;

/// <summary>Mapeamento da unidade: a "verdade" do SISREG sobre quem executa o quê.</summary>
public sealed record SisregMapeamentoDto(
    Guid UnidadeId,
    string UnidadeNome,
    string? UnidadeCnes,
    /// <summary>Última vez que o mapeamento foi atualizado a partir do SISREG.</summary>
    DateTime? AtualizadoEm,
    int TotalProfissionais,
    int ProfissionaisHabilitados,
    int TotalProcedimentos,
    int ProcedimentosHabilitados,
    /// <summary>Pares (profissional × procedimento) habilitados = requisições por varredura.</summary>
    int CombinacoesHabilitadas,
    IReadOnlyList<SisregProfissionalDto> Profissionais);

public sealed record SisregProfissionalDto(
    Guid Id,
    string Cpf,
    string Nome,
    bool Habilitado,
    Guid? PractitionerId,
    DateTime? SincronizadoEm,
    bool Ausente,
    IReadOnlyList<SisregProcedimentoDto> Procedimentos);

public sealed record SisregProcedimentoDto(
    Guid Id,
    string Codigo,
    string Nome,
    bool Habilitado,
    bool Grupo,
    bool Ausente,
    /// <summary>SIGTAP confirmado para este procedimento (só dígitos). NULL = de-para pendente.</summary>
    string? CodigoSigtap = null,
    /// <summary>
    /// Sem SIGTAP confirmado o procedimento <b>não entra na varredura</b>, mesmo habilitado — a
    /// agenda do SISREG não informa SIGTAP, e sem ele a solicitação nasceria sem categoria e sem
    /// worklist. A tela usa isto para avisar o operador antes que ele ligue o sincronismo e
    /// conclua, erradamente, que a unidade está coberta.
    /// </summary>
    bool SigtapPendente = false,
    /// <summary>Id da linha no CATÁLOGO global de procedimentos — é por ele que a tela confirma o
    /// de-para SIGTAP. NULL enquanto o procedimento não foi catalogado (só entra no catálogo ao
    /// "Atualizar mapeamento"). O de-para é nacional; o aviso ao paciente NÃO é.</summary>
    Guid? DeParaId = null,
    /// <summary>Importar este procedimento NESTA unidade avisa o paciente por WhatsApp?
    /// Decisão da unidade — outra unidade pode decidir diferente para o mesmo procedimento.</summary>
    bool EnviarConfirmacao = true);

/// <summary>Resultado de uma atualização do mapeamento contra o SISREG.</summary>
public sealed record SisregMapeamentoAtualizacaoDto(
    int ProfissionaisEncontrados,
    int ProfissionaisNovos,
    int ProfissionaisAusentes,
    int ProcedimentosEncontrados,
    int ProcedimentosNovos,
    int ProcedimentosAusentes,
    int RequisicoesFeitas,
    string Mensagem);

/// <summary>Ligar/desligar um item do mapeamento.</summary>
public sealed record AlternarHabilitacaoRequest(bool Habilitado);

/// <summary>Ligar/desligar vários de uma vez (checkbox do cabeçalho da tela).</summary>
public sealed record AlternarEmLoteRequest(IReadOnlyList<Guid> Ids, bool Habilitado);

/// <summary>Ligar/desligar o aviso por WhatsApp de um procedimento nesta unidade.</summary>
public sealed record AlternarEnvioConfirmacaoRequest(bool Enviar);

/// <summary>Resultado da sincronização dos profissionais habilitados com o hub FHIR.</summary>
public sealed record SisregSincronizacaoFhirDto(
    int Avaliados,
    int Criados,
    int Vinculados,
    int JaSincronizados,
    IReadOnlyList<string> Erros,
    string Mensagem);
