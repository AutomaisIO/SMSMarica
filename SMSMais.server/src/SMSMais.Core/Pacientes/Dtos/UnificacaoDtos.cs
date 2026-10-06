namespace SMSMais.Core.Pacientes.Dtos;

/// <summary>
/// Pedido de unificação de dois cadastros do MESMO paciente. O operador escolhe quem
/// <b>fica</b> (<see cref="SobreviventeId"/>) e, na tela, resolve campo a campo qual valor
/// predomina — o resultado dessa resolução chega em <see cref="DadosFinais"/>/<see cref="NomeFinal"/>
/// e é aplicado ao sobrevivente antes da fusão.
/// </summary>
public sealed record UnificarPacientesRequest(
    Guid SobreviventeId,
    Guid AbsorvidoId,
    /// <summary>
    /// Demografia final escolhida na tela (os valores que "ficam"). Aplicada ao sobrevivente pelo
    /// caminho de edição normal — portanto auditada campo a campo. Nulo = mantém o sobrevivente
    /// como está. Nome/CPF/nascimento não entram aqui (nome vem em <see cref="NomeFinal"/>; CPF e
    /// nascimento são identidade e não se trocam por esta tela nesta fase).
    /// </summary>
    AtualizarPacienteRequest? DadosFinais = null,
    /// <summary>Nome oficial final escolhido, quando o operador decidir trocar o do sobrevivente.</summary>
    string? NomeFinal = null,
    /// <summary>
    /// Confirmação explícita de que, mesmo com CPF e/ou CNS DIFERENTES entre os dois cadastros,
    /// é a mesma pessoa. Sem isso a unificação é recusada no caso divergente (risco de fundir
    /// duas pessoas distintas — ADR-0041).
    /// </summary>
    bool ConfirmaChavesDivergentes = false);

/// <summary>Prévia (dry-run) da unificação: os dois cadastros, as divergências e o que será movido.</summary>
public sealed record PreviaUnificacaoDto(
    PacienteDto Sobrevivente,
    PacienteDto Absorvido,
    /// <summary>CPF preenchido nos dois e diferente — caso perigoso, pede confirmação.</summary>
    bool CpfDivergente,
    /// <summary>CNS preenchido nos dois e diferente — caso perigoso, pede confirmação.</summary>
    bool CnsDivergente,
    /// <summary>Campos cujo valor difere entre os dois cadastros (para a resolução na tela).</summary>
    IReadOnlyList<DivergenciaCampoDto> Campos,
    /// <summary>Quantas linhas de cada módulo apontam para o absorvido e serão repontadas.</summary>
    IReadOnlyList<ContagemModuloDto> Referencias,
    int TotalReferencias);

/// <summary>Um campo divergente entre os dois cadastros.</summary>
public sealed record DivergenciaCampoDto(string Campo, string? Sobrevivente, string? Absorvido);

/// <summary>Quantas referências ao paciente um módulo do painel tem (agregado para exibição).</summary>
public sealed record ContagemModuloDto(string Modulo, int Quantidade);

/// <summary>Resultado da unificação concluída — o que foi efetivamente movido.</summary>
public sealed record ResultadoUnificacaoDto(
    Guid SobreviventeId,
    Guid AbsorvidoId,
    int ReferenciasRepontadas,
    int ClinicoRepontado,
    int IdentificadoresAbsorvidos);
