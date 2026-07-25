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
    bool Ausente);

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

/// <summary>Resultado da sincronização dos profissionais habilitados com o hub FHIR.</summary>
public sealed record SisregSincronizacaoFhirDto(
    int Avaliados,
    int Criados,
    int Vinculados,
    int JaSincronizados,
    IReadOnlyList<string> Erros,
    string Mensagem);
