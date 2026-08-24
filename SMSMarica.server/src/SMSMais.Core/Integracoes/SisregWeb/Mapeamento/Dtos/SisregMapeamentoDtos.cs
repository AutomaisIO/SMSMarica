namespace SMSMais.Core.Integracoes.SisregWeb.Mapeamento.Dtos;

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
    // O SIGTAP não aparece aqui de propósito: este código é só o FILTRO da varredura. Quem diz o
    // que o exame é de verdade é o próprio agendamento, na importação — e o que não resolver lá
    // vira pendência no histórico de erro, junto das outras.
    /// <summary>Id da linha no catálogo global de procedimentos do SISREG. NULL enquanto o
    /// procedimento não foi catalogado (entra ao "Atualizar mapeamento").</summary>
    Guid? DeParaId = null,
    /// <summary>Importar este procedimento NESTA unidade avisa o paciente por WhatsApp?
    /// Decisão da unidade — outra unidade pode decidir diferente para o mesmo procedimento.
    /// Opt-in: nasce desligado.</summary>
    bool EnviarConfirmacao = false);

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
